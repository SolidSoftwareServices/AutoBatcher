using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace S3.AutoBatcher.UnitTests
{
	public partial class BatchTests
	{
		private class TestContext:IBatchChunkProcessor<string>
		{
			public readonly AutoResetEvent BatchExecutedEvent = new AutoResetEvent(false);

			private TimeSpan _enlistAwaitTimeout = TimeSpan.FromMilliseconds(125);
			private Batch<string> _sut;



			
			private readonly List<ConcurrentBag<string>> _chunks=new List<ConcurrentBag<string>>();
			private int _batchSize;
			private int? _failingOnBatchNumber;
			public IReadOnlyList<ConcurrentBag<string>> ExecutedChunks => _chunks;
			public Batch<string> Sut => _sut ??= BuildSut();
			private int _batchCount;
			private int _numberOfFailuresOnRetry;
			private ErrorResult? _errorResult;

			private Batch<string> BuildSut()
			{
				return new Batch<string>(new BatchConfiguration<string>
				{
					AddMoreItemsTimeWindow = _enlistAwaitTimeout,
					ChunkSize=_batchSize
				},this);
			}

			public TestContext SetEnlistAwait(TimeSpan timeout)
			{
				_enlistAwaitTimeout = timeout;
				return this;
			}

			public Task Process(IReadOnlyCollection<string> chunkItems, CancellationToken cancellationToken)
			{

				if (_failingOnBatchNumber == Interlocked.Increment(ref _batchCount))
				{
					throw new Exception($"fail on batch #:{_batchCount}");
				}

				AddChunk(chunkItems);

				BatchExecutedEvent.Set();
				BatchExecutedEvent.Reset();
				
				return Task.CompletedTask;
			}

			private void AddChunk(IReadOnlyCollection<string> chunkItems)
			{
				if (chunkItems.Count > 0)
				{
					var current = new ConcurrentBag<string>();
					_chunks.Add(current);
					foreach (var request in chunkItems)
					{
						current.Add(request);
					}
				}
			}

			public ErrorResult HandleError(IReadOnlyCollection<string> chunkItems, Exception exception, int currentAttemptNumber)
			{
				if(_errorResult!=ErrorResult.Retry)
					return _errorResult.Value;

				if (--_numberOfFailuresOnRetry > 0)
					return _errorResult.Value;
				else
				{
					AddChunk(chunkItems);
					return ErrorResult.Continue;
				}
			}

			public TestContext WithBatchSize(int batchSize)
			{
				_batchSize = batchSize;
				return this;
			}

			public TestContext FailingOnBatchNumber(int failingOnBatchNumber, ErrorResult errorResult,int numberOfFailuresOnRetry)
			{
				_failingOnBatchNumber = failingOnBatchNumber;
				_numberOfFailuresOnRetry = numberOfFailuresOnRetry;
				_errorResult = errorResult;
				return this;
			}
		}

	}
}