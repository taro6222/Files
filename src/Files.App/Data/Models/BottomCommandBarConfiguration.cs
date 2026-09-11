// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Data.Models
{
	public sealed record BottomCommandEntry(bool Visible, string Code, string Key);

	public static class BottomCommandBarConfiguration
	{
		public static readonly string[] Actions = ["BottomView", "BottomEdit", "BottomCopy", "BottomMove", "CreateFolder", "DeleteItem", "BottomClose", "Rename"];

		public static List<BottomCommandEntry> Defaults() =>
		[
			new(true, "BottomView", "F3"), new(true, "BottomEdit", "F4"),
			new(true, "BottomCopy", "F5"), new(true, "BottomMove", "F6"),
			new(true, "CreateFolder", "F7"), new(true, "DeleteItem", "F8"),
			new(true, "BottomClose", "Alt+F4"), new(false, "Rename", "")
		];

		public static List<BottomCommandEntry> Load()
		{
			var saved = Ioc.Default.GetRequiredService<IAppSettingsService>().BottomCommandBar;
			if (string.IsNullOrEmpty(saved))
				return Defaults();
			var result = new List<BottomCommandEntry>();
			foreach (var row in saved.Split(';'))
			{
				var parts = row.Split('|');
				if (parts.Length != 3 || !Actions.Contains(parts[1]))
					return Defaults();
				result.Add(new(parts[0] == "1", parts[1], parts[2]));
			}
			return result.Count == 8 ? result : Defaults();
		}

		public static void Save(IEnumerable<BottomCommandEntry> rows) =>
			Ioc.Default.GetRequiredService<IAppSettingsService>().BottomCommandBar =
				string.Join(';', rows.Select(row => $"{(row.Visible ? 1 : 0)}|{row.Code}|{row.Key}"));

		public static bool HasConflict(HotKey key, string code, IEnumerable<IRichCommand> commands,
			IEnumerable<BottomCommandEntry>? entries = null)
		{
			if (key.IsNone)
				return false;
			return commands.Any(command => command.Code.ToString() != code &&
				command.HotKeys.Any(existing => SameKey(existing, key))) ||
				(entries ?? Load()).Any(row => row.Code != code && TryParseKey(row.Key, out var existing) && SameKey(existing, key));
		}

		public static HotKey EffectiveKey(BottomCommandEntry row, IEnumerable<IRichCommand> commands)
		{
			if (!TryParseKey(row.Key, out var key) || key.IsNone)
				return HotKey.None;
			// Existing action shortcuts retain priority when old settings contain a conflict.
			return commands.Any(command => command.Code.ToString() != row.Code &&
				command.HotKeys.Any(existing => SameKey(existing, key))) ? HotKey.None : key;
		}

		private static bool SameKey(HotKey left, HotKey right) =>
			left.Key == right.Key && left.Modifier == right.Modifier;
		public static bool TryParseKey(string text, out HotKey key)
		{
			key = HotKey.None;
			if (string.IsNullOrWhiteSpace(text))
				return true;
			var parts = text.Split('+', StringSplitOptions.TrimEntries);
			if (!Enum.TryParse<Keys>(parts[^1], true, out var last) || !Enum.IsDefined(last) || last == Keys.None)
				return false;
			foreach (var modifier in parts.SkipLast(1))
				if (modifier is not ("Ctrl" or "Alt" or "Shift"))
					return false;
			key = HotKey.Parse(text, false);
			// Printable unmodified keys belong to type-to-select and text input.
			return key.Modifier != KeyModifiers.None || (key.Key >= Keys.F1 && key.Key <= Keys.F24);
		}
	}
}