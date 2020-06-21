using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace S3.AutoBatcher
{
	public interface IBatchChunkProcessor<in TBatchItem>
	{
		/// <summary>
		/// process a batch chunk
		/// </summary>
		/// <param name="chunkItems"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		Task Process(IReadOnlyCollection<TBatchItem> chunkItems, CancellationToken cancellationToken);

		/// <summary>
		/// it handles what to do on error
		/// </summary>
		/// <param name="chunkItems"></param>
		/// <param name="exception"></param>
		/// <param name="currentAttemptNumber"></param>
		/// <returns></returns>
		ErrorResult HandleError(IReadOnlyCollection<TBatchItem> chunkItems, Exception exception, int currentAttemptNumber);
	}
}