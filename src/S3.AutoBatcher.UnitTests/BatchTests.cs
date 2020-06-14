using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace S3.AutoBatcher.UnitTests
{
	[Parallelizable(ParallelScope.All)]
	[TestFixture]
	public partial class BatchTests
	{
		

		[Test]
		public async Task CanAddItem()
		{
			var context = new TestContext();
			var value = Guid.NewGuid().ToString();
			using (var token = context.Sut.NewBatchAggregatorToken())
			{
				context.Sut.Add(value, token);
				Thread.Sleep(10);
				Assert.IsEmpty(context.ExecutedRequests);
				await context.Sut.AddingItemsToBatchCompleted(token);
				Assert.IsTrue(context.ExecutedRequests.Single() == value);
			}
		}

		[Test]
		public async Task CanAggregateFromSeveralAggregators()
		{
			var context = new TestContext();
			var itemsCount = 10000;
			using (var token = context.Sut.NewBatchAggregatorToken())
			{
				for (var i = 0; i < itemsCount; i++) context.Sut.Add(i.ToString(), token);
				Assert.IsEmpty(context.ExecutedRequests);
				await context.Sut.AddingItemsToBatchCompleted(token);
			}

			var items = Enumerable.Range(0, itemsCount - 1);
			foreach (var item in items)
			{
				var count = context.ExecutedRequests.Count(y => y == item.ToString());
				Assert.IsTrue(count == 1, $"Item number{item} count={count}");
			}
		}

		[Test]
		public async Task CanAggregateFromSeveralAggregators_Concurrently()
		{
			var context = new TestContext();
			var itemsCount = 10000;
			var tasks = new List<Task>();
			var sut = context.SetEnlistAwait(TimeSpan.FromMilliseconds(200)).Sut;

			var mre = new ManualResetEvent(false);
			for (var i = 0; i < itemsCount; i++)
			{
				var idx = i;
				var t = Task.Factory.StartNew(async () =>
				{
					using (var token = sut.NewBatchAggregatorToken())
					{
						mre.WaitOne();
						sut.Add(idx.ToString(), token);
						Assert.IsEmpty(context.ExecutedRequests);
						await sut.AddingItemsToBatchCompleted(token);
					}
				});
				tasks.Add(t);
			}

			mre.Set();
			await Task.WhenAll(tasks);

			if (!context.BatchExecutedEvent.WaitOne(TimeSpan.FromSeconds(5))) Assert.Fail("Execute was not completed");
			ThrowIfAnyFaulted();

			var items = Enumerable.Range(0, itemsCount - 1);
			foreach (var item in items)
			{
				var count = context.ExecutedRequests.Count(y => y == item.ToString());
				Assert.IsTrue(count == 1, $"Item number{item} count={count}");
			}

			void ThrowIfAnyFaulted()
			{
				var faultedTask = tasks.FirstOrDefault(x => x.IsFaulted);
				if (faultedTask != null) throw faultedTask.Exception?.InnerException;
			}
		}


		[Test]
		public async Task CanCompleteWhenNoItems()
		{
			var context = new TestContext();
			using (var token = context.Sut.NewBatchAggregatorToken())
			{
				await context.Sut.AddingItemsToBatchCompleted(token);
				Assert.IsEmpty(context.ExecutedRequests);
			}
		}

		[Test]
		public async Task CannotAddAfterTokenDispose()
		{
			var context = new TestContext();
			var value = Guid.NewGuid().ToString();
			var token = context.Sut.NewBatchAggregatorToken();
			token.Dispose();
			Assert.Throws<ObjectDisposedException>(() => context.Sut.Add(value, token));
		}


		[Test]
		public async Task CannotCompleteAfterTokenDispose()
		{
			var context = new TestContext();
			var value = Guid.NewGuid().ToString();
			var token = context.Sut.NewBatchAggregatorToken();
			context.Sut.Add(value, token);
			token.Dispose();
			Assert.ThrowsAsync<ObjectDisposedException>(
				async () => await context.Sut.AddingItemsToBatchCompleted(token));
		}


		[Test]
		public async Task CannotReuseCompletedBatch()
		{
			var context = new TestContext();
			using (var token = context.Sut.NewBatchAggregatorToken())
			{
				var v1 = Guid.NewGuid().ToString();
				context.Sut.Add(v1, token);

				await context.Sut.AddingItemsToBatchCompleted(token);
				var v2 = Guid.NewGuid().ToString();
				Assert.Throws<InvalidOperationException>(() => context.Sut.Add(v2, token));
			}
		}

		[Test]
		public async Task EnlistedItemsShowsCorrectCount()
		{
			var context = new TestContext();
			const int itemsCount = 100;
			Assert.AreEqual(0, context.Sut.EnlistedItems.Count);
			using (var token = context.Sut.NewBatchAggregatorToken())
			{
				for (var i = 0; i < itemsCount; i++) context.Sut.Add(i.ToString(), token);

				Assert.AreEqual(itemsCount, context.Sut.EnlistedItems.Count);
				await context.Sut.AddingItemsToBatchCompleted(token);
				Assert.AreEqual(0, context.Sut.EnlistedItems.Count);
			}
		}

		[Test]
		public async Task ItIsOnlyExecutedWhenRequested()
		{
			var context = new TestContext();
			var value = Guid.NewGuid().ToString();
			using (var token = context.Sut.NewBatchAggregatorToken())
			{
				context.Sut.Add(value, token);
				Thread.Sleep(10);
				Assert.IsEmpty(context.ExecutedRequests);
				await context.Sut.AddingItemsToBatchCompleted(token);
			}

			Assert.IsTrue(context.ExecutedRequests.Single() == value);
		}
	}
}