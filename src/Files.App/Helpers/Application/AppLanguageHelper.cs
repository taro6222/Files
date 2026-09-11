// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Globalization;
using System.IO;
using Windows.Globalization;

namespace Files.App.Helpers
{
	/// <summary>
	/// Provides static helper to manage supported languages in the application.
	/// </summary>
	public static class AppLanguageHelper
	{
		/// <summary>
		/// A constant string representing the default language code.
		/// It is initialized as an empty string.
		/// </summary>
		private static readonly string _defaultCode = string.Empty;

		/// <summary>
		/// A collection of available languages.
		/// </summary>
		public static ObservableCollection<AppLanguageItem> SupportedLanguages { get; }

		/// <summary>
		/// Gets the preferred language.
		/// </summary>
		public static AppLanguageItem PreferredLanguage { get; private set; }

		/// <summary>
		/// Gets the preferred language.
		/// </summary>
		public static bool IsPreferredLanguageRtl
		{
			get
			{
				if (PreferredLanguage.Code is null)
					return false;

				var culture = new CultureInfo(PreferredLanguage.Code);
				return culture.TextInfo.IsRightToLeft;
			}
		}

		/// <summary>
		/// Initializes the <see cref="AppLanguageHelper"/> class.
		/// </summary>
		static AppLanguageHelper()
		{
			// Populate the Languages collection with available languages
			var appLanguages = GetSupportedLanguageCodes()
			   .Append(string.Empty) // Add default language code
			   .Select(language => new AppLanguageItem(language))
			   .OrderBy(language => language.Code is not "") // Default language on top
			   .ThenBy(language => language.Name)
			   .ToList();

			// Get the current primary language override.
			var current = new AppLanguageItem(AppEnvironmentInfo.IsPortable ? PortableResourceHelper.LanguageOverride : ApplicationLanguages.PrimaryLanguageOverride);

			// Find the index of the saved language
			var index = appLanguages.IndexOf(appLanguages.FirstOrDefault(dl => dl.Name == current.Name) ?? appLanguages.First());

			// Set the system default language as the first item in the Languages collection
			var systemLanguage = new AppLanguageItem(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ko" ? "ko-KR" : "en-US", systemDefault: true);
			if (appLanguages.Any(lang => lang.Name.Contains(systemLanguage.Name)))
				appLanguages[0] = systemLanguage;
			else
				appLanguages[0] = new("en-US", systemDefault: true);

			// Initialize the list
			SupportedLanguages = new(appLanguages);
			PreferredLanguage = SupportedLanguages[index];
		}

		/// <summary>
		/// Attempts to change the preferred language code by index.
		/// </summary>
		/// <param name="index">The index of the new language.</param>
		/// <returns>True if the language was successfully changed; otherwise, false.</returns>
		public static bool TryChange(int index)
		{
			if (index < 0 || index >= SupportedLanguages.Count || PreferredLanguage == SupportedLanguages[index])
				return false;

			PreferredLanguage = SupportedLanguages[index];

			// Update the primary language override
			SaveLanguageOverride(index == 0 ? _defaultCode : PreferredLanguage.Code);
			return true;
		}

		/// <summary>
		/// Attempts to change the preferred language code by code.
		/// </summary>
		/// <param name="code">The code of the new language.</param>
		/// <returns>True if the language was successfully changed; otherwise, false.</returns>
		public static bool TryChange(string code)
		{
			if (AppEnvironmentInfo.IsPortable)
			{
				var portableIndex = string.IsNullOrEmpty(code) ? 0 : SupportedLanguages.ToList().FindIndex(1, language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));
				return TryChange(portableIndex);
			}

			var lang = new AppLanguageItem(code);
			var find = SupportedLanguages.FirstOrDefault(dl => dl.Name == lang.Name);
			if (find is null)
				return false;

			var index = SupportedLanguages
				.Skip(1) // Skip first (default) language
				.ToList()
				.IndexOf(find ?? SupportedLanguages.First());

			// Adjusts the index to match the correct index
			index = index == 0 ? index : index + 1;

			if (PreferredLanguage == SupportedLanguages[index])
				return false;

			PreferredLanguage = SupportedLanguages[index];

			// Update the primary language override
			SaveLanguageOverride(index == 0 ? _defaultCode : PreferredLanguage.Code);
			return true;
		}

		private static IEnumerable<string> GetSupportedLanguageCodes()
		{
			if (!AppEnvironmentInfo.IsPortable)
				return ApplicationLanguages.ManifestLanguages.Where(code => code is "en-US" or "ko-KR");

			return File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "SupportedLanguages.txt"))
				.Where(code => code is "en-US" or "ko-KR")
				.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		private static void SaveLanguageOverride(string code)
		{
			if (AppEnvironmentInfo.IsPortable)
				AppData.Current.LocalSettings.Values[PortableResourceHelper.LanguageOverrideKey] = code;
			else
				ApplicationLanguages.PrimaryLanguageOverride = code;
		}
	}
}
