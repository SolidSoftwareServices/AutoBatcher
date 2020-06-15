using System;
using System.Threading;
using System.Threading.Tasks;

namespace S3.Threading
{
	public sealed class ManualResetEventAsync
	{


		private const int WaitIndefinitely = -1;


		private readonly bool _runSynchronousContinuationsOnSetThread = true;


		private volatile TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();

		public ManualResetEventAsync(bool isSet)
			: this(isSet: isSet, runSynchronousContinuationsOnSetThread: true)
		{
		}


		public ManualResetEventAsync(bool isSet, bool runSynchronousContinuationsOnSetThread)
		{
			this._runSynchronousContinuationsOnSetThread = runSynchronousContinuationsOnSetThread;

			if (isSet)
			{
				this._completionSource.TrySetResult(true);
			}
		}

		public Task<bool> WaitAsync()
		{
			return this.AwaitCompletion(ManualResetEventAsync.WaitIndefinitely, default(CancellationToken));
		}


		public Task<bool> WaitAsync(CancellationToken token)
		{
			return this.AwaitCompletion(ManualResetEventAsync.WaitIndefinitely, token);
		}


		public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token)
		{
			return this.AwaitCompletion((int)timeout.TotalMilliseconds, token);
		}


		public Task<bool> WaitAsync(TimeSpan timeout)
		{
			return this.AwaitCompletion((int)timeout.TotalMilliseconds, default(CancellationToken));
		}

		public void Set()
		{
			if (this._runSynchronousContinuationsOnSetThread)
			{
				this._completionSource.TrySetResult(true);
			}
			else
			{
				Task.Run(() => this._completionSource.TrySetResult(true));
			}
		}

		public void Reset()
		{
			var currentCompletionSource = this._completionSource;

			if (!currentCompletionSource.Task.IsCompleted)
			{
				return;
			}

			Interlocked.CompareExchange(ref this._completionSource, new TaskCompletionSource<bool>(),
				currentCompletionSource);
		}

		private async Task<bool> AwaitCompletion(int timeoutMs, CancellationToken token)
		{
			if (timeoutMs < -1 || timeoutMs > int.MaxValue)
			{
				throw new ArgumentException(
					"The timeout must be either -1ms (indefinitely) or a positive ms value <= int.MaxValue");
			}

			CancellationTokenSource timeoutToken = null;

			if (false == token.CanBeCanceled)
			{
				if (timeoutMs == -1)
				{
					return await this._completionSource.Task;
				}

				timeoutToken = new CancellationTokenSource();
			}
			else
			{
				timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(token);
			}

			using (timeoutToken)
			{
				Task delayTask = Task.Delay(timeoutMs, timeoutToken.Token).ContinueWith((result) =>
				{
					var e = result.Exception;
				}, TaskContinuationOptions.ExecuteSynchronously);

				var resultingTask = await Task.WhenAny(this._completionSource.Task, delayTask).ConfigureAwait(false);

				if (resultingTask != delayTask)
				{
					timeoutToken.Cancel();
					return true;
				}

				token.ThrowIfCancellationRequested();
				return false;
			}
		}
	}

}
