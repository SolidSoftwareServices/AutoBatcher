﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace S3.AutoBatcher
{
	public interface IBatch<TBatchItem>
	{
		/// <summary>
		/// Adds an item to the batch
		/// </summary>
		/// <param name="item"></param>
		/// <param name="token"></param>
		/// <param name="willAddMoreItemsWithThisToken">When false it completes implicitly <see cref="AddingItemsToBatchCompleted"/> </param>
		Task Add(TBatchItem item, BatchAggregatorToken<TBatchItem> token,bool willAddMoreItemsWithThisToken=true);

		/// <summary>
		/// Obtains a token to contribute to the batch
		/// </summary>
		/// <returns></returns>
		Task<BatchAggregatorToken<TBatchItem>> NewBatchAggregatorToken();

		/// <summary>
		/// Notifies the batch the producer holding the token has finalised
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		Task AddingItemsToBatchCompleted(BatchAggregatorToken<TBatchItem> token);

		/// <summary>
		/// Gets the currently enlisted items
		/// </summary>
		IReadOnlyCollection<TBatchItem> EnlistedItems { get; }
	}
}