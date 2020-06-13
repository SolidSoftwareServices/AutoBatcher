using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace S3.AutoBatcher
{
	public class BatchConfiguration<TBatchItem>
	{
		public BatchConfiguration(Func<IReadOnlyCollection<TBatchItem>, CancellationToken, Task> onExecuteBatchHandler)
		{
			OnExecuteBatchHandler = onExecuteBatchHandler;
		}

		/// <summary>
		/// Get or sets the batch unique id
		/// </summary>
		public string Identifier { get; set; } = Guid.NewGuid().ToString();
		/// <summary>
		/// Gets or sets the time allowed to add tasks to the batch before being executed
		/// </summary>
		public TimeSpan AddMoreItemsTimeWindow { get; set; } = TimeSpan.Zero;

		

		public Func<IReadOnlyCollection<TBatchItem>, CancellationToken, Task> OnExecuteBatchHandler { get;  }
	}
}