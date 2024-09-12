using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace RaisedGardenBeds
{
	public class AssetManager
	{
		private readonly IModHelper _helper;

		internal static readonly string GameContentAssetPath = Path.Combine("Mods", "blueberry.rgb.Assets");

		internal static readonly string GameContentEndOfNightSpritesPath = Path.Combine(GameContentAssetPath, "EndOfNightSprites");
		internal static readonly string GameContentEventDataPath = Path.Combine(GameContentAssetPath, "EventData");
		internal static readonly string GameContentCommonTranslationDataPath = Path.Combine(GameContentAssetPath, "CommonTranslations");
		internal static readonly string GameContentItemTranslationDataPath = Path.Combine(GameContentAssetPath, "ItemTranslations");

		internal static readonly string LocalAssetPath = "assets";

		internal static readonly string LocalEndOfNightSpritesPath = Path.Combine(LocalAssetPath, "endOfNightSprites.png");
		internal static readonly string LocalEventDataPath = Path.Combine(LocalAssetPath, "eventData.json");

		internal static readonly string ContentPackPath = Path.Combine(LocalAssetPath, "ContentPack");


		public AssetManager(IModHelper helper)
		{
			this._helper = helper;
		}

		public bool CanLoad<T>(IAssetInfo asset)
		{
			return asset.Name.IsEquivalentTo(GameContentEndOfNightSpritesPath)
				|| asset.Name.IsEquivalentTo(GameContentEventDataPath)
				|| asset.Name.IsEquivalentTo(GameContentCommonTranslationDataPath)
				|| asset.Name.IsEquivalentTo(GameContentItemTranslationDataPath);
		}

		public T Load<T>(IAssetInfo asset)
		{
			if (asset.Name.IsEquivalentTo(GameContentEndOfNightSpritesPath))
			{
				_helper.ModContent.Load
					<Texture2D>
					(LocalEndOfNightSpritesPath);
			}
			if (asset.Name.IsEquivalentTo(GameContentEventDataPath))
			{
				return (T)(object)_helper.ModContent.Load
					<Dictionary<string, object>>
					(LocalEventDataPath);
			}
			if (asset.Name.IsEquivalentTo(GameContentCommonTranslationDataPath))
			{
				var data = new Dictionary
					<string, Dictionary<string, string>>
					(StringComparer.InvariantCultureIgnoreCase);

				// Populate all possible language codes for translation pack support
				string[] keys = Enum.GetNames(typeof(StardewValley.LocalizedContentManager.LanguageCode));
				foreach (string key in keys)
				{
					data.Add(key, []);
				}

				return (T)(object)data;
			}
			if (asset.Name.IsEquivalentTo(GameContentItemTranslationDataPath))
			{
				var data = new Dictionary
					<string, Dictionary<string, Dictionary<string, string>>>
					(StringComparer.InvariantCultureIgnoreCase);

				// Populate all possible language codes for translation pack support
				string[] keys = Enum.GetNames(typeof(StardewValley.LocalizedContentManager.LanguageCode));
				foreach (string key in keys)
				{
					data.Add(key, []);
				}

				return (T)(object)data;
			}
			return (T)(object)null;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return asset.Name.IsEquivalentTo(GameContentEventDataPath)
				|| asset.Name.IsEquivalentTo(Path.Combine("Data", "CraftingRecipes"))
				// Also patch the event dictionary for any locations with an entry in our event definitions
				|| (asset.Name.StartsWith(Path.Combine("Data", "Events"))
					&& Path.GetFileNameWithoutExtension(asset.Name.ToString()) is string where
					&& ModEntry.EventData is not null && ModEntry.EventData.Any(e => e["Where"] == where));
		}

		public void Edit(IAssetData asset)
		{
			/*********
			Local data
			*********/

			if (asset.Name.IsEquivalentTo(GameContentEventDataPath))
			{
				var events = ((Newtonsoft.Json.Linq.JArray)asset
					.AsDictionary<string, object>()
					.Data["Events"])
					.ToObject<List<Dictionary<string, string>>>();

				// Events are populated with preset tokens and script dialogues depending on game locale.

				// Root event tokenisation
				for (int i = 0; i < events.Count; ++i)
				{
					// Format event script with event NPC name, as well as their dialogue strings
					string[] args = new string[] { events[i]["Who"] }
						.Concat(new int[] { 1, 2, 3, 4 }
							.Select(j => Translations.GetTranslation($"event.{i}.dialogue.{j}")))
						.ToArray();
					events[i]["Script"] = string.Format(
						format: events[i]["Script"],
						args: args);
					events[i]["Conditions"] = string.Format(
						format: events[i]["Conditions"],
						events[i]["Who"]);
				}

				ModEntry.EventData = events;

				return;
			}

			/********
			Game data
			********/
			if (asset.Name.IsEquivalentTo(Path.Combine("Data", "CraftingRecipes")))
			{
				if (ModEntry.ItemDefinitions is null)
					return;

				var data = asset.AsDictionary<string, string>().Data;

				// Add crafting recipes for all object variants
				foreach (ItemDefinition entry in ModEntry.ItemDefinitions.Values)
				{
					string ingredients = ItemDefinition.ParseRecipeIngredients(data: entry);
					string displayName = Translations.GetNameTranslation(data: entry);
					string[] fields =
					[	// Crafting ingredients:
						ingredients,
						// Unused field:
						"blue berry",
						// Crafted item ID and quantity:
						$"{entry.ItemName} {entry.RecipeCraftedCount}",
						// Recipe is bigCraftable:
						"true",
						// Recipe conditions (we ignore these):
						"blue berry",
						// Recipe display name:
						displayName
					];
					data[entry.ItemName] = string.Join("/", fields);
				}

				return;
			}
			if (asset.Name.StartsWith(Path.Combine("Data", "Events"))
				&& Path.GetFileNameWithoutExtension(asset.Name.ToString()) is string where)
			{
				// Patch our event data into whatever location happens to match the one specified.
				// Event tokenisation is handled in the Edit block for GameContentEventDataPath.

				if (ModEntry.EventData?.FirstOrDefault(e => e["Where"] == where) is Dictionary<string, string> eventData)
				{
					string key = $"{ModEntry.EventRootId}{ModEntry.EventData.IndexOf(eventData)}/{eventData["Conditions"]}";
					asset.AsDictionary<string, string>().Data[key] = eventData["Script"];
				}

				return;
			}
		}
	}
}
