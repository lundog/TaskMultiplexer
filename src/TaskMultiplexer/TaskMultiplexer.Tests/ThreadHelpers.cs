using System;
using System.Threading;

namespace TaskMultiplexing.Tests
{
	public static class ThreadHelpers
	{
		public static void InvokeThreads(int numThreads, Action action)
		{
			var threads = new Thread[numThreads];
			for (int i = 0; i < numThreads; i++)
			{
				threads[i] = new Thread(Invoke);
			}
			for (int i = 0; i < numThreads; i++)
			{
				threads[i].Start(action);
			}
			for (int i = 0; i < numThreads; i++)
			{
				threads[i].Join();
			}
		}

		private static void Invoke(dynamic action) => action();
	}
}
