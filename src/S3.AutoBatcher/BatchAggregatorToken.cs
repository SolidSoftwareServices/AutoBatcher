using System;

namespace S3.AutoBatcher
{
	/// <summary>
	/// Token given to identify the batch aggregator 
	/// </summary>
	/// <summary>
	/// Every contributor to an specific batch needs to obtain a token from the batch that is used to add items to the batch and to request execution when its completed
	/// </summary>
	public class BatchAggregatorToken<TBatchItem>:IDisposable, IEquatable<BatchAggregatorToken<TBatchItem>>
	{
		internal BatchAggregatorToken(IBatch<TBatchItem> batch)
		{
			Batch = batch ?? throw new ArgumentNullException(nameof(batch));
		}

		internal IBatch<TBatchItem> Batch { get; }
		internal Guid Id { get; } = Guid.NewGuid();

		internal bool Disposed { get; set; } = false;

		public void Dispose()
		{
			Disposed = true;
		}

		public bool Equals(BatchAggregatorToken<TBatchItem> other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Id.Equals(other.Id);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((BatchAggregatorToken<TBatchItem>) obj);
		}

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}
	}
}