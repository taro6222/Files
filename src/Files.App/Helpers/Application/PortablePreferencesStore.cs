// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

namespace Files.App.Helpers
{
	internal sealed class PortablePreferencesStore<T>(
		string fileName,
		JsonTypeInfo<List<T>> typeInfo,
		Func<T, string> getPath,
		Func<T, ulong?> getFrn,
		Action<T, string?, ulong?> updateIdentity) where T : class
	{
		public List<T> Read() => Execute(ReadCore);

		public void Replace(IEnumerable<T> items)
		{
			Execute(() =>
			{
				var records = new List<T>();
				foreach (var item in items)
				{
					RemovePath(records, getPath(item));
					records.Add(item);
				}
				SaveCore(records);
				return true;
			});
		}

		public T? Find(string? path, ulong? frn)
		{
			return Execute(() =>
			{
				var records = ReadCore();
				var record = FindCore(records, path, frn);
				if (record is not null &&
					((path is not null && !string.Equals(getPath(record), path, StringComparison.Ordinal)) ||
					(frn is not null && getFrn(record) != frn)))
				{
					updateIdentity(record, path, frn);
					SaveCore(records);
				}
				return record;
			});
		}

		public void Set(string path, ulong? frn, T? record, bool preserveFrn = false)
		{
			Execute(() =>
			{
				var records = ReadCore();
				if (preserveFrn && record is not null && frn is null)
				{
					var existing = FindCore(records, path, frn);
					if (existing is not null)
						updateIdentity(record, path, getFrn(existing));
				}
				if (record is null)
				{
					var existing = FindCore(records, path, frn);
					if (existing is not null)
						records.Remove(existing);
				}
				RemovePath(records, path);
				if (record is not null)
					records.Add(record);
				SaveCore(records);
				return true;
			});
		}

		public void Update(string? oldPath, ulong? oldFrn, string? newPath, ulong? newFrn)
		{
			Execute(() =>
			{
				var records = ReadCore();
				var record = FindCore(records, oldPath, oldFrn);
				if (record is not null)
				{
					records.Remove(record);
					updateIdentity(record, newPath, newFrn);
					RemovePath(records, getPath(record));
					records.Add(record);
					SaveCore(records);
				}
				return true;
			});
		}

		private T? FindCore(List<T> records, string? path, ulong? frn) =>
			(path is null ? null : records.Find(item => PathsEqual(getPath(item), path))) ??
			(frn is null ? null : records.FindLast(item => getFrn(item) == frn));

		private void RemovePath(List<T> records, string path) =>
			records.RemoveAll(item => PathsEqual(getPath(item), path));

		internal static bool PathsEqual(string left, string right) =>
			string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

		internal static string NormalizePath(string path) => path.Replace('/', '\\').TrimEnd('\\');

		private List<T> ReadCore()
		{
			var path = Path.Combine(AppEnvironmentInfo.DataDirectory, fileName);
			return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), typeInfo) ?? [] : [];
		}

		private void SaveCore(List<T> records)
		{
			Directory.CreateDirectory(AppEnvironmentInfo.DataDirectory);
			var path = Path.Combine(AppEnvironmentInfo.DataDirectory, fileName);
			var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					JsonSerializer.Serialize(stream, records, typeInfo);
					stream.Flush(flushToDisk: true);
				}
				File.Move(temporaryPath, path, overwrite: true);
			}
			finally
			{
				if (File.Exists(temporaryPath))
					File.Delete(temporaryPath);
			}
		}

		private TResult Execute<TResult>(Func<TResult> action)
		{
			var path = Path.GetFullPath(Path.Combine(AppEnvironmentInfo.DataDirectory, fileName)).ToUpperInvariant();
			var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
			using var mutex = new Mutex(false, @"Local\Files.Portable.Preferences." + hash);
			try
			{
				mutex.WaitOne();
			}
			catch (AbandonedMutexException)
			{
				// The abandoned mutex is acquired; the last atomic save remains readable.
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
	}
}
