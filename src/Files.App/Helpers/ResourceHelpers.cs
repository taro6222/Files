// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Markup;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Files.App.Helpers
{
	[MarkupExtensionReturnType(ReturnType = typeof(string))]
	public sealed partial class ResourceString : MarkupExtension
	{
		private static readonly ResourceLoader resourceLoader = new();

		public string Name { get; set; } = string.Empty;

		protected override object ProvideValue() => AppEnvironmentInfo.IsPortable
			? PortableResourceHelper.TryGetString(Name) ?? string.Empty
			: resourceLoader.GetString(Name);
	}
}
