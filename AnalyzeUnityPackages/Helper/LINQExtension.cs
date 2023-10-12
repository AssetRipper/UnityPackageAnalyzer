namespace AssetRipper.AnalyzeUnityPackages.Helper;

public static class LINQExtension
{
	public static void EnqueueRange<T>(this Queue<T> queue, IEnumerable<T> items)
	{
		foreach (T item in items)
		{
			queue.Enqueue(item);
		}
	}
}
