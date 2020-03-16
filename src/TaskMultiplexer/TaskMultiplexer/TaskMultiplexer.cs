using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TaskMultiplexing
{
	public class TaskMultiplexer<TKey, T>
	{
		private ConcurrentDictionary<TKey, Lazy<Task<IDictionary<TKey, T>>>> Tasks { get; } = new ConcurrentDictionary<TKey, Lazy<Task<IDictionary<TKey, T>>>>();

		public async Task<T> GetMultiplexed(TKey key, Func<TKey, Task<T>> valueFactory)
		{
			var valuesTask = Tasks.GetOrAdd(key, k => CreateLazyValuesTask(k, valueFactory));
			var values = await valuesTask.Value.ConfigureAwait(false);
			values.TryGetValue(key, out var value);
			return value;
		}

		public async Task<IDictionary<TKey, T>> GetMultiplexed(IEnumerable<TKey> keys, Func<ICollection<TKey>, Task<IDictionary<TKey, T>>> valueFactory)
		{
			var keysList = keys.ToList();
			var values = new Dictionary<TKey, T>(keysList.Count);

			if (keysList.Any())
			{
				var newKeysTaskCompletionSource = new TaskCompletionSource<ICollection<TKey>>();
				var newValuesTask = CreateLazyValuesTask(newKeysTaskCompletionSource, valueFactory);

				List<TKey> newKeys = null;
				IDictionary<TKey, Lazy<Task<IDictionary<TKey, T>>>> existingValuesTasks = null;

				foreach (var key in keysList)
				{
					var valuesTask = Tasks.GetOrAdd(key, newValuesTask);
					if (ReferenceEquals(valuesTask, newValuesTask))
					{
						if (newKeys == null)
						{
							newKeys = new List<TKey>();
						}
						newKeys.Add(key);
					}
					else
					{
						if (existingValuesTasks == null)
						{
							existingValuesTasks = new Dictionary<TKey, Lazy<Task<IDictionary<TKey, T>>>>();
						}
						existingValuesTasks[key] = valuesTask;
					}
				}

				if (newKeys != null)
				{
					newKeysTaskCompletionSource.SetResult(newKeys);
					var newValues = await newValuesTask.Value.ConfigureAwait(false);
					foreach (var valuePair in newValues)
					{
						values[valuePair.Key] = valuePair.Value;
					}
				}

				if (existingValuesTasks != null)
				{
					foreach (var valuesTaskPair in existingValuesTasks)
					{
						if ((await valuesTaskPair.Value.Value.ConfigureAwait(false)).TryGetValue(valuesTaskPair.Key, out var value))
						{
							values[valuesTaskPair.Key] = value;
						}
					}
				}
			}

			return values;
		}

		private Lazy<Task<IDictionary<TKey, T>>> CreateLazyValuesTask(TKey key, Func<TKey, Task<T>> valueFactory)
		{
			return new Lazy<Task<IDictionary<TKey, T>>>(() => Task.Run(async () =>
			{
				try
				{
					var value = await valueFactory(key).ConfigureAwait(false);
					return (IDictionary<TKey, T>)new Dictionary<TKey, T>
					{
						[key] = value
					};
				}
				finally
				{
					Remove(key);
				}
			}));
		}

		private Lazy<Task<IDictionary<TKey, T>>> CreateLazyValuesTask(TaskCompletionSource<ICollection<TKey>> keysTaskCompletionSource, Func<ICollection<TKey>, Task<IDictionary<TKey, T>>> valueFactory)
		{
			return new Lazy<Task<IDictionary<TKey, T>>>(() => Task.Run(async () =>
			{
				var keys = await keysTaskCompletionSource.Task.ConfigureAwait(false);

				try
				{
					var values = await valueFactory(keys).ConfigureAwait(false);
					return values;
				}
				finally
				{
					Remove(keys);
				}
			}));
		}

		private void Remove(TKey key)
		{
			Tasks.TryRemove(key, out var _);
		}

		private void Remove(IEnumerable<TKey> keys)
		{
			foreach (var key in keys)
			{
				Remove(key);
			}
		}
	}
}
