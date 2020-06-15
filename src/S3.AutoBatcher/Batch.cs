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
			_batchChunkProcessor = batchChunkProcessor ?? throw new ArgumentNullException(nameof(batchChunkProcessor));

		}
		private Guid _lastOperation = Guid.NewGuid();

		public async Task Add(TBatchItem item,BatchAggregatorToken<TBatchItem> token, bool willAddMoreItemsWithThisToken = true)
		{
			ThrowIfInvalidToken(token);
			if(!await _allowItemsEvent.WaitAsync(_cts.Token)) throw new TaskCanceledException();
			
			_items.Add(item);
			_lastOperation = Guid.NewGuid();
			if (!willAddMoreItemsWithThisToken)
			{
				await AddingItemsToBatchCompleted(token);
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
			await AwaitEnlistersUntilNoMoreEnlist();

			bool executeBatch;
			Task awaitForTask;
			
			lock (_syncLock)
			{
				ThrowIfInvalidToken(token);

				_currentBatchAggregators.Remove(token);
				executeBatch = !_currentBatchAggregators.Any();
				if (executeBatch)
				{
					_allowItemsEvent.Reset();
					Status = BatchStatus.Executing;
					awaitForTask = _batchChunkProcessor.Process(_items, _cts.Token);

				}
				else
					awaitForTask = _resetEvent.WaitAsync(_cts.Token);
			}
			
			await awaitForTask;
			if (executeBatch)
			{
				_items=new ConcurrentBag<TBatchItem>();
				Status = BatchStatus.Opened;
				_allowItemsEvent.Set();
				_resetEvent.Set();
				_resetEvent.Reset();
			}

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