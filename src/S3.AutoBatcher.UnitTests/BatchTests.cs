using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using S3.Threading;

namespace S3.AutoBatcher.UnitTests
{
	[TestFixture]
	public class BatchTests
	{
		private TestContext _context;


		[SetUp]
		public void OnSetup()
		{
			_context = new TestContext();
		}


		[Test]
		public async Task CanAddItem()
		{
			var value = Guid.NewGuid().ToString();
			using (var token = _context.Sut.NewBatchAggregatorToken())
			{
				_context.Sut.Add(value,token);
				Thread.Sleep(10);
				Assert.IsEmpty(_context.ExecutedRequests);
				await _context.Sut.AddingItemsToBatchCompleted(token);
				Assert.IsTrue(_context.ExecutedRequests.Single() == value);

			}
		}


		[Test]
		public async Task CanCompleteWhenNoItems()
		{
			using (var token = _context.Sut.NewBatchAggregatorToken())
			{
				await _context.Sut.AddingItemsToBatchCompleted(token);
				Assert.IsEmpty(_context.ExecutedRequests);

			}
		}

		[Test]
		public async Task ItIsOnlyExecutedWhenRequested()
		{
			var value = Guid.NewGuid().ToString();
			using (var token = _context.Sut.NewBatchAggregatorToken())
			{
				_context.Sut.Add(value, token);
				Thread.Sleep(10);
				Assert.IsEmpty(_context.ExecutedRequests);
				await _context.Sut.AddingItemsToBatchCompleted(token);
			}
			Assert.IsTrue(_context.ExecutedRequests.Single() == value);
		}


		[Test]
		public async Task CannotCompleteAfterTokenDispose()
		{
			var value = Guid.NewGuid().ToString();
			var token = _context.Sut.NewBatchAggregatorToken();
				_context.Sut.Add(value, token);
			token.Dispose();
			Assert.ThrowsAsync<ObjectDisposedException>(async () => await _context.Sut.AddingItemsToBatchCompleted(token));
		}

		[Test]
		public async Task CannotAddAfterTokenDispose()
		{
			var value = Guid.NewGuid().ToString();
			var token = _context.Sut.NewBatchAggregatorToken();
			token.Dispose();
			Assert.Throws<ObjectDisposedException>(() => _context.Sut.Add(value, token));
		}
		[Test]
		public async Task CanAggregateFromSeveralAggregators()
		{
			var itemsCount = 10000;
			using (var token = _context.Sut.NewBatchAggregatorToken())
			{
				
				for (var i = 0; i < itemsCount; i++)
				{
					_context.Sut.Add(i.ToString(),token);
				}
				Assert.IsEmpty(_context.ExecutedRequests);
				await _context.Sut.AddingItemsToBatchCompleted(token);
			}

			var items = Enumerable.Range(0, itemsCount-1);
			foreach (var item in items)
			{
				var count = _context.ExecutedRequests.Count(y => y == item.ToString());
				Assert.IsTrue(count == 1,$"Item number{item} count={count}");
			}
		
		}
		[Test]
		public async Task CanAggregateFromSeveralAggregators_Concurrently()
		{
			var itemsCount = 10000;
			var tasks=new List<Task>();
			var sut = _context.SetEnlistAwait(TimeSpan.FromMilliseconds(200)).Sut;
			for (var i = 0; i < itemsCount; i++)
			{
				var idx = i;
				var t=Task.Factory.StartNew(async() =>
				{
					
					using (var token = sut.NewBatchAggregatorToken())
					{


						sut.Add(idx.ToString(), token);
						Assert.IsEmpty(_context.ExecutedRequests);
						await sut.AddingItemsToBatchCompleted(token);
					}
				});
				tasks.Add(t);
			}
			
			await Task.WhenAll(tasks);

			if (!_context.BatchExecutedEvent.WaitOne(TimeSpan.FromSeconds(5)))
			{
				Assert.Fail("Execute was not completed");
			}
			ThrowIfAnyFaulted();

			var items = Enumerable.Range(0, itemsCount - 1);
			foreach (var item in items)
			{
				var count = _context.ExecutedRequests.Count(y => y == item.ToString());
				Assert.IsTrue(count == 1, $"Item number{item} count={count}");
			}

			void ThrowIfAnyFaulted()
			{
				var faultedTask = tasks.FirstOrDefault(x => x.IsFaulted);
				if (faultedTask != null)
				{
					throw faultedTask.Exception?.InnerException;
				}
			}
		}


		[Test]
		public async Task CannotReuseCompletedBatch()
		{
			using (var token = _context.Sut.NewBatchAggregatorToken())
			{
				var v1 = Guid.NewGuid().ToString();
				_context.Sut.Add(v1,token);

				await _context.Sut.AddingItemsToBatchCompleted(token);
				var v2 = Guid.NewGuid().ToString();
				Assert.Throws<InvalidOperationException>(() => _context.Sut.Add(v2,token));
			}
		}

		[Test]
		public async Task EnlistedItemsShowsCorrectCount()
		{
			const int itemsCount = 100;
			Assert.AreEqual(0, _context.Sut.EnlistedItems.Count);
			using (var token = _context.Sut.NewBatchAggregatorToken())
			{
				for (var i = 0; i < itemsCount; i++)
				{
					_context.Sut.Add(i.ToString(), token);
				}
				
				Assert.AreEqual(itemsCount,_context.Sut.EnlistedItems.Count);
				await _context.Sut.AddingItemsToBatchCompleted(token);
				Assert.AreEqual(0, _context.Sut.EnlistedItems.Count);
			}
		}


		private class TestContext
		{

			

			public ConcurrentBag<string> ExecutedRequests { get; } = new ConcurrentBag<string>();

			public int ExecutionCount;
			private async Task OnExecute(IReadOnlyCollection<string> items, CancellationToken cancellationToken)
			{
				foreach (var request in items)
				{
					ExecutedRequests.Add(request);
				}
				Interlocked.Increment(ref ExecutionCount);

				BatchExecutedEvent.Set();
			}

			public readonly AutoResetEvent BatchExecutedEvent = new AutoResetEvent(false);

			public TestContext()
			{
			}
			private Batch<string> _sut;
			public Batch<string> Sut => _sut ??= BuildSut();

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

			private TimeSpan _enlistAwaitTimeout=TimeSpan.FromMilliseconds(125);
		}

	}
}