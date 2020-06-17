using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
			using (var token =  await context.Sut.NewBatchAggregatorToken())
			{
				await context.Sut.Add(value, token);
				Thread.Sleep(10);
				Assert.IsEmpty(context.ExecutedChunks);
				await context.Sut.AddingItemsToBatchCompleted(token);
				Assert.IsTrue(context.ExecutedChunks.Single().Single() == value);
			}
		}

		[Test]
		public async Task CanAggregateFromSeveralAggregators()
		{
			var context = new TestContext();
			var itemsCount = 10000;
			using (var token =  await context.Sut.NewBatchAggregatorToken())
			{
				for (var i = 0; i < itemsCount; i++) await context.Sut.Add(i.ToString(), token);
				Assert.IsEmpty(context.ExecutedChunks);
				await context.Sut.AddingItemsToBatchCompleted(token);
			}

			var chunk = context.ExecutedChunks.Single();
			var items = Enumerable.Range(0, itemsCount - 1);
			foreach (var item in items)
			{
				var count = chunk.Count(y => y == item.ToString());
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
					using (var token = await sut.NewBatchAggregatorToken())
					{
						mre.WaitOne();
						await sut.Add(idx.ToString(), token);
						Assert.IsEmpty(context.ExecutedChunks);
						await sut.AddingItemsToBatchCompleted(token);
					}
				});
				tasks.Add(t);
			}

			mre.Set();
			await Task.WhenAll(tasks);

			if (!context.BatchExecutedEvent.WaitOne(TimeSpan.FromSeconds(90)))
			{
				Assert.Fail("Execute was not completed");
			}
			ThrowIfAnyFaulted();
			var chunk = context.ExecutedChunks.Single();
			var items = Enumerable.Range(0, itemsCount - 1);
			foreach (var item in items)
			{
				var count = chunk.Count(y => y == item.ToString());
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
			using (var token =  await context.Sut.NewBatchAggregatorToken())
			{
				await context.Sut.AddingItemsToBatchCompleted(token);
			}
			Assert.AreEqual(0,context.ExecutedChunks.Count);

		}

		[Test]
		public async Task CannotAddAfterTokenDispose()
		{
			var context = new TestContext();
			var value = Guid.NewGuid().ToString();
			var token =  await context.Sut.NewBatchAggregatorToken();
			token.Dispose();
			Assert.ThrowsAsync<ObjectDisposedException>(async() => await context.Sut.Add(value, token));
		}


		[Test]
		public async Task CannotCompleteAfterTokenDispose()
		{
			var context = new TestContext();
			var value = Guid.NewGuid().ToString();
			var token =  await context.Sut.NewBatchAggregatorToken();
			await context.Sut.Add(value, token);
			token.Dispose();
			Assert.ThrowsAsync<ObjectDisposedException>(
				async () => await context.Sut.AddingItemsToBatchCompleted(token));
		}


		[Test]
		public async Task BatchAcceptsRequestAfterAggregatorsCompleted()
		{
			var v1 = Guid.NewGuid().ToString();
			var v2 = Guid.NewGuid().ToString();

			var context = new TestContext();
			using (var token =  await context.Sut.NewBatchAggregatorToken())
			{

				await context.Sut.Add(v1, token);

				await context.Sut.AddingItemsToBatchCompleted(token);
			}

			using (var token =  await context.Sut.NewBatchAggregatorToken())
			{
				await context.Sut.Add(v2, token);

				await context.Sut.AddingItemsToBatchCompleted(token);
			}

			Assert.AreEqual(2, context.ExecutedChunks.Count);
			var actualJoin = context.ExecutedChunks.SelectMany(x=>x).Distinct().ToArray();
			Assert.AreEqual(2, actualJoin.Length);
			Assert.IsTrue(actualJoin.Contains(v1));
			Assert.IsTrue(actualJoin.Contains(v2));

		}
		[TestCase(50, 0)]
		[TestCase(50,10)]
		[TestCase(52, 10)]
		public async Task CanAutoProcessBatchChunk(int numItemsToPublish,int batchSize)
		{
			
			var context = new TestContext().WithBatchSize(batchSize);
			var expected=new List<string>();
			using (var token = await context.Sut.NewBatchAggregatorToken())
			{
				for (int i = 0; i < numItemsToPublish;i++)
				{
					var item = Guid.NewGuid().ToString();
					
					await context.Sut.Add(item, token);
					expected.Add(item);
				}

				await context.Sut.AddingItemsToBatchCompleted(token);
			}

			var expectedChunks = batchSize==0?1:(int)Math.Ceiling((numItemsToPublish / (double)batchSize));
			Assert.AreEqual(expectedChunks,context.ExecutedChunks.Count);


			var joinedActual = context.ExecutedChunks.SelectMany(x=>x).ToArray();
			Assert.AreEqual(numItemsToPublish, joinedActual.Length);
			
			Assert.IsTrue(expected.All(joinedActual.Contains));
		}


		[Test]
		public async Task EnlistedItemsShowsCorrectCount()
		{
			var context = new TestContext();
			const int itemsCount = 100;
			Assert.AreEqual(0, context.Sut.EnlistedItems.Count);
			using (var token =  await context.Sut.NewBatchAggregatorToken())
			{
				for (var i = 0; i < itemsCount; i++) await context.Sut.Add(i.ToString(), token);

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
			using (var token =  await context.Sut.NewBatchAggregatorToken())
			{
				await context.Sut.Add(value, token);
				Thread.Sleep(10);
				Assert.IsEmpty(context.ExecutedChunks);
				await context.Sut.AddingItemsToBatchCompleted(token);
			}

			Assert.IsTrue(context.ExecutedChunks.Single().Single() == value);
		}
	}
}