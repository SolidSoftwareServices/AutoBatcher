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
			public IReadOnlyList<ConcurrentBag<string>> ExecutedChunks => _chunks;
			public Batch<string> Sut => _sut ??= BuildSut();


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
				if (chunkItems.Count > 0)
				{
					var current = new ConcurrentBag<string>();
					_chunks.Add(current);
					foreach (var request in chunkItems)
					{
						current.Add(request);
					}
				}

				BatchExecutedEvent.Set();
				BatchExecutedEvent.Reset();
				return Task.CompletedTask;
			}

			public TestContext WithBatchSize(int batchSize)
			{
				_batchSize = batchSize;
				return this;
			}
		}

	}
}