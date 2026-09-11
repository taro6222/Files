// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Shared.Helpers;

namespace Files.App.Actions
{
	[GeneratedRichCommand]
	internal sealed partial class BottomViewAction : ObservableObject, IAction
	{
		private readonly IContentPageContext context = Ioc.Default.GetRequiredService<IContentPageContext>();
		public string Label => Strings.BottomView.GetLocalizedResource();
		public string Description => Label;
		public ActionCategory Category => ActionCategory.Open;
		public bool IsExecutable => context.HasSelection;
		public BottomViewAction() => context.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsExecutable));
		public Task ExecuteAsync(object? parameter = null)
		{
			Ioc.Default.GetRequiredService<IInfoPaneSettingsService>().SelectedTab = InfoPaneTabs.Preview;
			Ioc.Default.GetRequiredService<InfoPaneViewModel>().IsEnabled = true;
			return Task.CompletedTask;
		}
	}

	[GeneratedRichCommand]
	internal sealed partial class BottomEditAction : ObservableObject, IAction
	{
		private readonly IContentPageContext context = Ioc.Default.GetRequiredService<IContentPageContext>();
		public string Label => Strings.BottomEdit.GetLocalizedResource();
		public string Description => Label;
		public ActionCategory Category => ActionCategory.Open;
		public bool IsExecutable => context.HasSelection && context.SelectedItems.All(item => !item.IsFolder) &&
			context.PageType != ContentPageTypes.RecycleBin && context.PageType != ContentPageTypes.ZipFolder;
		public BottomEditAction() => context.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsExecutable));
		public Task ExecuteAsync(object? parameter = null) => Task.WhenAll(context.SelectedItems.Select(item =>
			Win32Helper.RunPowershellCommandAsync($"& 'notepad.exe' {Win32Helper.ToPowerShellStringLiteral(item.ItemPath)}", PowerShellExecutionOptions.Hidden)));
	}

	[GeneratedRichCommand]
	internal sealed partial class BottomCloseAction : IAction
	{
		public string Label => Strings.BottomClose.GetLocalizedResource();
		public string Description => Label;
		public ActionCategory Category => ActionCategory.Navigation;
		public Task ExecuteAsync(object? parameter = null)
		{
			MainWindow.Instance.Close();
			return Task.CompletedTask;
		}
	}
}