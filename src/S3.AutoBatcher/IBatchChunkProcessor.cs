using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace S3.AutoBatcher
{
	public interface IBatchChunkProcessor<in TBatchItem>
	{
		Task Process(IReadOnlyCollection<TBatchItem> chunkItems, CancellationToken cancellationToken);
	}
}