// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.Views.Settings
{
	public sealed partial class GeneralPage : Page
	{

		public GeneralPage()
		{
			InitializeComponent();
		}

		private async void ConfigureBottomBar_Click(object sender, RoutedEventArgs e)
			=> await new BottomCommandBarSettingsDialog { XamlRoot = XamlRoot }.TryShowAsync();

		private void RemoveStartupPage(object sender, RoutedEventArgs e)
		{
			if (sender is FrameworkElement { DataContext: PageOnStartupViewModel page })
				ViewModel.RemovePageCommand.Execute(page);
		}
	}
}
