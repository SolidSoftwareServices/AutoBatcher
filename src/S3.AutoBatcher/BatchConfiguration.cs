using System;

namespace S3.AutoBatcher
{
	public class BatchConfiguration<TBatchItem>
	{
		

		/// <summary>
		/// Get or sets the batch unique id
		/// </summary>
		public string Identifier { get; set; } = Guid.NewGuid().ToString();
		/// <summary>
		/// Gets or sets the time allowed to add tasks to the batch before being executed
		/// </summary>
		public TimeSpan AddMoreItemsTimeWindow { get; set; } = TimeSpan.Zero;

		
	}
}