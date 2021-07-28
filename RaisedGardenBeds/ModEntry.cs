using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RaisedGardenBeds
{
	public class ModEntry : Mod
	{
		// common
		internal static ModEntry Instance;
		internal static Config Config;

		// definitions
		internal static Dictionary<string, Content.ContentData> ItemDefinitions = null;
		/// <summary>
		/// Shared spritesheet containing object icon, world sprite component, object breakage, and watered/unwatered soil sprites.
		/// </summary>
		internal static Dictionary<string, Texture2D> Sprites = null;
		internal static List<Dictionary<string, string>> EventData = null;

		// others
		internal static int ModUpdateKey;
		internal static int EventRootId => ModUpdateKey * 10000;
		internal const string CommandPrefix = "rgb.";
		internal const string EndOfNightState = "blueberry.rgb.endofnightmenu";


		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<Config>();
			ModUpdateKey = int.Parse(ModManifest.UpdateKeys.First().Split(':')[1]);

			helper.Events.GameLoop.GameLaunched += this.GameLoop_GameLaunched;
		}

		private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked += this.Event_LoadLate;
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			this.SaveLoadedBehaviours();
		}

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			Log.T($"Start of day: Y{Game1.year}/M{1 + Utility.getSeasonNumber(Game1.currentSeason)}/D{Game1.dayOfMonth}");

			// Perform OnSaveLoaded behaviours when starting a new game
			bool isNewGame = Game1.dayOfMonth == 1 && Game1.currentSeason == "spring" && Game1.year == 1;
			if (isNewGame)
			{
				this.SaveLoadedBehaviours();
			}
			
			// Add always-available recipes to player list without any unique fanfare
			AddDefaultRecipes();
		}

		private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			// Break ready objects at the start of each season
			if (Config.RaisedBedsMayBreakWithAge && Game1.dayOfMonth == 28)
			{
				Log.T($"Performing end-of-season breakage: Y{Game1.year}/M{1 + Utility.getSeasonNumber(Game1.currentSeason)}/D{Game1.dayOfMonth}");
				OutdoorPot.BreakAll();
			}
		}

		private void Specialized_LoadStageChanged(object sender, LoadStageChangedEventArgs e)
		{
			if (e.NewStage == StardewModdingAPI.Enums.LoadStage.Loaded && !Context.IsMainPlayer)
			{
				Log.T("Invalidating assets on connected for multiplayer peer.");

				Helper.Content.InvalidateCache(Path.Combine("Data", "BigCraftablesInformation"));
				Helper.Content.InvalidateCache(Path.Combine("Data", "CraftingRecipes"));
				Helper.Content.InvalidateCache(Path.Combine("TileSheets", "Craftables"));
			}
		}

		private void SpaceEvents_ShowNightEndMenus(object sender, SpaceCore.Events.EventArgsShowNightEndMenus e)
		{
			// Add and show any newly-available object recipes to player list at the end of day screens
			List<string> newVarieties = AddNewAvailableRecipes();
			if (newVarieties.Count > 0)
			{
				Log.T($"Unlocked {newVarieties.Count} new recipes:{newVarieties.Aggregate(string.Empty, (str, s) => $"{str}{Environment.NewLine}{s}")}");

				NewRecipeMenu.Push(newVarieties);
			}
		}

		private void Event_LoadLate(object sender, OneSecondUpdateTickedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked -= this.Event_LoadLate;

			if (this.LoadAPIs())
			{
				this.Initialise();
			}
		}

		private bool LoadAPIs()
		{
			Log.T("Loading mod-provided APIs.");
			ISpaceCoreAPI spacecoreAPI = Helper.ModRegistry.GetApi<ISpaceCoreAPI>("spacechase0.SpaceCore");
			if (spacecoreAPI == null)
			{
				// Skip all mod behaviours if we fail to load the objects
				Log.E($"Couldn't access mod-provided API for SpaceCore.{Environment.NewLine}Garden beds will not be available, and no changes will be made.");
				return false;
			}

			spacecoreAPI.RegisterSerializerType(typeof(OutdoorPot));

			return true;
		}

		private void Initialise()
		{
			Log.T("Initialising mod data.");

			// Assets
			AssetManager assetManager = new AssetManager(helper: Helper);
			Helper.Content.AssetLoaders.Add(assetManager);
			Helper.Content.AssetEditors.Add(assetManager);

			// Content
			Translations.LoadTranslationPacks();
			this.LoadContentPacks();
			this.AddGenericModConfigMenu();

			// Patches
			HarmonyPatches.Patch(id: ModManifest.UniqueID);

			// Events
			Helper.Events.Specialized.LoadStageChanged += this.Specialized_LoadStageChanged;
			Helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			Helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;
			Helper.Events.GameLoop.DayEnding += this.GameLoop_DayEnding;
			SpaceCore.Events.SpaceEvents.ShowNightEndMenus += this.SpaceEvents_ShowNightEndMenus;

			// Console commands
			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "eventget",
				documentation: $"Check if event has been seen.{Environment.NewLine}Provide event ID, default to root event.",
				callback: Cmd_IsEventSeen);
			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "eventset",
				documentation: $"Set state for having seen any event.{Environment.NewLine}Provide event ID, default to root event.",
				callback: Cmd_ToggleEventSeen);
			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "give",
				documentation: $"Give several unlocked raised beds.{Environment.NewLine}Has no effect if none are available.",
				callback: Cmd_Give);
			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "giveall",
				documentation: "Give several of all varieties of raised beds.",
				callback: Cmd_GiveAll);
		}

		private void AddGenericModConfigMenu()
		{
			IGenericModConfigMenuAPI modconfigAPI = Helper.ModRegistry.GetApi<IGenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
			if (modconfigAPI != null)
			{
				modconfigAPI.RegisterModConfig(
					mod: ModManifest,
					revertToDefault: () => Config = new Config(),
					saveToFile: () => Helper.WriteConfig(Config));
				modconfigAPI.SetDefaultIngameOptinValue(
					mod: ModManifest,
					optedIn: true);
				System.Reflection.PropertyInfo[] properties = Config
					.GetType()
					.GetProperties()
					.Where(p => p.PropertyType == typeof(bool))
					.ToArray();
				foreach (System.Reflection.PropertyInfo property in properties)
				{
					string key = property.Name.ToLower();
					string description = Translations.GetTranslation($"config.{key}.description");
					modconfigAPI.RegisterSimpleOption(
						mod: ModManifest,
						optionName: Translations.GetTranslation($"config.{key}.name"),
						optionDesc: string.IsNullOrWhiteSpace(description) ? null : description,
						optionGet: () => (bool)property.GetValue(Config),
						optionSet: (bool value) => property.SetValue(Config, value: value));
				}
			}
		}

		private void SaveLoadedBehaviours()
		{
			Log.T($"Adding endOfNightStatus definition: {EndOfNightState}");
			Game1.player.team.endOfNightStatus.AddSpriteDefinition(
				key: EndOfNightState,
				file: AssetManager.GameContentEndOfNightSpritesPath,
				x: 48, y: 0, width: 16, height: 16);

			Game1.content.Load  // Return value unused; event data is set in AssetManager.Edit()
				<Dictionary<string, object>>
				(AssetManager.GameContentEventDataPath);

			// Reinitialise objects to recalculate XmlIgnore values
			if (Context.IsMainPlayer)
			{
				OutdoorPot.AdjustAll(reinitialise: true);
			}
			else
			{
				OutdoorPot.AdjustAllOnNextTick();
			}
		}

		public void LoadContentPacks()
		{
			ItemDefinitions = new Dictionary<string, Content.ContentData>();
			Sprites = new Dictionary<string, Texture2D>();

			List<IContentPack> contentPacks = Helper.ContentPacks.GetOwned().ToList();
			foreach (IContentPack contentPack in contentPacks)
			{
				string packKey = contentPack.Manifest.UniqueID;
				var sprites = contentPack.LoadAsset
					<Texture2D>
					(Content.ContentData.SpritesFile);
				var data = contentPack.ReadJsonFile
					<Dictionary<string, Content.ContentData>>
					(Content.ContentData.DefinitionsFile);

				// For some quality assurance, we check that there are an equal number of entries in the
				// ItemDefinitions dictionary as there are sprites in the shared framework spritesheet.

				const int minWidth = 160;
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

				if (warnMessage != null)
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

				int parentSheetIndex = 0;
				foreach (KeyValuePair<string, Content.ContentData> entry in data)
				{
					string dataKey = $"{packKey}.{entry.Key}";

					// Parse temp values for each entry
					entry.Value.ContentPack = contentPack;
					entry.Value.LocalName = entry.Key;
					entry.Value.SpriteKey = packKey;
					entry.Value.SpriteIndex = parentSheetIndex++;

					ItemDefinitions.Add(dataKey, entry.Value);
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
					IAssetData asset = Helper.Content.GetPatchHelper(sprites);
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
				Sprites.Add(packKey, sprites);
			}

			Log.T($"Loaded {contentPacks.Count} content pack(s) containing {ItemDefinitions.Count} valid objects.");
		}

		public static void AddDefaultRecipes()
		{
			List<string> recipesToAdd = new List<string>();
			int[] eventsSeen = Game1.player.eventsSeen.ToArray();
			string precondition = $"{EventRootId}/{EventData[0]["Conditions"]}";
			int rootEventReady = Game1.getFarm().checkEventPrecondition(precondition);
			bool hasOrWillSeeRootEvent = eventsSeen.Contains(EventRootId) || rootEventReady != -1;
			for (int i = 0; i < ItemDefinitions.Count; ++i)
			{
				string variantKey = ItemDefinitions.Keys.ElementAt(i);
				string craftingRecipeName = OutdoorPot.GenericName + "." + variantKey;
				bool isAlreadyKnown = Game1.player.craftingRecipes.ContainsKey(craftingRecipeName);
				bool isDefaultRecipe = ItemDefinitions[variantKey].RecipeIsDefault;
				bool isInitialEventRecipe = string.IsNullOrEmpty(ItemDefinitions[variantKey].RecipeConditions);
				bool shouldAdd = Config.RecipesAlwaysAvailable || isDefaultRecipe || (hasOrWillSeeRootEvent && isInitialEventRecipe);

				if (!isAlreadyKnown && shouldAdd)
				{
					recipesToAdd.Add(craftingRecipeName);
				}
			}
			if (recipesToAdd.Count > 0)
			{
				Log.T($"Adding {recipesToAdd.Count} default recipes:{recipesToAdd.Aggregate(string.Empty, (str, s) => str + Environment.NewLine + s)}");

				for (int i = 0; i < recipesToAdd.Count; ++i)
				{
					Game1.player.craftingRecipes.Add(recipesToAdd[i], 0);
				}
			}
		}

		public static List<string> AddNewAvailableRecipes()
		{
			List<string> newVariants = new List<string>();
			for (int i = 0; i < ItemDefinitions.Count; ++i)
			{
				string variantKey = ItemDefinitions.Keys.ElementAt(i);
				string itemName = OutdoorPot.GetNameFromVariantKey(variantKey);

				if (Game1.player.craftingRecipes.ContainsKey(itemName)
					|| string.IsNullOrEmpty(ItemDefinitions[variantKey].RecipeConditions)
					|| !Game1.player.eventsSeen.Contains(EventRootId))
				{
					continue;
				}

				int eventID = EventRootId + i;
				string eventKey = $"{eventID.ToString()}/{ItemDefinitions[variantKey].RecipeConditions}";
				int precondition = Game1.getFarm().checkEventPrecondition(eventKey);
				if (precondition != -1)
				{
					newVariants.Add(variantKey);
					Game1.player.craftingRecipes.Add(itemName, 0);
				}
			}
			return newVariants;
		}

		private static void Give(string variantKey, int quantity)
		{
			OutdoorPot item = new OutdoorPot(variantKey: variantKey, tileLocation: Vector2.Zero)
			{
				Stack = quantity
			};
			if (!Game1.player.addItemToInventoryBool(item))
			{
				Log.D($"Inventory full: Did not add {variantKey} raised bed.");
			}
		}

		public static void Cmd_Give(string s, string[] args)
		{
			const int defaultQuantity = 25;
			int quantity = args.Length == 0
				? defaultQuantity
				: int.TryParse(args[0], out int argQuantity)
					? argQuantity
					: defaultQuantity;

			Log.D($"Adding {quantity} of each unlocked raised bed. Use '{CommandPrefix}giveall' to add all varieties.");

			IEnumerable<string> unlockedKeys = Game1.player.craftingRecipes.Keys
				.Where(recipe => recipe.StartsWith(OutdoorPot.GenericName));
			foreach (string variantKey in unlockedKeys)
			{
				Give(variantKey: variantKey, quantity: quantity);
			}
		}

		public static void Cmd_GiveAll(string s, string[] args)
		{
			const int defaultQuantity = 25;
			int quantity = args.Length == 0
				? defaultQuantity
				: int.TryParse(args[0], out int argQuantity)
					? argQuantity
					: defaultQuantity;

			if (Game1.player.craftingRecipes.Keys.Count(r => r.StartsWith(OutdoorPot.GenericName)) < 1)
			{
				Log.D($"No raised bed recipes are unlocked! Use '{CommandPrefix}giveall' to add all varieties.");
				return;
			}

			Log.D($"Adding {quantity} of all raised beds. Use '{CommandPrefix}give' to add unlocked varieties only.");

			foreach (string variantKey in ItemDefinitions.Keys)
			{
				Give(variantKey: variantKey, quantity: quantity);
			}
		}

		public static void Cmd_IsEventSeen(string s, string[] args)
		{
			int eventId = args.Length > 0 && int.TryParse(args[0], out int argId)
				? argId
				: EventRootId;
			string msg = $"Player {(Game1.player.eventsSeen.Contains(eventId) ? "has" : "has not")} seen event {eventId}.";
			Log.D(msg);
		}

		public static void Cmd_ToggleEventSeen(string s, string[] args)
		{
			int eventId = args.Length > 0 && int.TryParse(args[0], out int argId)
				? argId
				: EventRootId;
			if (Game1.player.eventsSeen.Contains(eventId))
			{
				Game1.player.eventsSeen.Remove(eventId);
			}
			else
			{
				Game1.player.eventsSeen.Add(eventId);
			}
			Cmd_IsEventSeen(s: s, args: args);
		}
	}
}
