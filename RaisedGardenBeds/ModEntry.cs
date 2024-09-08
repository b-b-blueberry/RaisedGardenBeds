using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace RaisedGardenBeds
{
	public class ModEntry : Mod
	{
		// common
		internal static ModEntry Instance;
		internal static Config Config;
		internal static AssetManager AssetManager;

		// definitions
		/// <summary>
		/// Shared object variant dictionary containing entries provided by the content pack, as well as some metadata about the content pack itself.
		/// Entries are keyed by <see cref="OutdoorPot.VariantIndex".
		/// </summary>
		internal static Dictionary<string, ItemDefinition> ItemDefinitions = null;
		/// <summary>
		/// Shared object spritesheet dictionary containing object icon, world sprite component, object breakage, and watered/unwatered soil sprites.
		/// Entries are keyed by <see cref="OutdoorPot.VariantIndex".
		/// </summary>
		internal static Dictionary<string, Texture2D> Sprites = null;
		/// <summary>
		/// List of parsed events loaded from <see cref="AssetManager.GameContentEventDataPath"./>
		/// Event entries are keyed by event ID and conditions.
		/// </summary>
		internal static List<Dictionary<string, string>> EventData = null;
		/// <summary>
		/// Flag raised when item definitions are added to game big craftables dictionary.
		/// </summary>
		internal static bool IsDataAdded;


		// others
		internal static int ModUpdateKey;
		internal static int EventRootId => ModEntry.ModUpdateKey * 10000;
		internal const string CommandPrefix = "rgb.";
		internal const string EndOfNightState = "blueberry.rgb.endofnightmenu";


		public override void Entry(IModHelper helper)
		{
			ModEntry.Instance = this;
			ModEntry.Config = helper.ReadConfig<Config>();
			ModEntry.AssetManager = new AssetManager(helper: this.Helper);
			ModEntry.ModUpdateKey = int.Parse(this.ModManifest.UpdateKeys.First().Split(':')[1]);

			helper.Events.GameLoop.GameLaunched += this.GameLoop_GameLaunched;
			helper.Events.Content.AssetRequested += this.OnAssetRequested;
		}

		private Dictionary<string, Dictionary<string, string>> CTData()
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
			return data;
		}

		private Dictionary<string, Dictionary<string, Dictionary<string, string>>> ITData()
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
			return data;
		}

		private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
		{
			bool isModContent = true;
			if (e.Name.IsEquivalentTo(AssetManager.GameContentEndOfNightSpritesPath))
			{
				e.LoadFromModFile
					<Texture2D>
					(AssetManager.LocalEndOfNightSpritesPath, AssetLoadPriority.Exclusive);
			}
			else if (e.Name.IsEquivalentTo(AssetManager.GameContentEventDataPath))
			{
				e.LoadFromModFile
					<Dictionary<string, object>>
					(AssetManager.LocalEventDataPath, AssetLoadPriority.Exclusive);
			}
			else if (e.Name.IsEquivalentTo(AssetManager.GameContentCommonTranslationDataPath))
			{
				e.LoadFrom(this.CTData, AssetLoadPriority.Low);
			}
			else if (e.Name.IsEquivalentTo(AssetManager.GameContentItemTranslationDataPath))
			{
				e.LoadFrom(this.ITData, AssetLoadPriority.Low);
			}
			else
			{
				isModContent = false;
			}
			
			if (isModContent
				|| e.Name.IsEquivalentTo(Path.Combine("Data", "CraftingRecipes"))
				|| e.Name.StartsWith(Path.Combine("Data", "Events"))
					&& Path.GetFileNameWithoutExtension(e.Name.ToString()) is string where
					&& ModEntry.EventData?.Any(dict => dict["Where"] == where) is bool isHere && isHere)
			{
				e.Edit(ModEntry.AssetManager.Edit);
			}
		}

		private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
		{
			this.Helper.Events.GameLoop.OneSecondUpdateTicked += this.Event_LoadLate;
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			this.SaveLoadedBehaviours();
		}

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			// Perform OnSaveLoaded behaviours when starting a new game
			bool isNewGame = WorldDate.Now().TotalDays <= 1;
			if (isNewGame)
			{
				this.SaveLoadedBehaviours();
			}

			// Add always-available recipes to player list without any unique fanfare
			ModEntry.AddDefaultRecipes();
		}

		private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			// Break ready objects at the start of each season
			if (ModEntry.Config.RaisedBedsMayBreakWithAge && Game1.dayOfMonth == WorldDate.DaysPerMonth)
			{
				OutdoorPot.BreakAll();
			}
		}

		private void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			ModEntry.IsDataAdded = false;
		}

		private void Specialized_LoadStageChanged(object sender, LoadStageChangedEventArgs e)
		{
			if (e.NewStage is StardewModdingAPI.Enums.LoadStage.Loaded)
			{
				this.Helper.GameContent.InvalidateCache(Path.Combine("Data", "CraftingRecipes"));
			}
		}

		private void SpaceEvents_ShowNightEndMenus(object sender, SpaceCore.Events.EventArgsShowNightEndMenus e)
		{
			// Add and show any newly-available object recipes to player list at the end of day screens
			ModEntry.AddNewAvailableRecipes(out List<string> variantKeys);
			if (variantKeys.Any())
			{
				NewRecipeMenu.Push(variantKeys);
			}
		}

		private void Event_LoadLate(object sender, OneSecondUpdateTickedEventArgs e)
		{
			this.Helper.Events.GameLoop.OneSecondUpdateTicked -= this.Event_LoadLate;

			if (this.LoadAPIs())
			{
				this.Initialise();
			}
		}

		private bool LoadAPIs()
		{
			ISpaceCoreAPI spacecoreAPI = this.Helper.ModRegistry.GetApi<ISpaceCoreAPI>("spacechase0.SpaceCore");
			if (spacecoreAPI is null)
			{
				// Skip all mod behaviours if we fail to load the objects
				Log.E($"Couldn't access mod-provided API for SpaceCore.{Environment.NewLine}Garden beds will not be available, and no changes will be made.");
				return false;
			}

			spacecoreAPI.RegisterSerializerType(typeof(OutdoorPot));
			ItemRegistry.AddTypeDefinition(new OutdoorPotDataDefinition());

			return true;
		}

		private void Initialise()
		{
			// Content
			Translations.Initialise();
			this.LoadContentPacks();
			this.AddGenericModConfigMenu();

			// Patches
			HarmonyPatches.Patch(id: this.ModManifest.UniqueID);

			// Events
			this.Helper.Events.Specialized.LoadStageChanged += this.Specialized_LoadStageChanged;
			this.Helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			this.Helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;
			this.Helper.Events.GameLoop.DayEnding += this.GameLoop_DayEnding;
			this.Helper.Events.GameLoop.ReturnedToTitle += this.GameLoop_ReturnedToTitle;
			SpaceCore.Events.SpaceEvents.ShowNightEndMenus += this.SpaceEvents_ShowNightEndMenus;
		}

		private void AddGenericModConfigMenu()
		{
			IGenericModConfigMenuApi modconfigAPI = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
			if (modconfigAPI is not null)
			{
				modconfigAPI.Register(
					mod: this.ModManifest,
					reset: () => ModEntry.Config = new Config(),
					save: () => this.Helper.WriteConfig(ModEntry.Config));
				modconfigAPI.SetTitleScreenOnlyForNextOptions(
					mod: this.ModManifest,
					false);
				System.Reflection.PropertyInfo[] properties = ModEntry.Config
					.GetType()
					.GetProperties()
					.Where(p => p.PropertyType == typeof(bool))
					.ToArray();
				foreach (System.Reflection.PropertyInfo property in properties)
				{
					string key = property.Name.ToLower();
					string description = Translations.GetTranslation($"config.{key}.description", defaultToNull: true);
					modconfigAPI.AddBoolOption(
						mod: this.ModManifest,
						name: () => Translations.GetTranslation($"config.{key}.name"),
						tooltip: () => string.IsNullOrWhiteSpace(description) ? null : description,
						getValue: () => (bool)property.GetValue(ModEntry.Config),
						setValue: (bool value) => property.SetValue(ModEntry.Config, value: value));
				}
			}
		}

		private void SaveLoadedBehaviours()
		{
			Game1.player.team.endOfNightStatus.AddSpriteDefinition(
				key: ModEntry.EndOfNightState,
				file: AssetManager.GameContentEndOfNightSpritesPath,
				x: 48, y: 0, width: 16, height: 16);

			Game1.content.Load  // Return value unused; event data is set in AssetManager.Edit()
				<Dictionary<string, object>>
				(AssetManager.GameContentEventDataPath);

			// Reinitialise objects to recalculate XmlIgnore values
			if (Context.IsMainPlayer)
			{
				OutdoorPot.ArrangeAll();
			}
			else
			{
				OutdoorPot.ArrangeAllOnNextTick();
			}
		}

		public void LoadContentPacks()
		{
			ModEntry.ItemDefinitions = [];
			ModEntry.Sprites = [];

			List<IContentPack> contentPacks = this.Helper.ContentPacks.GetOwned().ToList();
			foreach (IContentPack contentPack in contentPacks)
			{
				string packKey = contentPack.Manifest.UniqueID;
				var sprites = contentPack.ModContent.Load
					<Texture2D>
					(ItemDefinition.SpritesFile);
				var data = contentPack.ReadJsonFile
					<Dictionary<string, ItemDefinition>>
					(ItemDefinition.DefinitionsFile);

				// For some quality assurance, we check that there are an equal number of entries in the
				// ItemDefinitions dictionary as there are sprites in the shared framework spritesheet.

				const int minWidth = Game1.smallestTileSize * 10;
				const int minHeight = Game1.smallestTileSize * 2;
				if (sprites.Width < minWidth)
				{
					Log.W($"Did not load content pack {packKey}:{Environment.NewLine}Spritesheet does not meet minimum width (required {minWidth}, found {sprites.Width}).");
					continue;
				}
				if (sprites.Height % minHeight != 0)
				{
					Log.W($"While loading content pack {packKey}:{Environment.NewLine}Found spritesheet with unexpected height (expected multiple of {minHeight}, found {sprites.Height}).{Environment.NewLine}Some variants may fail to load.");
				}

				int numberOfSprites = sprites.Height / minHeight;
				string warnMessage = null;

				Log.T($"Loading content pack {packKey}:{Environment.NewLine}{data.Count} item variant entries and {numberOfSprites} spritesheet entries.");

				int difference = Math.Abs(numberOfSprites - data.Count);
				if (difference != 0)
				{
					warnMessage = $"Found {difference} partially-defined garden beds.";
				}

				if (warnMessage is not null)
				{
					Log.W(warnMessage);

					// Remove items until number is within spritesheet bounds
					while (data.Count > numberOfSprites)
					{
						string key = data.Last().Key;
						if (data.Remove(key))
							Log.W($"Removing excess raised bed: {key}");
						else
							Log.E($"Failed to remove excess raised bed: {key}");
					}
				}

				int spriteIndex = 0;
				foreach (var pair in data)
				{
					string localName = pair.Key;
					string variantKey = $"{packKey}.{localName}";
					string itemName = $"{OutdoorPot.GenericName}.{variantKey}";

					// Parse temp values for each entry
					pair.Value.ContentPack = contentPack;
					pair.Value.LocalName = localName;
					pair.Value.VariantName = variantKey;
					pair.Value.ItemName = itemName;
					pair.Value.SpriteKey = packKey;
					pair.Value.SpriteIndex = spriteIndex++;

					// Set default DaysToBreak values to unbreakable
					if (pair.Value.DaysToBreak <= 0)
					{
						pair.Value.DaysToBreak = 999999;
					}

					ModEntry.ItemDefinitions.Add(variantKey, pair.Value);
				}

				// To avoid having to keep many separate spritesheet images updated with any changes,
				// the content pack folder's extra sprite image files required for "ReserveExtraIndexCount"
				// are left blank.
				// We patch the sprites to the game tilesheet in-place where they'd otherwise have appeared,
				// which lets us consolidate all of our sprites into the one framework spritesheet.

				// Patch basic object sprites to game craftables sheet for all variants
				// Compiled sprites are patched in individual regions per sheet index

				// Object sprites are patched in 2 steps, soil and object, since sprites are taken
				// directly from the framework sprite, which stores them separately in order to
				// have the variant's unique soil sprite change when watered.
				if (data.Count > 0)
				{
					IAssetData asset = this.Helper.ModContent.GetPatchHelper(sprites);
					Rectangle destination = Rectangle.Empty;
					Rectangle source;
					int width = Game1.smallestTileSize;
					// soil
					source = new Rectangle(OutdoorPot.SoilIndexInSheet * width, 0, width, width);
					for (int i = 0; i < data.Count; ++i)
					{
						int yOffset = (width * 2 * i) + (width - data[data.Keys.ElementAt(i)].SoilHeightAboveGround);
						destination = new Rectangle(OutdoorPot.PreviewIndexInSheet * width, yOffset, width, width);
						asset.AsImage().PatchImage(
							source: sprites,
							sourceArea: source,
							targetArea: destination,
							patchMode: PatchMode.Overlay);
					}
					// object
					source = new Rectangle(0, 0, width, sprites.Height);
					destination = new Rectangle(destination.X, 0, width, sprites.Height);
					asset.AsImage().PatchImage(
						source: sprites,
						sourceArea: source,
						targetArea: destination,
						patchMode: PatchMode.Overlay);
				}
				ModEntry.Sprites.Add(packKey, sprites);
			}

			Log.T($"Loaded {contentPacks.Count} content pack(s) containing {ModEntry.ItemDefinitions.Count} valid objects.");
		}

		public static void AddDefaultRecipes()
		{
			List<string> recipes = [];
			string precondition = $"{ModEntry.EventRootId}/{ModEntry.EventData[0]["Conditions"]}";
			string rootEventReady = Game1.getFarm().checkEventPrecondition(precondition: precondition, check_seen: true);
			bool hasOrWillSeeRootEvent = Game1.player.eventsSeen.Contains(ModEntry.EventRootId.ToString()) || rootEventReady != "-1";

			foreach (ItemDefinition entry in ModEntry.ItemDefinitions.Values)
			{
				bool isKnown = Game1.player.craftingRecipes.ContainsKey(entry.ItemName);
				bool isDefault = string.IsNullOrEmpty(entry.RecipeConditions);
				bool isAvailable = ModEntry.Config.RecipesAlwaysAvailable || entry.RecipeIsDefault || (hasOrWillSeeRootEvent && isDefault);
				if (!isKnown && isAvailable)
					recipes.Add(entry.ItemName);
			}
			if (recipes.Any())
			{
				foreach (string recipe in recipes)
					Game1.player.craftingRecipes.Add(recipe, 0);
			}
		}

		/// <summary>
		/// Adds new entries to player's crafting recipe dictionary.
		/// No effect if introduction event has not been seen.
		/// </summary>
		/// <param name="variantKeys">List of variant keys of objects from new crafting recipes.</param>
		public static void AddNewAvailableRecipes(out List<string> variantKeys)
		{
			variantKeys = [];

			// Skip if player has not seen introduction event
			if (!Game1.player.eventsSeen.Contains(ModEntry.EventRootId.ToString()))
				return;

			foreach (ItemDefinition entry in ModEntry.ItemDefinitions.Values)
			{
				// Ignore known recipes and recipes with no defined preconditions (these are added by default elsewhere)
				if (Game1.player.craftingRecipes.ContainsKey(entry.ItemName) || string.IsNullOrEmpty(entry.RecipeConditions))
					continue;

				// Add recipes with fulfilled preconditions
				string eventKey = $"{ModEntry.EventRootId}/{entry.RecipeConditions}";
				string precondition = Game1.getFarm().checkEventPrecondition(precondition: eventKey, check_seen: false);
				if (precondition != "-1")
				{
					variantKeys.Add(entry.VariantName);
					Game1.player.craftingRecipes.Add(entry.ItemName, 0);
				}
			}
			return;
		}
	}
}
