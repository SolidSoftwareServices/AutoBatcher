using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S3.Threading;

namespace S3.AutoBatcher
{
	public sealed class Batch<TBatchItem>: IBatch<TBatchItem> 
	{
		private readonly IBatchChunkProcessor<TBatchItem> _batchChunkProcessor;
		public string Id { get; }
		public TimeSpan EnlistAwaitTimeout { get; }
		private readonly HashSet<BatchAggregatorToken<TBatchItem>> _currentBatchAggregators =
			new HashSet<BatchAggregatorToken<TBatchItem>>();

		private readonly ManualResetEventAsync _resetEvent = new ManualResetEventAsync(false);
		private readonly ManualResetEventAsync _allowItemsEvent = new ManualResetEventAsync(true);
		private readonly object _syncLock = new object();
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private ConcurrentBag<TBatchItem> _items=new ConcurrentBag<TBatchItem>();

		public BatchStatus Status { get; private set; } = BatchStatus.Opened;
		public Batch(BatchConfiguration<TBatchItem> configuration, IBatchChunkProcessor<TBatchItem> batchChunkProcessor)
		{
			if (configuration == null) throw new ArgumentNullException(nameof(configuration));
			Id = configuration.Identifier;
			EnlistAwaitTimeout = configuration.AddMoreItemsTimeWindow;
			ChunkSize = configuration.ChunkSize;
			_batchChunkProcessor = batchChunkProcessor ?? throw new ArgumentNullException(nameof(batchChunkProcessor));

		}

		public int ChunkSize { get; set; }

		private Guid _lastOperation = Guid.NewGuid();

		public async Task Add(TBatchItem item,BatchAggregatorToken<TBatchItem> token, bool willAddMoreItemsWithThisToken = true)
		{
			ThrowIfInvalidToken(token);
			if(!await _allowItemsEvent.WaitAsync(_cts.Token)) throw new TaskCanceledException();
			
			_items.Add(item);
			_lastOperation = Guid.NewGuid();
			if (!willAddMoreItemsWithThisToken )
			{
				await CompleteChunk(token,false);
			}
			else if (ChunkSize > 0 && _items.Count >= ChunkSize)
			{
				await CompleteChunk(token, true);
			}
		}

		

		/// <summary>
		/// Several concurrent callers can contribute to the batch each one of them must hold a token
		/// </summary>
		/// <returns></returns>
		public async Task<BatchAggregatorToken<TBatchItem>> NewBatchAggregatorToken()
		{
			var token = new BatchAggregatorToken<TBatchItem>(this);
			if (!await _allowItemsEvent.WaitAsync(_cts.Token)) throw new TaskCanceledException();
			lock (_syncLock)
			{
				_currentBatchAggregators.Add(token);
			}
			
			_lastOperation = Guid.NewGuid();
			return token;
		}



		public async Task AddingItemsToBatchCompleted(BatchAggregatorToken<TBatchItem> token)
		{
			await CompleteChunk(token,false);
		}

		private async Task CompleteChunk(BatchAggregatorToken<TBatchItem> token,bool chunkLimitReachedTriggers)
		{
			await AwaitEnlistersUntilNoMoreEnlist();

			Task awaitForTask;

			lock (_syncLock)
			{
				ThrowIfInvalidToken(token);

				if (!chunkLimitReachedTriggers)
				{
					_currentBatchAggregators.Remove(token);
				}

				var executeBatch = chunkLimitReachedTriggers || !_currentBatchAggregators.Any();
				if (executeBatch)
				{
					_allowItemsEvent.Reset();
					Status = BatchStatus.Executing;
					var batchItems = _items.ToArray();
					TBatchItem[] itemsToProcess;
					var itemsToKeep = new TBatchItem[0];
					if (ChunkSize == 0)
					{
						itemsToProcess = batchItems;
					}
					else
					{
						itemsToProcess = batchItems.Take(ChunkSize).ToArray();
						itemsToKeep = batchItems.Skip(ChunkSize).ToArray();
					}

					awaitForTask = _batchChunkProcessor.Process(itemsToProcess, _cts.Token);
					//immediately allow new additions
					_items = new ConcurrentBag<TBatchItem>(itemsToKeep);
					Status = BatchStatus.Opened;
					_allowItemsEvent.Set();
					_resetEvent.Set();
					_resetEvent.Reset();
				}
				else
					awaitForTask = _resetEvent.WaitAsync(_cts.Token);
			}

			await awaitForTask;

			async Task AwaitEnlistersUntilNoMoreEnlist()
			{
				var current = _lastOperation;
				//by doing this we are letting other asynchronous tasks to enlist before executing the batch
				await Task.Delay(EnlistAwaitTimeout);

				while (current != _lastOperation)
				{
					current = _lastOperation;
					await Task.Delay(EnlistAwaitTimeout);
				}
			}
		}

		public IReadOnlyCollection<TBatchItem> EnlistedItems => _items.ToArray();

		private void ThrowIfInvalidToken(BatchAggregatorToken<TBatchItem> token)
		{
			if(token.Disposed) 
				throw new ObjectDisposedException("token","The aggregator token was already disposed");
			if (!_currentBatchAggregators.Contains(token))
				throw new InvalidOperationException("The aggregator is not part of the current batch");
		}
		
		public void Dispose()
		{
			_cts.Cancel(false);
			_cts.Dispose();

		}
	}
}