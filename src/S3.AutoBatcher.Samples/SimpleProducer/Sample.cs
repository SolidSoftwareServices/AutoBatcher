using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Console = Colorful.Console;

namespace S3.AutoBatcher.Samples.SimpleProducer
{
	internal class Sample : ISample
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		public string Description { get; } =
			@"The following Sample produces numbers until the user cancels(Ctrl+C).
YOU need to PRESS Ctrl+C to stop adding items";

		public async Task Run()
		{
			//itm production lag random generator
			var rnd = new Random((int) DateTime.UtcNow.Ticks);

			//prepare to handle Ctrl+C
			Console.CancelKeyPress += Console_CancelKeyPress;

			//batch configuration
			var batchConfiguration = new BatchConfiguration<int>
			{
				//Time window before the current batch chuck collected is executed
				AddMoreItemsTimeWindow = TimeSpan.FromSeconds(3.0)
			};
			//Create batch
			var batchCollector = new Batch<int>(batchConfiguration, new BatchChunkProcessor());

			//obtain a batch token that allows aggregation of items to the batch, there can be more than one concurrent aggregators. Not represented in this example 
			//new aggregator
			using (var token = await batchCollector.NewBatchAggregatorToken())
			{
				//add items until the user cancels
				while (!_cancellationTokenSource.Token.IsCancellationRequested)
				{
					await AddItemToBatch(token);
				}
				//notify the batch that this aggregator is done adding items
				await batchCollector.AddingItemsToBatchCompleted(token);
			}

			async Task AddItemToBatch(BatchAggregatorToken<int> token)
			{
				

				//generate and add new item to the batch
				var item = rnd.Next(0, 25);
				Console.WriteLine($"Produced {item}", Color.DarkSlateGray);
				await batchCollector.Add(item, token);
				


				//wait to produce next
				var waitMilliseconds = rnd.Next(1, 2000);
				Console.WriteLine($"Waiting {TimeSpan.FromMilliseconds(waitMilliseconds)}", Color.DarkGray);
				Thread.Sleep(waitMilliseconds);
			}
		}

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			_cancellationTokenSource.Cancel();
			e.Cancel = true;
		}
	}
}