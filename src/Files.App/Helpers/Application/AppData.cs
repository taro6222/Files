// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Files.App.Helpers
{
	public sealed class AppData
	{
		private readonly ApplicationData? _packagedData;
		private readonly Lazy<StorageFolder> _localFolder;
		private readonly Lazy<StorageFolder> _roamingFolder;
		private readonly Lazy<StorageFolder> _temporaryFolder;
		private readonly Lazy<StorageFolder> _localCacheFolder;

		public static AppData Current { get; } = new();

		public StorageFolder LocalFolder => _localFolder.Value;
		public StorageFolder RoamingFolder => _roamingFolder.Value;
		public StorageFolder TemporaryFolder => _temporaryFolder.Value;
		public StorageFolder LocalCacheFolder => _localCacheFolder.Value;
		public AppDataContainer LocalSettings { get; }

		private AppData()
		{
			if (!AppEnvironmentInfo.IsPortable)
				_packagedData = ApplicationData.Current;

			_localFolder = new(() => _packagedData?.LocalFolder ?? GetFolder("LocalState"));
			_roamingFolder = new(() => _packagedData?.RoamingFolder ?? GetFolder("RoamingState"));
			_temporaryFolder = new(() => _packagedData?.TemporaryFolder ?? GetFolder("TempState"));
			_localCacheFolder = new(() => _packagedData?.LocalCacheFolder ?? GetFolder("LocalCache"));
			LocalSettings = _packagedData is not null
				? new(_packagedData.LocalSettings)
				: new(Path.Combine(AppEnvironmentInfo.DataDirectory, "Settings"));
		}

		private static StorageFolder GetFolder(string name)
		{
			var path = Path.Combine(AppEnvironmentInfo.DataDirectory, name);
			Directory.CreateDirectory(path);
			return StorageFolder.GetFolderFromPathAsync(path).AsTask().GetAwaiter().GetResult();
		}
	}

	public sealed class AppDataContainer
	{
		private readonly ApplicationDataContainer? _packagedContainer;
		private readonly string? _directory;

		public IPropertySet Values { get; }

		public IReadOnlyDictionary<string, AppDataContainer> Containers
		{
			get
			{
				if (_packagedContainer is not null)
					return _packagedContainer.Containers.ToDictionary(pair => pair.Key, pair => new AppDataContainer(pair.Value));

				var result = new Dictionary<string, AppDataContainer>(StringComparer.Ordinal);
				foreach (var directory in Directory.EnumerateDirectories(_directory!, "container-*"))
				{
					var name = Encoding.UTF8.GetString(Convert.FromHexString(Path.GetFileName(directory)[10..]));
					result.Add(name, new(directory));
				}
				return result;
			}
		}

		internal AppDataContainer(ApplicationDataContainer container)
		{
			_packagedContainer = container;
			Values = container.Values;
		}

		internal AppDataContainer(string directory)
		{
			_directory = directory;
			Directory.CreateDirectory(directory);
			Values = new PortableSettings(Path.Combine(directory, "values.json"));
		}

		public AppDataContainer CreateContainer(string name, ApplicationDataCreateDisposition disposition)
		{
			if (_packagedContainer is not null)
				return new(_packagedContainer.CreateContainer(name, disposition));

			var path = GetContainerPath(name);
			if (disposition == ApplicationDataCreateDisposition.Existing && !Directory.Exists(path))
				throw new DirectoryNotFoundException(path);
			return new(path);
		}

		public void DeleteContainer(string name)
		{
			if (_packagedContainer is not null)
			{
				_packagedContainer.DeleteContainer(name);
				return;
			}

			var path = GetContainerPath(name);
			if (Directory.Exists(path))
				Directory.Delete(path, true);
		}

		private string GetContainerPath(string name)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(name);
			if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
				throw new ArgumentException("Invalid settings container name.", nameof(name));

			var parent = Path.GetFullPath(_directory!);
			var path = Path.GetFullPath(Path.Combine(parent, "container-" + Convert.ToHexString(Encoding.UTF8.GetBytes(name))));
			if (!string.Equals(Path.GetDirectoryName(path), parent, StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException("Invalid settings container path.", nameof(name));
			return path;
		}
	}

	internal sealed partial class PortableSettings : IPropertySet
	{
		private readonly string _path;
		private readonly string _mutexName;

		public event MapChangedEventHandler<string, object>? MapChanged;

		internal PortableSettings(string path)
		{
			_path = path;
			_mutexName = @"Local\Files.Portable.Settings." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
		}

		public object this[string key]
		{
			get => Read(values => values[key]);
			set => Change(values =>
			{
				var change = values.ContainsKey(key) ? CollectionChange.ItemChanged : CollectionChange.ItemInserted;
				values[key] = value;
				return change;
			}, key);
		}

		public ICollection<string> Keys => Read(values => values.Keys.ToArray());
		public ICollection<object> Values => Read(values => values.Values.ToArray());
		public int Count => Read(values => values.Count);
		public bool IsReadOnly => false;

		public void Add(string key, object value) => Change(values =>
		{
			values.Add(key, value);
			return CollectionChange.ItemInserted;
		}, key);

		public void Add(KeyValuePair<string, object> item) => Add(item.Key, item.Value);
		public bool ContainsKey(string key) => Read(values => values.ContainsKey(key));
		public bool Contains(KeyValuePair<string, object> item) => Read(values => ((ICollection<KeyValuePair<string, object>>)values).Contains(item));
		public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => Read(values => values.ToArray()).CopyTo(array, arrayIndex);
		public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Read(values => values).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public bool TryGetValue(string key, out object value)
		{
			var values = Read(values => values);
			return values.TryGetValue(key, out value!);
		}

		public bool Remove(string key) => Change(values => values.Remove(key) ? CollectionChange.ItemRemoved : null, key);
		public bool Remove(KeyValuePair<string, object> item) => Change(values => ((ICollection<KeyValuePair<string, object>>)values).Remove(item) ? CollectionChange.ItemRemoved : null, item.Key);
		public void Clear() => Change(values =>
		{
			values.Clear();
			return CollectionChange.Reset;
		}, string.Empty);

		private T WithLock<T>(Func<T> action)
		{
			using var mutex = new Mutex(false, _mutexName);
			try
			{
				mutex.WaitOne();
			}
			catch (AbandonedMutexException)
			{
				// The abandoned mutex is acquired; atomic replacement leaves a complete file.
			}
			try
			{
				return action();
			}
			finally
			{
				mutex.ReleaseMutex();
			}
		}

		private T Read<T>(Func<Dictionary<string, object>, T> read) => WithLock(() => read(Load()));

		private bool Change(Func<Dictionary<string, object>, CollectionChange?> change, string key)
		{
			var result = WithLock(() =>
			{
				// Reload under the shared mutex so another process's unrelated writes survive.
				var values = Load();
				var result = change(values);
				if (result is not null)
					Save(values);
				return result;
			});
			if (result is not null)
				MapChanged?.Invoke(this, new SettingsChangedEventArgs(result.Value, key));
			return result is not null;
		}

		private Dictionary<string, object> Load()
		{
			var values = new Dictionary<string, object>(StringComparer.Ordinal);
			if (!File.Exists(_path))
				return values;

			using var stream = File.OpenRead(_path);
			using var document = JsonDocument.Parse(stream);
			foreach (var property in document.RootElement.EnumerateObject())
			{
				var kind = property.Value.GetProperty("kind").GetString();
				var text = property.Value.GetProperty("value").GetString()!;
				object value = kind switch
				{
					"string" => text,
					"bool" => bool.Parse(text),
					"byte" => byte.Parse(text, CultureInfo.InvariantCulture),
					"sbyte" => sbyte.Parse(text, CultureInfo.InvariantCulture),
					"short" => short.Parse(text, CultureInfo.InvariantCulture),
					"ushort" => ushort.Parse(text, CultureInfo.InvariantCulture),
					"int" => int.Parse(text, CultureInfo.InvariantCulture),
					"uint" => uint.Parse(text, CultureInfo.InvariantCulture),
					"long" => long.Parse(text, CultureInfo.InvariantCulture),
					"ulong" => ulong.Parse(text, CultureInfo.InvariantCulture),
					"float" => float.Parse(text, CultureInfo.InvariantCulture),
					"double" => double.Parse(text, CultureInfo.InvariantCulture),
					"char" => char.Parse(text),
					"guid" => Guid.Parse(text),
					"datetimeoffset" => DateTimeOffset.ParseExact(text, "O", CultureInfo.InvariantCulture),
					"timespan" => TimeSpan.ParseExact(text, "c", CultureInfo.InvariantCulture),
					"bytes" => Convert.FromBase64String(text),
					_ => throw new InvalidDataException($"Unsupported portable setting type: {kind}")
				};
				values.Add(property.Name, value);
			}
			return values;
		}

		private void Save(Dictionary<string, object> values)
		{
			var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					using (var writer = new Utf8JsonWriter(stream))
					{
						writer.WriteStartObject();
						foreach (var pair in values)
						{
							var kind = pair.Value switch
							{
								string => "string", bool => "bool", byte => "byte", sbyte => "sbyte",
								short => "short", ushort => "ushort", int => "int", uint => "uint",
								long => "long", ulong => "ulong", float => "float", double => "double",
								char => "char", Guid => "guid", DateTimeOffset => "datetimeoffset",
								TimeSpan => "timespan", byte[] => "bytes",
								_ => throw new ArgumentException($"Unsupported portable setting value for {pair.Key}.")
							};
							var text = pair.Value switch
							{
								DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
								TimeSpan time => time.ToString("c", CultureInfo.InvariantCulture),
								byte[] bytes => Convert.ToBase64String(bytes),
								IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
								_ => pair.Value.ToString()
							};
							writer.WriteStartObject(pair.Key);
							writer.WriteString("kind", kind);
							writer.WriteString("value", text);
							writer.WriteEndObject();
						}
						writer.WriteEndObject();
					}
					stream.Flush(true);
				}
				File.Move(temporaryPath, _path, true);
			}
			finally
			{
				File.Delete(temporaryPath);
			}
		}

		private sealed partial class SettingsChangedEventArgs(CollectionChange collectionChange, string key) : IMapChangedEventArgs<string>
		{
			public CollectionChange CollectionChange { get; } = collectionChange;
			public string Key { get; } = key;
		}
	}
}
