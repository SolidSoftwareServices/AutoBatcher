namespace S3.AutoBatcher
{
	public enum BatchStatus
	{
		/// <summary>
		/// it admits requests
		/// </summary>
		Opened = 1,
		/// <summary>
		/// It is being executed
		/// </summary>
		/// <remarks>it does not admit requests</remarks>
		Executing,
		/// <summary>
		/// It ewas executed
		/// </summary>
		/// <remarks>it does not admit requests</remarks>
		Executed
	}
}