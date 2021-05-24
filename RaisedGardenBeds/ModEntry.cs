using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RaisedGardenBeds
{
	// TODO: FEATURE: Add GMCM config support, including IngameOptin
	// TODO: FEATURE: Look into sprinkler radius
	// TODO: TEST: See StardewValley.Farmer.cs:tryToCraftItem()
	/*
		if (bigCraftable)
		{
			Game1.player.ActiveObject = new Object(Vector2.Zero, itemToCraft);
			Game1.player.showCarrying();
		}
	*/

	public class ModEntry : Mod, IAssetLoader, IAssetEditor
	{
		internal static ModEntry Instance;
		internal static Config Config;
		internal static IJsonAssetsApi JsonAssets;
		internal ITranslationHelper i18n => Helper.Translation;

		// definitions
		internal static Dictionary<string, Dictionary<string, string>> ItemDefinitions = null;
		internal static Dictionary<string, string> EventData = null;
		internal static List<Dictionary<string, string>> Events = null;
		// assets
		internal const string AssetPrefix = "blueberry.rgb.Assets";
		internal const string ItemName = "blueberry.rgb.raisedbed";
		internal static readonly string ContentPackPath = Path.Combine("assets", "ContentPack");
		internal static readonly string GameContentEventDataPath = Path.Combine(AssetPrefix, "EventData");
		internal static readonly string LocalEventDataPath = Path.Combine("assets", "eventData.json");
		internal static readonly string GameContentItemDefinitionsPath = Path.Combine(AssetPrefix, "ItemDefinitions");
		internal static readonly string LocalItemDefinitionsPath = Path.Combine("assets", "itemDefinitions.json");
		internal static readonly string GameContentSpritesPath = Path.Combine(AssetPrefix, "Sprites");
		internal static readonly string LocalSpritesPath = Path.Combine("assets", "sprites.png");
		// others
		internal const string CommandPrefix = "rgb";
		internal static int ModUpdateKey = -1;
		internal static int EventRoot => ModUpdateKey * 10000;


		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<Config>();
			ModUpdateKey = int.Parse(ModManifest.UpdateKeys.First().Split(':')[1]);
			HarmonyPatches.Patch(id: ModManifest.UniqueID);

			helper.Events.GameLoop.GameLaunched += this.GameLoop_GameLaunched;
			helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;
			helper.Events.GameLoop.DayEnding += this.GameLoop_DayEnding;
			helper.ConsoleCommands.Add(name: CommandPrefix + "give", "Drop some raised bed items.", Cmd_Give);
			helper.ConsoleCommands.Add(name: CommandPrefix + "prebreak", "Mark all raised beds as ready to break.", Cmd_Prebreak);
		}

		private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked += this.Event_LoadLate;
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			if (Context.IsMainPlayer)
			{
				// Reinitialise objects to recalculate XmlIgnore values
				OutdoorPot.AdjustAll(reinitialise: true);
			}
		}

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			// Ensure generic object recipe is unavailable
			//Game1.player.craftingRecipes.Remove(ItemName);

			// Invalidate sprites at the start of each day in case patches target the asset
			OutdoorPot.ReloadSprites();
		}

		private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			if (Config.RaisedBedsMayBreakWithAge && Game1.dayOfMonth == 28)
			{
				OutdoorPot.BreakAll();
			}

			// Add all available object recipes to player list
			NewRecipeMenu.Push(GetNewRecipes());
		}

		private void Event_LoadLate(object sender, OneSecondUpdateTickedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked -= this.Event_LoadLate;

			this.LoadApis();
			OutdoorPot.ReloadSprites();
			ItemDefinitions = Game1.content.Load<Dictionary<string, Dictionary<string, string>>>(GameContentItemDefinitionsPath);
		}

		private void LoadApis()
		{
			// SpaceCore
			ISpaceCoreAPI spacecoreAPI = Helper.ModRegistry.GetApi<ISpaceCoreAPI>("spacechase0.SpaceCore");
			spacecoreAPI.RegisterSerializerType(typeof(OutdoorPot));

			// Json Assets
			JsonAssets = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
			if (JsonAssets == null)
			{
				Log.E("Can't access the Json Assets API. Is the mod installed correctly?");
				return;
			}
			JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, ContentPackPath));

			// Generic Mod Config Menu
			IGenericModConfigMenuAPI modconfigAPI = Helper.ModRegistry.GetApi<IGenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
			modconfigAPI.RegisterModConfig(
				ModManifest,
				revertToDefault: () => Config = new Config(),
				saveToFile: () => Helper.WriteConfig(Config));
			modconfigAPI.SetDefaultIngameOptinValue(
				ModManifest,
				optedIn: true);
			System.Reflection.PropertyInfo[] properties = Config.GetType().GetProperties()
				.Where(p => p.PropertyType == typeof(bool)).ToArray();
			foreach (System.Reflection.PropertyInfo property in properties)
			{
				string key = property.Name.ToLower();
				Translation description = i18n.Get("config." + key + ".description");
				modconfigAPI.RegisterSimpleOption(
					ModManifest,
					optionName: i18n.Get("config." + key + ".name"),
					optionDesc: description.HasValue() ? description : null,
					optionGet: () => (bool)property.GetValue(Config),
					optionSet: (bool value) => property.SetValue(Config, value: value));
			}
		}

		public static List<string> GetNewRecipes()
		{
			List<string> newVarieties = new List<string>();
			for (int i = 0; i < ItemDefinitions.Count; ++i)
			{
				string varietyName = ItemDefinitions.Keys.ElementAt(i);
				string fullVarietyName = ItemName + "." + varietyName;
				int eventID = EventRoot + i;
				int precondition = !Game1.player.craftingRecipes.ContainsKey(fullVarietyName)
					&& (Config.RecipesAlwaysAvailable
						|| bool.Parse(ItemDefinitions[varietyName]["RecipeIsDefault"])
						|| (string.IsNullOrEmpty(ItemDefinitions[varietyName]["RecipeConditions"])
							&& !Game1.player.eventsSeen.Contains(EventRoot)))
					? 1
					: Game1.getFarm().checkEventPrecondition(
						eventID.ToString() + "/" + (string.IsNullOrEmpty(ItemDefinitions[varietyName]["RecipeConditions"])
						? "null" : ItemDefinitions[varietyName]["RecipeConditions"]));
				if (precondition != -1)
				{
					newVarieties.Add(fullVarietyName);
					Game1.player.craftingRecipes.Add(fullVarietyName, 0);
				}
			}
			return newVarieties;
		}

		public static void Cmd_Give(string s, string[] args)
		{
			const int defaultQuantity = 25;
			int quantity = args.Length == 0 ? defaultQuantity : int.TryParse(args[0], out int argQuantity) ? argQuantity : defaultQuantity;
			foreach (string key in ItemDefinitions.Keys)
			{
				OutdoorPot item = new OutdoorPot(variant: OutdoorPot.GetVariantIndexFromName(key), tileLocation: Vector2.Zero)
				{
					Stack = quantity
				};
				if (!Game1.player.addItemToInventoryBool(item))
				{
					Log.W("Inventory full: Did not add " + key + " raised bed.");
				}
			}
		}

		public static void Cmd_Prebreak(string s, string[] args)
		{
			if (!Config.RaisedBedsMayBreakWithAge)
			{
				Log.W("Breakage is disabled in mod config file!");
				return;
			}
			foreach (OutdoorPot o in Game1.getFarm().Objects.Values.OfType<OutdoorPot>().Where(o => o.MinutesUntilReady > 0))
			{
				o.Unbreak();
			}
		}

		public bool CanLoad<T>(IAssetInfo asset)
		{
			return asset.AssetNameEquals(GameContentSpritesPath)
				|| asset.AssetNameEquals(GameContentItemDefinitionsPath)
				|| asset.AssetNameEquals(GameContentEventDataPath);
		}

		public T Load<T>(IAssetInfo asset)
		{
			if (asset.AssetNameEquals(GameContentSpritesPath))
				return (T) (object) Helper.Content.Load<Texture2D>(LocalSpritesPath);
			if (asset.AssetNameEquals(GameContentItemDefinitionsPath))
				return (T)(object) Helper.Content.Load<Dictionary<string, Dictionary<string, string>>>(LocalItemDefinitionsPath);
			if (asset.AssetNameEquals(GameContentEventDataPath))
				return (T) (object) Helper.Content.Load<Dictionary<string, string>>(LocalEventDataPath);
			return (T) (object) null;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			bool isReady = OutdoorPot.Sprites != null && JsonAssets != null && !(Game1.activeClickableMenu is StardewValley.Menus.TitleMenu);
			bool isEventAsset = asset.AssetName.StartsWith(Path.Combine("Data", "Events"))
						&& Path.GetFileNameWithoutExtension(asset.AssetName) is string where
						&& Events != null && Events.Any(e => e["Where"] == where);
			return isReady
				&& (asset.AssetNameEquals(GameContentItemDefinitionsPath)
					|| asset.AssetNameEquals(GameContentEventDataPath)
					|| asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables"))
					|| asset.AssetNameEquals(Path.Combine("Data", "BigCraftablesInformation"))
					|| asset.AssetNameEquals(Path.Combine("Data", "CraftingRecipes"))
					|| isEventAsset);
		}

		public void Edit<T>(IAssetData asset)
		{
			// Local data
			if (asset.AssetNameEquals(GameContentItemDefinitionsPath))
			{
				// Remove items until number is within the reserved set
				string itemDataPath = Path.Combine(ContentPackPath, "BigCraftables", "Raised Bed", "big-craftable.json");
				var itemData = Instance.Helper.Content.Load<Dictionary<string, object>>(itemDataPath);
				var data = asset.AsDictionary<string, Dictionary<string, string>>().Data;
				while (data.Count > (int)itemData["ReserveExtraIndexCount"] || data.Count > OutdoorPot.Sprites.Height / Game1.smallestTileSize * 2)
				{
					string key = ItemDefinitions.Last().Key;
					if (data.Remove(key))
						Log.W("Removing excess raised bed: " + key);
					else
						break;
				}
				return;
			}
			if (asset.AssetNameEquals(GameContentEventDataPath))
			{
				var events = JsonConvert.DeserializeObject(asset.AsDictionary<string, string>().Data["Events"])
					as List<Dictionary<string, string>>;
				Events = events;

				return;
			}

			// Game data
			int id = JsonAssets == null ? -1 : JsonAssets.GetBigCraftableId(ItemName);
			if (id < 0)
				return;
			if (asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables")))
			{
				// Patch basic object sprites to game craftables sheet for all variants
				for (int variant = 0; variant < ItemDefinitions.Count; ++variant)
				{
					Rectangle destination = StardewValley.Object.getSourceRectForBigCraftable(id + variant);
					Rectangle source;
					string variantName = OutdoorPot.GetVariantKeyFromVariantIndex(variant: variant);
					int soilOffsetY = int.Parse(ItemDefinitions[variantName]["SoilHeightAboveGround"]);
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
				for (int i = 0; i < ItemDefinitions.Count; ++i)
				{
					string varietyName = ItemDefinitions.Keys.ToArray()[i];
					fields = data[id].Split('/');
					fields[8] = i18n.Get("item.name." + varietyName);
					data[id + i] = string.Join("/", fields);
				}
				
				// Patch object display name and description from localisations file
				fields = data[id].Split('/');
				fields[4] = i18n.Get("item.description" + (Config.CanBePlacedInBuildings ? ".indoors" : ""));
				fields[8] = i18n.Get("item.name");
				data[id] = string.Join("/", fields);
				
				return;
			}
			if (asset.AssetNameEquals(Path.Combine("Data", "CraftingRecipes")))
			{
				// Add crafting recipes for all object variants
				var data = asset.AsDictionary<string, string>().Data;
				string[] fields = data[ItemName].Split('/');
				foreach (KeyValuePair<string, Dictionary<string, string>> idAndFields in ItemDefinitions)
				{
					string[] newFields = new string[]
					{
						idAndFields.Value["RecipeItems"],			// Crafting ingredients
						fields[1],									// Unused field
						fields[2],									// Crafted item ID and quantity
						idAndFields.Value["RecipeIsDefault"],		// Recipe always available
						idAndFields.Value["RecipeConditions"] ?? "null",	// Recipe availability conditions
						i18n.Get("item.name." + idAndFields.Key)	// Recipe display name
					};
					data[ItemName + "." + idAndFields.Key] = string.Join("/", newFields);
				}
				return;
			}
			if ((asset.AssetName.StartsWith(Path.Combine("Data", "Events"))
				&& Path.GetFileNameWithoutExtension(asset.AssetName) is string where
				&& Events != null
				&& Events.FirstOrDefault(e => e["Where"] == where) is Dictionary<string, string> eventData))
			{
				string key = EventRoot + Events.IndexOf(eventData) + "/" + eventData["Conditions"];
				string script = string.Format(
					format: eventData["Script"],
					eventData["Who"],
					i18n.Get("event.0.dialogue"));
				asset.AsDictionary<string, string>().Data[key] = script;

				return;
			}
		}
	}
}
