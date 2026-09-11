// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Actions
{
	internal abstract class BottomTransferAction : ObservableObject, IAction
	{
		protected readonly IContentPageContext Context = Ioc.Default.GetRequiredService<IContentPageContext>();
		protected abstract bool Move { get; }
		public string Label => (Move ? Strings.BottomMove : Strings.BottomCopy).GetLocalizedResource();
		public string Description => Label;
		public ActionCategory Category => ActionCategory.FileSystem;
		public bool IsExecutable => Context.HasSelection && Context.ShellPage is not null &&
			Context.PageType != ContentPageTypes.RecycleBin && Context.PageType != ContentPageTypes.ZipFolder;
		protected BottomTransferAction() => Context.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsExecutable));

		public async Task ExecuteAsync(object? parameter = null)
		{
			if (!IsExecutable || Context.ShellPage is not { } source)
				return;
			var items = Context.SelectedItems.Select(item => StorageHelpers.FromPathAndType(
				item.GetRequiredPath(), item.IsFolder ? FilesystemItemType.Directory : FilesystemItemType.File)).ToList();
			var otherPane = source.PaneHolder?.IsMultiPaneActive == true
				? source.PaneHolder.GetPanes().FirstOrDefault(pane => !ReferenceEquals(pane, source.PaneHolder.ActivePane)) : null;
			IShellPage? other = otherPane is { IsColumnView: true, SlimContentPage: ColumnsLayoutPage columns }
				? columns.ActiveColumnShellPage : otherPane;
			string? destination = other?.ShellViewModel?.WorkingDirectory;
			if (otherPane is null)
			{
				if (!Ioc.Default.GetRequiredService<ICommonDialogService>().Open_FileOpenDialog(
					MainWindow.Instance.WindowHandle, true, [], Environment.SpecialFolder.Desktop, out destination))
					return;
			}
			if (string.IsNullOrWhiteSpace(destination) || !SystemIO.Directory.Exists(destination))
				return;
			var paths = items.Select(item => PathNormalization.Combine(destination, item.Name)).ToList();
			var result = Move
				? await source.FilesystemHelpers.MoveItemsAsync(items, paths, true, true)
				: await source.FilesystemHelpers.CopyItemsAsync(items, paths, true, true);
			if (result == ReturnResult.Cancelled)
				return;
			await source.RefreshIfNoWatcherExistsAsync();
			if (other is not null)
				await other.RefreshIfNoWatcherExistsAsync();
		}
	}

	[GeneratedRichCommand]
	internal sealed partial class BottomCopyAction : BottomTransferAction
	{
		protected override bool Move => false;
	}

	[GeneratedRichCommand]
	internal sealed partial class BottomMoveAction : BottomTransferAction
	{
		protected override bool Move => true;
	}
}