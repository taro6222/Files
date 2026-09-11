// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.ApplicationModel;

namespace Files.App.Helpers;

public static class AppEnvironmentInfo
{
	private static readonly Package? package = GetPackage();
	public static bool IsPortable => package is null;
	public static string InstallDirectory => package?.EffectivePath ?? AppContext.BaseDirectory;
	public static string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");
	public static string Name => package?.Id.Name ?? "FilesPortable";
	public static string DisplayName => package?.DisplayName ?? "Files";
	public static string Identity => package?.Id.FamilyName ?? "FilesPortable";
	public static string InstanceKey { get; } = IsPortable
		? "Files-Portable-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(AppContext.BaseDirectory).ToUpperInvariant())))
		: $"Files-{AppLifecycleHelper.AppEnvironment}-Instance";
	public static PackageVersion Version => package?.Id.Version ?? new PackageVersion(4, 2, 32, 0);

	private static Package? GetPackage()
	{
		try
		{
			return Package.Current;
		}
		catch (Exception ex) when (ex.HResult == unchecked((int)0x80073D54))
		{
			return null;
		}
	}
}