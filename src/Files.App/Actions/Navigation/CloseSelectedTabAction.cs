// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Actions
{
	[GeneratedRichCommand]
	internal sealed partial class CloseSelectedTabAction : CloseTabBaseAction
	{
		private TabBarItem? observedTab;

		public override string Label
			=> Strings.CloseTab.GetLocalizedResource();

		public override string Description
			=> Strings.CloseSelectedTabDescription.GetLocalizedResource();

		public override HotKey HotKey
			=> new(Keys.W, KeyModifiers.Ctrl);

		public override HotKey SecondHotKey
			=> new(Keys.F4, KeyModifiers.Ctrl);

		public override RichGlyph Glyph
			=> new();

		public CloseSelectedTabAction()
		{
			ObserveCurrentTab();
		}

		public override Task ExecuteAsync(object? parameter = null)
		{
			context.Control!.CloseTab(context.CurrentTabItem);

			return Task.CompletedTask;
		}

		protected override bool GetIsExecutable()
		{
			return
				context.Control is not null &&
				context.TabCount > 0 &&
				context.CurrentTabItem is { IsPinned: false };
		}

		protected override void Context_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(IMultitaskingContext.CurrentTabItem):
				case nameof(IMultitaskingContext.Control):
				case nameof(IMultitaskingContext.TabCount):
					ObserveCurrentTab();
					OnPropertyChanged(nameof(IsExecutable));
					break;
			}
		}

		private void ObserveCurrentTab()
		{
			if (observedTab == context.CurrentTabItem)
				return;
			if (observedTab is not null)
				observedTab.PropertyChanged -= Tab_PropertyChanged;
			observedTab = context.CurrentTabItem;
			if (observedTab is not null)
				observedTab.PropertyChanged += Tab_PropertyChanged;
		}

		private void Tab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(TabBarItem.IsPinned))
				OnPropertyChanged(nameof(IsExecutable));
		}
	}
}
