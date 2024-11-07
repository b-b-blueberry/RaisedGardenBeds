using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace RaisedGardenBeds
{
	public static class Translations
	{
		/// <summary>
		/// Translation definitions for all common UI strings.
		/// </summary>
		internal static Dictionary<string, Dictionary<string, string>> CommonTranslations = null;
		/// <summary>
		/// Language code used if current language code contains no entries for a given translation.
		/// </summary>
		private static LocalizedContentManager.LanguageCode DefaultLanguageCode => LocalizedContentManager.LanguageCode.en;
		private static LocalizedContentManager.LanguageCode[] LanguageCodesToTry;


		private static void LocalizedContentManager_OnLanguageChange(LocalizedContentManager.LanguageCode code)
		{
			Translations.SetForLanguage(code: code);
		}

		public static void Initialise()
		{
			LocalizedContentManager.OnLanguageChange += Translations.LocalizedContentManager_OnLanguageChange;
			Translations.SetForLanguage(code: LocalizedContentManager.CurrentLanguageCode);
			Translations.LoadTranslationPacks();
		}

		public static void SetForLanguage(LocalizedContentManager.LanguageCode code)
		{
			Translations.LanguageCodesToTry =
			[
				code,
				Translations.DefaultLanguageCode
			];
		}

		/// <summary>
		/// Prompt SMAPI to check for all Content Patcher packs targeting our translation assets.
		/// </summary>
		public static void LoadTranslationPacks()
		{
			Log.T($"Loading translation packs for locale '{LocalizedContentManager.CurrentLanguageCode}'.");
			Log.T($"Translators should target these paths:{Environment.NewLine}\"Target\": \"{AssetManager.GameContentCommonTranslationDataPath}\"{Environment.NewLine}\"");

			Translations.CommonTranslations = Game1.content.Load
				<Dictionary<string, Dictionary<string, string>>>
				(AssetManager.GameContentCommonTranslationDataPath);
		}

		/// <summary>
		/// Return a dictionary of all translations for the current or default language.
		/// </summary>
		public static Dictionary<string, string> GetTranslations(LocalizedContentManager.LanguageCode? languageCode = null)
		{
			if (languageCode.HasValue)
			{
				return Translations.CommonTranslations[languageCode.ToString()];
			}
			foreach (LocalizedContentManager.LanguageCode lc in Translations.LanguageCodesToTry)
			{
				return Translations.CommonTranslations[lc.ToString()];
			}
			return null;
		}

		/// <summary>
		/// Return the translated string for a given entry in the <see cref="Translations.CommonTranslations"/> dictionary.
		/// </summary>
		public static string GetTranslation(string key, object[] tokens = null, bool defaultToNull = false)
		{
			foreach (LocalizedContentManager.LanguageCode lc in Translations.LanguageCodesToTry)
			{
				if (Translations.CommonTranslations.TryGetValue(lc.ToString(), out Dictionary<string, string> entries)
					&& entries.TryGetValue(key, out string translation) && !string.IsNullOrWhiteSpace(translation))
				{
					return tokens?.Length > 0 ? string.Format(translation, tokens) : translation;
				}
			}
			return defaultToNull ? null : key;
		}

		/// <summary>
		/// Return the display name for an item definition.
		/// </summary>
		/// <param name="data">Item definition entry.</param>
		public static string GetNameTranslation(ItemDefinition data)
		{
			return Translations.GetTranslation("item.name.variant", tokens: [data.DisplayName ?? data.LocalName]);
		}
	}
}
