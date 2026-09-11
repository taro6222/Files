// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UserControls
{
	public sealed partial class BottomCommandBarSettingsDialog : ContentDialog
	{
		private readonly StackPanel rowsPanel = new() { Spacing = 8 };
		private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap };
		private readonly List<(CheckBox Visible, ComboBox Action, TextBox Key)> editors = [];
		private List<BottomCommandEntry> rows = BottomCommandBarConfiguration.Load();

		public BottomCommandBarSettingsDialog()
		{
			Title = Strings.BottomBarSettings.GetLocalizedResource();
			PrimaryButtonText = Strings.Save.GetLocalizedResource();
			CloseButtonText = Strings.Cancel.GetLocalizedResource();
			SecondaryButtonText = Strings.BottomRestoreDefaults.GetLocalizedResource();
			var panel = new StackPanel { Spacing = 12, MinWidth = 420 };
			panel.Children.Add(new TextBlock { Text = Strings.BottomBarHelp.GetLocalizedResource(), TextWrapping = TextWrapping.Wrap });
			panel.Children.Add(rowsPanel);
			panel.Children.Add(error);
			Content = new ScrollViewer { Content = panel, MaxHeight = 560 };
			PrimaryButtonClick += SaveClicked;
			SecondaryButtonClick += (_, args) => { args.Cancel = true; rows = BottomCommandBarConfiguration.Defaults(); Rebuild(); };
			Rebuild();
		}

		private void ReadRows() => rows = editors.Select(editor => new BottomCommandEntry(
			editor.Visible.IsChecked == true, BottomCommandBarConfiguration.Actions[editor.Action.SelectedIndex], editor.Key.Text.Trim())).ToList();

		private void Rebuild()
		{
			editors.Clear();
			rowsPanel.Children.Clear();
			error.Text = string.Empty;
			var commands = Ioc.Default.GetRequiredService<ICommandManager>();
			for (var i = 0; i < rows.Count; i++)
			{
				var index = i;
				var row = rows[i];
				var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
				var visible = new CheckBox { IsChecked = row.Visible, MinWidth = 32 };
				AutomationProperties.SetName(visible, Strings.BottomVisible.GetLocalizedResource());
				var action = new ComboBox { Width = 160, SelectedIndex = Array.IndexOf(BottomCommandBarConfiguration.Actions, row.Code) };
				foreach (var code in BottomCommandBarConfiguration.Actions)
					action.Items.Add(commands[code].Label);
				action.SelectedIndex = Array.IndexOf(BottomCommandBarConfiguration.Actions, row.Code);
				AutomationProperties.SetName(action, Strings.BottomAction.GetLocalizedResource());
				var key = new TextBox { Text = row.Key, Width = 105, PlaceholderText = "Ctrl+F9" };
				AutomationProperties.SetName(key, Strings.BottomHotkey.GetLocalizedResource());
				var up = new Button { Content = "↑", IsEnabled = i > 0, Padding = new Thickness(8, 4, 8, 4) };
				var down = new Button { Content = "↓", IsEnabled = i < rows.Count - 1, Padding = new Thickness(8, 4, 8, 4) };
				AutomationProperties.SetName(up, Strings.BottomMoveUp.GetLocalizedResource());
				AutomationProperties.SetName(down, Strings.BottomMoveDown.GetLocalizedResource());
				up.Click += (_, _) => Move(index, -1);
				down.Click += (_, _) => Move(index, 1);
				panel.Children.Add(visible);
				panel.Children.Add(action);
				panel.Children.Add(key);
				panel.Children.Add(up);
				panel.Children.Add(down);
				rowsPanel.Children.Add(panel);
				editors.Add((visible, action, key));
			}
		}

		private void Move(int index, int offset)
		{
			ReadRows();
			(rows[index], rows[index + offset]) = (rows[index + offset], rows[index]);
			Rebuild();
		}

		private void SaveClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		{
			ReadRows();
			var used = new HashSet<(Keys, KeyModifiers)>();
			var commands = Ioc.Default.GetRequiredService<ICommandManager>();
			foreach (var row in rows)
			{
				if (!BottomCommandBarConfiguration.TryParseKey(row.Key, out var key))
				{
					error.Text = Strings.BottomInvalidHotkey.GetLocalizedResource();
					args.Cancel = true;
					return;
				}
				if (key.IsNone)
					continue;
				if (!used.Add((key.Key, key.Modifier)) || BottomCommandBarConfiguration.HasConflict(key, row.Code, commands, rows))
				{
					error.Text = Strings.BottomDuplicateHotkey.GetLocalizedFormatResource(row.Key);
					args.Cancel = true;
					return;
				}
			}
			BottomCommandBarConfiguration.Save(rows);
		}
	}
}