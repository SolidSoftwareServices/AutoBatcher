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



			public ConcurrentBag<string> ExecutedRequests { get; } = new ConcurrentBag<string>();
			public Batch<string> Sut => _sut ??= BuildSut();


			private Batch<string> BuildSut()
			{
				return new Batch<string>(new BatchConfiguration<string>
				{
					AddMoreItemsTimeWindow = _enlistAwaitTimeout
				},this);
			}

			public TestContext SetEnlistAwait(TimeSpan timeout)
			{
				_enlistAwaitTimeout = timeout;
				return this;
			}

			public Task Process(IReadOnlyCollection<string> chunkItems, CancellationToken cancellationToken)
			{
				foreach (var request in chunkItems) ExecutedRequests.Add(request);

				BatchExecutedEvent.Set();
				return Task.CompletedTask;
			}
		}

	}
}