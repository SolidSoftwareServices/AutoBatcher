using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S3.Threading;

namespace S3.AutoBatcher
{
	public sealed class Batch<TBatchItem>: IBatch<TBatchItem> where TBatchItem : class
	{
		public string Id { get; }
		public TimeSpan EnlistAwaitTimeout { get; }
		private readonly HashSet<BatchAggregatorToken<TBatchItem>> _currentBatchAggregators =
			new HashSet<BatchAggregatorToken<TBatchItem>>();

		private readonly ManualResetEventAsync _resetEvent = new ManualResetEventAsync(false);
		private readonly object _syncLock = new object();
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private ConcurrentBag<TBatchItem> _items=new ConcurrentBag<TBatchItem>();

		public BatchStatus Status { get; private set; } = BatchStatus.Opened;
		public Batch(BatchConfiguration<TBatchItem> configuration)
		{
			Id = configuration.Identifier;
			EnlistAwaitTimeout = configuration.AddMoreItemsTimeWindow;
			_onExecute = configuration.OnExecuteBatchHandler;
		}
		private Guid _lastOperation = Guid.NewGuid();

		public void Add(TBatchItem item,BatchAggregatorToken<TBatchItem> token)
		{
			ThrowIfInvalidToken(token);
			ThrowIfNoMoreRequestAllowed();
			
			_items.Add(item);
			_lastOperation = Guid.NewGuid();
		}

		

		/// <summary>
		/// Several concurrent callers can contribute to the batch each one of them must hold a token
		/// </summary>
		/// <returns></returns>
		public BatchAggregatorToken<TBatchItem> NewBatchAggregatorToken()
		{
			var token = new BatchAggregatorToken<TBatchItem>(this);
			lock (_syncLock)
			{
				ThrowIfNoMoreRequestAllowed();
				_currentBatchAggregators.Add(token);
			}
			
			_lastOperation = Guid.NewGuid();
			return token;
		}


		private readonly Func<IReadOnlyCollection<TBatchItem>, CancellationToken, Task> _onExecute;

		public async Task AddingItemsToBatchCompleted(BatchAggregatorToken<TBatchItem> token)
		{
			await AwaitEnlistersUntilNoMoreEnlist();

			bool executeBatch;
			Task awaitForTask;

			lock (_syncLock)
			{
				ThrowIfNoMoreRequestAllowed();
				ThrowIfInvalidToken(token);

				_currentBatchAggregators.Remove(token);
				executeBatch = !_currentBatchAggregators.Any();
				if (executeBatch)
				{
					Status = BatchStatus.Executing;
					awaitForTask = _onExecute(_items, _cts.Token);

				}
				else
					awaitForTask = _resetEvent.WaitAsync(_cts.Token);
			}
			await awaitForTask;
			if (executeBatch)
			{
				_items=new ConcurrentBag<TBatchItem>();
				Status = BatchStatus.Executed;

				_resetEvent.Set();

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
		private void ThrowIfNoMoreRequestAllowed()
		{
			if (Status != BatchStatus.Opened)
				throw new InvalidOperationException($"The batch status is not opened. Status:{Status}");
		}

		public void Dispose()
		{
			_cts.Cancel(false);
			_cts.Dispose();

		}
	}
}