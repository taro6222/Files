// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UserControls
{
	public sealed partial class BottomCommandBar : UserControl
	{
		private readonly StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
		private readonly IAppSettingsService settings = Ioc.Default.GetRequiredService<IAppSettingsService>();
		private readonly IActionsSettingsService actionsSettings = Ioc.Default.GetRequiredService<IActionsSettingsService>();

		public BottomCommandBar()
		{
			Content = new ScrollViewer
			{
				Content = buttons,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollMode = ScrollMode.Enabled,
				VerticalScrollMode = ScrollMode.Disabled,
				VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
			};
			Loaded += (_, _) => { settings.PropertyChanged += SettingsChanged; actionsSettings.PropertyChanged += ActionsChanged; Rebuild(); };
			Unloaded += (_, _) => { settings.PropertyChanged -= SettingsChanged; actionsSettings.PropertyChanged -= ActionsChanged; };
		}

		private void ActionsChanged(object? sender, PropertyChangedEventArgs e) => Rebuild();

		private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(IAppSettingsService.BottomCommandBar))
				Rebuild();
		}

		private void Rebuild()
		{
			buttons.Children.Clear();
			var commands = Ioc.Default.GetRequiredService<ICommandManager>();
			foreach (var row in BottomCommandBarConfiguration.Load().Where(row => row.Visible))
			{
				var command = commands[row.Code];
				var label = BottomCommandBarConfiguration.EffectiveKey(row, commands).IsNone ? command.Label : $"{row.Key} {command.Label}";
				var button = new Button { Content = label, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 2, 0, 4) };
				AutomationProperties.SetName(button, label);
				button.Click += async (_, _) =>
				{
					// Activate this bar's pane before resolving its selection-dependent command.
					if (this.FindAscendant<ModernShellPage>() is { PaneHolder: ShellPanesPage holder } pane)
						holder.ActivePane = pane;
					if (command.IsExecutable)
						await command.ExecuteAsync();
				};
				buttons.Children.Add(button);
			}
			var configure = new Button { Content = new FontIcon { Glyph = "\uE713", FontSize = 14 }, Padding = new Thickness(8, 4, 8, 4) };
			AutomationProperties.SetName(configure, Strings.BottomBarSettings.GetLocalizedResource());
			ToolTipService.SetToolTip(configure, Strings.BottomBarSettings.GetLocalizedResource());
			configure.Click += async (_, _) => await new BottomCommandBarSettingsDialog { XamlRoot = XamlRoot }.TryShowAsync();
			buttons.Children.Add(configure);
		}
	}
}