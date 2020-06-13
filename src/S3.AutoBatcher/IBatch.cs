using System.Collections.Generic;
using System.Threading.Tasks;

namespace S3.AutoBatcher
{
	public interface IBatch<TBatchItem>
	{
		void Add(TBatchItem item, BatchAggregatorToken<TBatchItem> token);
		BatchAggregatorToken<TBatchItem> NewBatchAggregatorToken();
		Task AddingItemsToBatchCompleted(BatchAggregatorToken<TBatchItem> token);
		IReadOnlyCollection<TBatchItem> EnlistedItems { get; }
	}
}