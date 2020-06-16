# S3.Autobatcher

<img src="https://github.com/SolidSoftwareServices/AutoBatcher/blob/master/docs/images/logo.png" width="200" height="200"/>

[![NuGet version](https://buildstats.info/nuget/s3.autobatcher?includeprereleases=false)](http://www.nuget.org/packages/s3.autobatcher)
![](https://github.com/SolidSoftwareServices/AutoBatcher/workflows/main/badge.svg)

S3.Autobatcher is an utility to process **"chunks of items"** of an specified size from one or more **"item producers"**.
Its was conceived for scenarios where its needed to collect items from multiplesources and then aggregate/or or compose a single aggregate that is payloaded toghether.



### Features
* Collect items from producers and encapsulates the logic to process them
* Process all the items in a single chunk or auto-chunk the batch when sizes are being reached without blocking the producers
* Supported concurrent producers


### Concepts
* **Batch**: represents a container of items and the logic to invoke the processing and the partition of the items in chunks
* **Chunk**: is a collection of items of a batch that completed to be processed
* **Item Producer**: each one of the code clients that push items to the batch is an item producer
* **Chunk processor**: it processes a batch throughout its chunks

### Technical documentation:
* *Working samples* in the repository
* *Unit tests* in the repository

# QuickStart

Simplest possible example of publishing `int` items  to the `Batch`.  :

```csharp
    ...
    //batch configuration
	var batchConfiguration = new BatchConfiguration<int>
	{
	    //Time window before the current batch chuck collected is executed
		AddMoreItemsTimeWindow = TimeSpan.FromSeconds(3.0)
	};
    //Create batch
    var batchCollector = new Batch<int>(batchConfiguration, new BatchChunkProcessor());

    //obtain a producer token that allows aggregation of items to the batch, there can be more than one concurrent aggregators. Not represented in this example 
    //new aggregator
    using (var token = await batchCollector.NewBatchAggregatorToken())
    {
        //add items until the user cancels
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
           	var item = rnd.Next(0, 25);
			await batchCollector.Add(item, token);
        }
        //notify the batch that this aggregator is done adding items
        await batchCollector.AddingItemsToBatchCompleted(token);
    }
    ...
```

Simplest possible example of a processor of batch chunks where you can define the payload to execute for each chunk: send request, compute,...

```csharp
	class BatchChunkProcessor : IBatchChunkProcessor<int>
	{
		private int _batchNumber = 0;
		public Task Process(IReadOnlyCollection<int> chunkItems, CancellationToken cancellationToken)
		{
			Console.WriteLine($"Batch #{++_batchNumber}, items processed:",Color.DarkGreen);
			//this example prints the items comma-separated
			var current = string.Join(',', chunkItems);
			Console.WriteLine(current,Color.Olive);
			return Task.CompletedTask;
		}
}
```
## License ##

S3.Autobatcher is Open Source software and is released under the [MIT license](https://github.com/SolidSoftwareServices/AutoBatcher/wiki/License).
