using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RaisedGardenBeds
{
	public class AssetManager : IAssetLoader, IAssetEditor
	{
		private IModHelper _helper;
		private ITranslationHelper i18n => _helper.Translation;

		internal const string AssetPrefix = "blueberry.rgb.Assets";

		internal static readonly string GameContentEndOfNightSpritesPath = Path.Combine(AssetPrefix, "EndOfNightSprites");
		internal static readonly string GameContentItemDefinitionsPath = Path.Combine(AssetPrefix, "ItemDefinitions");
		internal static readonly string GameContentEventDataPath = Path.Combine(AssetPrefix, "EventData");
		internal static readonly string GameContentSpritesPath = Path.Combine(AssetPrefix, "Sprites");

		internal static readonly string LocalEndOfNightSpritesPath = Path.Combine("assets", "endOfNightSprites.png");
		internal static readonly string LocalItemDefinitionsPath = Path.Combine("assets", "itemDefinitions.json");
		internal static readonly string LocalEventDataPath = Path.Combine("assets", "eventData.json");
		internal static readonly string LocalSpritesPath = Path.Combine("assets", "sprites.png");

		internal static readonly string ContentPackPath = Path.Combine("assets", "ContentPack");


		public AssetManager(IModHelper helper)
		{
			this._helper = helper;
		}

		public bool CanLoad<T>(IAssetInfo asset)
		{
			return asset.AssetNameEquals(GameContentEndOfNightSpritesPath)
				|| asset.AssetNameEquals(GameContentItemDefinitionsPath)
				|| asset.AssetNameEquals(GameContentEventDataPath)
				|| asset.AssetNameEquals(GameContentSpritesPath);
		}

		public T Load<T>(IAssetInfo asset)
		{
			if (asset.AssetNameEquals(GameContentSpritesPath))
				return (T)(object)_helper.Content.Load<Texture2D>(LocalSpritesPath);
			if (asset.AssetNameEquals(GameContentEndOfNightSpritesPath))
				return (T)(object)_helper.Content.Load<Texture2D>(LocalEndOfNightSpritesPath);
			if (asset.AssetNameEquals(GameContentEventDataPath))
				return (T)(object)_helper.Content.Load<Dictionary<string, object>>(LocalEventDataPath);
			if (asset.AssetNameEquals(GameContentItemDefinitionsPath))
				return (T)(object)_helper.Content.Load<Dictionary<string, Dictionary<string, string>>>(LocalItemDefinitionsPath);
			return (T)(object)null;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			bool isCustomContentReady = OutdoorPot.Sprites != null && ModEntry.JsonAssets != null && !(Game1.activeClickableMenu is StardewValley.Menus.TitleMenu);
			return asset.AssetNameEquals(GameContentItemDefinitionsPath)
				|| (isCustomContentReady
					&& (asset.AssetNameEquals(GameContentEventDataPath)
						|| asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables"))
						|| asset.AssetNameEquals(Path.Combine("Data", "BigCraftablesInformation"))
						|| asset.AssetNameEquals(Path.Combine("Data", "CraftingRecipes"))
						|| (asset.AssetName.StartsWith(Path.Combine("Data", "Events"))
							&& Path.GetFileNameWithoutExtension(asset.AssetName) is string where
							&& ModEntry.EventData != null && ModEntry.EventData.Any(e => e["Where"] == where))));
		}

		public void Edit<T>(IAssetData asset)
		{
			// Local data
			if (asset.AssetNameEquals(GameContentItemDefinitionsPath))
			{
				// Remove items until number is within the reserved set
				string itemDataPath = Path.Combine(ContentPackPath, "BigCraftables", "Raised Bed", "big-craftable.json");
				var itemData = _helper.Content.Load<Dictionary<string, object>>(itemDataPath);
				var data = asset.AsDictionary<string, Dictionary<string, string>>().Data;

				long numberOfVariants = 1 + System.Convert.ToInt64(itemData["ReserveExtraIndexCount"]);
				int numberOfSprites = OutdoorPot.Sprites.Height / Game1.smallestTileSize / 2;
				string warnMessage = null;
				if (data.Count > numberOfVariants || data.Count > numberOfSprites)
				{
					warnMessage = $"Found {numberOfVariants - data.Count} partially-defined garden beds in ItemDefinitions.";
					if (data.Count > numberOfVariants)
						warnMessage += $"\nJSON big-craftable 'ReserveExtraIndexCount': {numberOfVariants - 1}.";
					if (data.Count > numberOfSprites)
						warnMessage += $"\nJSON big-craftable blank sprite files found: {numberOfSprites}";
				}
				
				if (warnMessage != null)
				{
					Log.W(warnMessage);
					while (data.Count > numberOfVariants || data.Count > numberOfSprites)
					{
						string key = data.Last().Key;
						if (data.Remove(key))
							Log.W("Removing excess raised bed: " + key);
						else
							Log.E("Failed to remove excess raised bed: " + key);
					}
				}
				return;
			}
			if (asset.AssetNameEquals(GameContentEventDataPath))
			{
				var events = ((Newtonsoft.Json.Linq.JArray)asset.AsDictionary<string, object>().Data["Events"])
					.ToObject<List<Dictionary<string, string>>>();

				// Root event tokenisation
				events[0]["Conditions"] = string.Format(
					format: events[0]["Conditions"],
					events[0]["Who"]);
				events[0]["Script"] = string.Format(
					format: events[0]["Script"],
					events[0]["Who"],
					i18n.Get("event.0.dialogue.1"),
					i18n.Get("event.0.dialogue.2"),
					i18n.Get("event.0.dialogue.3"));

				ModEntry.EventData = events;

				return;
			}

			// Game data
			int id = ModEntry.JsonAssets == null ? -1 : ModEntry.JsonAssets.GetBigCraftableId(OutdoorPot.GenericName);
			if (id < 0)
				return;
			if (asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables")) && Game1.bigCraftableSpriteSheet != null)
			{
				// Patch basic object sprites to game craftables sheet for all variants
				for (int variant = 0; variant < ModEntry.ItemDefinitions.Count; ++variant)
				{
					Rectangle destination = StardewValley.Object.getSourceRectForBigCraftable(id + variant);
					Rectangle source;
					string variantName = OutdoorPot.GetVariantKeyFromVariantIndex(variant: variant);
					int soilOffsetY = int.Parse(ModEntry.ItemDefinitions[variantName]["SoilHeightAboveGround"]);
					// soil
					source = new Rectangle(Game1.smallestTileSize * OutdoorPot.SoilIndexInSheet, variant * Game1.smallestTileSize * 2, Game1.smallestTileSize, Game1.smallestTileSize);
					asset.AsImage().PatchImage(
						source: OutdoorPot.Sprites,
						sourceArea: source,
						targetArea: new Rectangle(destination.X, destination.Y + Game1.smallestTileSize - soilOffsetY, source.Width, source.Height),
						patchMode: PatchMode.Overlay);
					// object
					source = new Rectangle(0, variant * Game1.smallestTileSize * 2, destination.Width, destination.Height);
					asset.AsImage().PatchImage(
						source: OutdoorPot.Sprites,
						sourceArea: source,
						targetArea: destination,
						patchMode: PatchMode.Overlay);
				}
				return;
			}
			if (asset.AssetNameEquals(Path.Combine("Data", "BigCraftablesInformation")))
			{
				string[] fields;
				var data = asset.AsDictionary<int, string>().Data;

				// Patch dummy entries into bigcraftables file
				for (int i = 0; i < ModEntry.ItemDefinitions.Count; ++i)
				{
					string varietyName = ModEntry.ItemDefinitions.Keys.ToArray()[i];
					fields = data[id].Split('/');
					fields[8] = i18n.Get("item.name." + varietyName);
					data[id + i] = string.Join("/", fields);
				}
				
				// Patch object display name and description from localisations file
				fields = data[id].Split('/');
				fields[4] = i18n.Get("item.description" + (ModEntry.Config.CanBePlacedInBuildings ? ".indoors" : ""));
				fields[8] = i18n.Get("item.name");
				data[id] = string.Join("/", fields);

				// Don't remove the generic craftable from data lookup, since it's used later for crafting recipes and defaults

				return;
			}
			if (asset.AssetNameEquals(Path.Combine("Data", "CraftingRecipes")))
			{
				// Add crafting recipes for all object variants
				var data = asset.AsDictionary<string, string>().Data;
				string[] fields = data[OutdoorPot.GenericName].Split('/');
				foreach (KeyValuePair<string, Dictionary<string, string>> idAndFields in ModEntry.ItemDefinitions)
				{
					string[] newFields = new string[]
					{
						idAndFields.Value["RecipeItems"],			// Crafting ingredients
						fields[1],									// Unused field
						fields[2],									// Crafted item ID and quantity
						"true",										// Recipe is bigCraftable
						"blue berry",								// Recipe conditions
						i18n.Get("item.name." + idAndFields.Key)	// Recipe display name
					};
					data[OutdoorPot.GenericName + "." + idAndFields.Key] = string.Join("/", newFields);
				}

				// Remove generic crafting recipe to prevent it from appearing in lookups
				data.Remove(OutdoorPot.GenericName);
				return;
			}
			if (asset.AssetName.StartsWith(Path.Combine("Data", "Events"))
				&& Path.GetFileNameWithoutExtension(asset.AssetName) is string where)
			{
				if (ModEntry.EventData != null
					&& ModEntry.EventData.FirstOrDefault(e => e["Where"] == where) is Dictionary<string, string> eventData)
				{
					string key = ModEntry.EventRoot + ModEntry.EventData.IndexOf(eventData) + "/" + eventData["Conditions"];
					asset.AsDictionary<string, string>().Data[key] = eventData["Script"];
				}

				return;
			}
		}
	}
}
