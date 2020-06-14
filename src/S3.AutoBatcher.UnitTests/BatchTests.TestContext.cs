using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace S3.AutoBatcher.UnitTests
{
	public partial class BatchTests
	{
		private class TestContext
		{
			public readonly AutoResetEvent BatchExecutedEvent = new AutoResetEvent(false);

			private TimeSpan _enlistAwaitTimeout = TimeSpan.FromMilliseconds(125);
			private Batch<string> _sut;

			public int ExecutionCount;


			public ConcurrentBag<string> ExecutedRequests { get; } = new ConcurrentBag<string>();
			public Batch<string> Sut => _sut ??= BuildSut();

			private Task OnExecute(IReadOnlyCollection<string> items, CancellationToken cancellationToken)
			{
				foreach (var request in items) ExecutedRequests.Add(request);
				Interlocked.Increment(ref ExecutionCount);

				BatchExecutedEvent.Set();
				return Task.CompletedTask;
			}

			private Batch<string> BuildSut()
			{
				return new Batch<string>(new BatchConfiguration<string>(OnExecute)
				{
					AddMoreItemsTimeWindow = _enlistAwaitTimeout
				});
			}

			public TestContext SetEnlistAwait(TimeSpan timeout)
			{
				_enlistAwaitTimeout = timeout;
				return this;
			}
		}

	}
}