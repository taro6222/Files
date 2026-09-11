// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using Files.App.ViewModels.Settings;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using WinRT;

namespace Files.App.Views.Settings
{
	internal static class SettingsWindow
	{
		private static WindowEx? window;
		private static SettingsPage? page;

		public static XamlRoot? XamlRoot => window?.Content.XamlRoot;
		public static nint WindowHandle => window?.WindowHandle ?? MainWindow.Instance.WindowHandle;

		[DynamicWindowsRuntimeCast(typeof(OverlappedPresenter))]
		public static void Show(SettingsNavigationParams? parameters = null)
		{
			if (window is null)
				CreateWindow();
			if (parameters is not null)
			{
				page!.ViewModel.ClearSearch();
				page.NavigateTo(parameters);
			}
			if (window!.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
				presenter.Restore();
			window.AppWindow.Show();
			window.Activate();
		}

		[DynamicWindowsRuntimeCast(typeof(SettingsPage))]
		private static void CreateWindow()
		{
			var themeService = Ioc.Default.GetRequiredService<IAppThemeModeService>();
			var frame = new Frame();
			var searchBox = new AutoSuggestBox
			{
				PlaceholderText = Strings.SearchSettings.GetLocalizedResource(),
				QueryIcon = new SymbolIcon(Symbol.Find),
				Margin = new Thickness(16, 12, 16, 0),
				MaxWidth = 560,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			AutomationProperties.SetName(searchBox, Strings.SearchSettings.GetLocalizedResource());
			var content = new Grid { RequestedTheme = themeService.AppThemeMode };
			content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
			content.Children.Add(searchBox);
			Grid.SetRow(frame, 1);
			content.Children.Add(frame);
			window = new WindowEx(640, 480)
			{
				Content = content,
				SystemBackdrop = new AppSystemBackdrop(true),
			};
			window.AppWindow.Title = Strings.Settings.GetLocalizedResource();
			window.AppWindow.SetIcon(AppLifecycleHelper.AppIconPath);
			window.AppWindow.Resize(new SizeInt32(
				Convert.ToInt32(1000 * App.AppModel.AppWindowDPI),
				Convert.ToInt32(760 * App.AppModel.AppWindowDPI)));
			frame.Navigate(typeof(SettingsPage), null, new SuppressNavigationTransitionInfo());
			page = (SettingsPage)frame.Content;
			searchBox.TextChanged += (_, args) =>
			{
				if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
					page?.ApplySearchQuery(searchBox.Text);
			};
			page.ViewModel.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(SettingsPageViewModel.SearchQuery))
					searchBox.Text = page?.ViewModel.SearchQuery ?? string.Empty;
			};
			void ApplyTheme(object? sender, EventArgs args)
				=> themeService.SetAppThemeMode(window, window?.AppWindow.TitleBar, themeService.AppThemeMode, callThemeModeChangedEvent: false);
			themeService.AppThemeModeChanged += ApplyTheme;
			window.Closed += (_, _) =>
			{
				themeService.AppThemeModeChanged -= ApplyTheme;
				window = null;
				page = null;
			};
			ApplyTheme(null, EventArgs.Empty);
		}
	}
}