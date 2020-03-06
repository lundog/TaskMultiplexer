using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TaskMultiplexing.Tests
{
    [TestClass]
    public class TaskMultiplexerTests
    {
		[TestMethod]
		public void Get_SlowAsyncValueFactory_InvokesOnce()
		{
			var taskMultiplexer = new TaskMultiplexer<int, string>();
			int valueFactoryInvocationCount = 0;
			Func<int, Task<string>> valueFactory = async x =>
			{
				await Task.Delay(500).ConfigureAwait(false);
				Interlocked.Increment(ref valueFactoryInvocationCount);
				return x.ToString();
			};

			ThreadHelpers.InvokeThreads(10, () => taskMultiplexer.Get(1, valueFactory).Wait());

			Assert.AreEqual(1, valueFactoryInvocationCount);
		}

		[TestMethod]
		public void Get_SlowSyncValueFactory_InvokesOnce()
		{
			var taskMultiplexer = new TaskMultiplexer<int, string>();
			int valueFactoryInvocationCount = 0;
			Func<int, Task<string>> valueFactory = x =>
			{
				Thread.Sleep(500);
				Interlocked.Increment(ref valueFactoryInvocationCount);
				return Task.FromResult(x.ToString());
			};

			ThreadHelpers.InvokeThreads(10, () => taskMultiplexer.Get(1, valueFactory).Wait());

			Assert.AreEqual(1, valueFactoryInvocationCount);
		}

		[TestMethod]
		public void GetBatch_SlowAsyncValueFactory_InvokesMinimum()
		{
			var taskMultiplexer = new TaskMultiplexer<int, string>();
			int valueFactoryInvocationCount = 0;
			Func<ICollection<int>, Task<IDictionary<int, string>>> valueFactory = async keys =>
			{
				await Task.Delay(500).ConfigureAwait(false);
				Interlocked.Increment(ref valueFactoryInvocationCount);
				var values = keys
					.Distinct()
					.ToDictionary(k => k, k => k.ToString());
				return values;
			};

			int threadIndex = -1;
			ThreadHelpers.InvokeThreads(10, () =>
			{
				// *[0,5], *[1,6], *[2,7], *[3,8], *[4,9], [0,5], [1,6], [2,7], [3,8], [4,9]
				var key1 = Interlocked.Increment(ref threadIndex) % 5;
				var key2 = key1 + 5;
				var keys = new[] { key1, key2 };
				taskMultiplexer.Get(keys, valueFactory).Wait();
			});

			Assert.AreEqual(5, valueFactoryInvocationCount);
		}
	}
}
