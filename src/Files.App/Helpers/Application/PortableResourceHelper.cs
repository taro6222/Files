// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Windows.ApplicationModel.Resources;

namespace Files.App.Helpers
{
	internal static class PortableResourceHelper
	{
		internal const string LanguageOverrideKey = "PrimaryLanguageOverride";

		private static readonly ResourceManager _resourceManager = new();
		private static readonly ResourceMap _resources = _resourceManager.MainResourceMap.GetSubtree("Resources");

		internal static string LanguageOverride { get; } = AppData.Current.LocalSettings.Values.TryGetValue(LanguageOverrideKey, out var value)
			? NormalizeLanguage(value as string)
			: string.Empty;

		private static readonly ResourceContext _context = CreateContext();

		internal static string? TryGetString(string name) => _resources.TryGetValue(name, _context)?.ValueAsString;

		private static string NormalizeLanguage(string? code)
			=> code?.Split('-')[0].ToLowerInvariant() switch
			{
				"ko" => "ko-KR",
				"en" => "en-US",
				_ => string.Empty,
			};

		private static ResourceContext CreateContext()
		{
			var context = _resourceManager.CreateResourceContext();
			// Language changes take effect after restart, matching the settings UI.
			context.QualifierValues["Language"] = !string.IsNullOrEmpty(LanguageOverride)
				? LanguageOverride
				: System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ko" ? "ko-KR" : "en-US";
			return context;
		}
	}
}
