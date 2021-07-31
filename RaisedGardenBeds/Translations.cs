using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaisedGardenBeds
{
	public static class Translations
	{
		internal static Dictionary<string, Dictionary<string, Dictionary<string, string>>> ItemTranslations = null;
		internal static Dictionary<string, Dictionary<string, string>> CommonTranslations = null;
		private static LocalizedContentManager.LanguageCode DefaultLanguageCode => LocalizedContentManager.LanguageCode.en;


		/// <summary>
		/// Return a dictionary of all translations for the current or default language.
		/// </summary>
		public static Dictionary<string, string> GetTranslations()
		{
			Dictionary<string, string> entries;
			return Translations.CommonTranslations.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out entries)
				? entries
				: Translations.CommonTranslations.TryGetValue(Translations.DefaultLanguageCode.ToString(), out entries)
					? entries
					: Translations.CommonTranslations.First().Value;
		}

		/// <summary>
		/// Return a dictionary of all item translations for the current or default language.
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> GetItemTranslations()
		{
			Dictionary<string, Dictionary<string, string>> entries;
			return Translations.ItemTranslations.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out entries)
				? entries
				: Translations.ItemTranslations.TryGetValue(Translations.DefaultLanguageCode.ToString(), out entries)
					? entries
					: Translations.ItemTranslations.First().Value;
		}

		public static string GetTranslation(string key, object[] tokens = null)
		{
			string translation;
			Dictionary<string, string> entries;
			if (!Translations.CommonTranslations.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out entries)
				|| !entries.TryGetValue(key, out translation))
				if (!Translations.CommonTranslations.TryGetValue(Translations.DefaultLanguageCode.ToString(), out entries)
					|| !entries.TryGetValue(key, out translation))
					return key;
			return tokens?.Length > 0 ? string.Format(translation, tokens) : translation;
		}

		public static string GetNameTranslation(Content.ContentData data)
		{
			string pack = data.ContentPack.Manifest.UniqueID;
			string item = data.LocalName;

			string translation;
			Dictionary<string, Dictionary<string, string>> packs;
			Dictionary<string, string> items;
			if (!Translations.ItemTranslations.TryGetValue(LocalizedContentManager.CurrentLanguageCode.ToString(), out packs)
				|| !packs.TryGetValue(pack, out items)
				|| !items.TryGetValue(item, out translation))
				if (!Translations.ItemTranslations.TryGetValue(Translations.DefaultLanguageCode.ToString(), out packs)
					|| !packs.TryGetValue(pack, out items)
					|| !items.TryGetValue(item, out translation))
					return data.LocalName;

			return Translations.GetTranslation("item.name.variant", tokens: new[] { translation ?? data.LocalName });
		}

		/// <summary>
		/// Prompt SMAPI to check for all Content Patcher packs targeting our translation assets.
		/// </summary>
		public static void LoadTranslationPacks()
		{
			Log.T("Loading translation packs.");
			Log.T($"Translators should target these paths:{Environment.NewLine}\"Target\": \"{AssetManager.GameContentCommonTranslationDataPath}\"{Environment.NewLine}\"Target\": \"{AssetManager.GameContentItemTranslationDataPath}\"");

			Translations.CommonTranslations = Game1.content.Load
				<Dictionary<string, Dictionary<string, string>>>
				(AssetManager.GameContentCommonTranslationDataPath);
			Translations.ItemTranslations = Game1.content.Load
				<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>
				(AssetManager.GameContentItemTranslationDataPath);

			Log.T(Translations.ItemTranslations.Aggregate("Loaded translation pack(s):", (str, entry) => $"{str}{Environment.NewLine}{entry.Key}: {entry.Value.Count} content pack(s) containing {entry.Value.Sum(v => v.Value.Count)} items."));
		}
	}
}
