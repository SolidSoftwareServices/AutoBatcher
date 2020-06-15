using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Colorful;

namespace S3.AutoBatcher.Samples.SimpleProducer
{

	/// <summary>
	/// It processes the batch chunks for the sample.
	/// The process consists in joining the numbers in the current batch and printing them on the screen
	/// </summary>
	class BatchChunkProcessor : IBatchChunkProcessor<int>
	{
		private int _batchNumber = 0;
		public Task Process(IReadOnlyCollection<int> chunkItems, CancellationToken cancellationToken)
		{
			Console.WriteLine($"Batch #{++_batchNumber}, items processed:",Color.DarkGreen);
			//prints hte items comma-separated
			var current = string.Join(',', chunkItems);
			Console.WriteLine(current,Color.Olive);
			return Task.CompletedTask;
		}
	}
}