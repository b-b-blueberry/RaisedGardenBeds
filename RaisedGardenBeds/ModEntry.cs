using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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
		internal static IJsonAssetsApi JsonAssets;
		internal ITranslationHelper i18n => Helper.Translation;

		// definitions
		internal static Dictionary<string, Dictionary<string, string>> ItemDefinitions = null;
		internal static List<Dictionary<string, string>> EventData = null;

		// others
		internal const string CommandPrefix = "rgb";
		internal static int ModUpdateKey = -1;
		internal static int EventRoot => ModUpdateKey * 10000;
		internal const string EndOfNightState = "blueberry.rgb.endofnightmenu";


		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<Config>();
			ModUpdateKey = int.Parse(ModManifest.UpdateKeys.First().Split(':')[1]);

			Log.W($"Loading {ModManifest.UniqueID} pre-release candidate: {ModManifest.Version}");

			helper.Events.GameLoop.GameLaunched += this.GameLoop_GameLaunched;
		}

		private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked += this.Event_LoadLate;
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			Game1.player.team.endOfNightStatus.AddSpriteDefinition(EndOfNightState, "LooseSprites\\PlayerStatusList", 48, 0, 16, 16);
			Game1.content.Load<Dictionary<string, object>>(AssetManager.GameContentEventDataPath);

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

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			// Invalidate sprites at the start of each day in case patches target the asset
			OutdoorPot.ReloadSprites();

			// Add always-available recipes to player list without any unique fanfare
			AddDefaultRecipes();
		}

		private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			// Break ready objects at the start of each season
			if (Config.RaisedBedsMayBreakWithAge && Game1.dayOfMonth == 28)
			{
				OutdoorPot.BreakAll();
			}
		}

		private void Specialized_LoadStageChanged(object sender, LoadStageChangedEventArgs e)
		{
			if (e.NewStage == StardewModdingAPI.Enums.LoadStage.Loaded && !Context.IsMainPlayer)
			{
				Helper.Content.InvalidateCache(Path.Combine("Data", "BigCraftablesInformation"));
				Helper.Content.InvalidateCache(Path.Combine("Data", "CraftingRecipes"));
				Helper.Content.InvalidateCache(Path.Combine("TileSheets", "Craftables"));
			}
		}

		private void SpaceEvents_ShowNightEndMenus(object sender, SpaceCore.Events.EventArgsShowNightEndMenus e)
		{
			// Add and show any newly-available object recipes to player list at the end of day screens
			NewRecipeMenu.Push(AddNewAvailableRecipes());
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
			JsonAssets = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
			ISpaceCoreAPI spacecoreAPI = Helper.ModRegistry.GetApi<ISpaceCoreAPI>("spacechase0.SpaceCore");
			if (JsonAssets == null || spacecoreAPI == null)
			{
				// Skip all mod behaviours if we fail to load the objects
				Log.E("Couldn't access mod-provided APIs.\nGarden beds will not be available, and no changes will be made.");
				return false;
			}

			spacecoreAPI.RegisterSerializerType(typeof(OutdoorPot));
			JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, AssetManager.ContentPackPath));

			// Add entries for optional mod config menu support
			IGenericModConfigMenuAPI modconfigAPI = Helper.ModRegistry.GetApi<IGenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
			if (modconfigAPI != null)
			{
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
			return true;
		}

		private void Initialise()
		{
			AssetManager assetManager = new AssetManager(helper: Helper);
			Helper.Content.AssetLoaders.Add(assetManager);
			Helper.Content.AssetEditors.Add(assetManager);

			OutdoorPot.ReloadSprites();
			ItemDefinitions = Game1.content.Load<Dictionary<string, Dictionary<string, string>>>(AssetManager.GameContentItemDefinitionsPath);

			HarmonyPatches.Patch(id: ModManifest.UniqueID);

			Helper.Events.Specialized.LoadStageChanged += this.Specialized_LoadStageChanged;
			Helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			Helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;
			Helper.Events.GameLoop.DayEnding += this.GameLoop_DayEnding;

			SpaceCore.Events.SpaceEvents.ShowNightEndMenus += this.SpaceEvents_ShowNightEndMenus;

			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "give",
				documentation: "Give several unlocked raised beds.\nHas no effect if none are available.\n",
				callback: Cmd_Give);
			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "giveall",
				documentation: "Give several of all varieties of raised beds.",
				callback: Cmd_GiveAll);

			/*
			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "prebreak",
				documentation: "Mark all raised beds as ready to break.",
				callback: Cmd_Prebreak);
			Helper.ConsoleCommands.Add(
				name: CommandPrefix + "checkdirt",
				documentation: "Check whether garden bed under cursor is watered.",
				callback: Cmd_CheckDirt);
			*/
		}

		public static void AddDefaultRecipes()
		{
			List<string> recipesToAdd = new List<string>();
			int[] eventsSeen = Game1.player.eventsSeen.ToArray();
			string precondition = $"{EventRoot}/{EventData[0]["Conditions"]}";
			int rootEventReady = Game1.getFarm().checkEventPrecondition(precondition);
			bool hasOrWillSeeRootEvent = eventsSeen.Contains(EventRoot)
				|| rootEventReady != -1;
			for (int i = 0; i < ItemDefinitions.Count; ++i)
			{
				string varietyName = ItemDefinitions.Keys.ElementAt(i);
				string craftingRecipeName = OutdoorPot.GenericName + "." + varietyName;
				bool isAlreadyKnown = Game1.player.craftingRecipes.ContainsKey(craftingRecipeName);
				bool isDefaultRecipe = bool.Parse(ItemDefinitions[varietyName]["RecipeIsDefault"]);
				bool isInitialEventRecipe = string.IsNullOrEmpty(ItemDefinitions[varietyName]["RecipeConditions"]);

				if (!isAlreadyKnown && (Config.RecipesAlwaysAvailable || isDefaultRecipe || (hasOrWillSeeRootEvent && isInitialEventRecipe)))
				{
					recipesToAdd.Add(craftingRecipeName);
				}
			}
			for (int i = 0; i < recipesToAdd.Count; ++i)
			{
				Game1.player.craftingRecipes.Add(recipesToAdd[i], 0);
			}
		}

		public static List<string> AddNewAvailableRecipes()
		{
			List<string> newVarieties = new List<string>();
			for (int i = 0; i < ItemDefinitions.Count; ++i)
			{
				string varietyName = ItemDefinitions.Keys.ElementAt(i);
				string itemName = OutdoorPot.GetNameFromVariantKey(varietyName);

				if (Game1.player.craftingRecipes.ContainsKey(itemName)
					|| string.IsNullOrEmpty(ItemDefinitions[varietyName]["RecipeConditions"])
					|| !Game1.player.eventsSeen.Contains(EventRoot)
					)
					continue;

				int eventID = EventRoot + i;
				string eventKey = eventID.ToString() + "/" + ItemDefinitions[varietyName]["RecipeConditions"];
				int precondition = Game1.getFarm().checkEventPrecondition(eventKey);
				if (precondition != -1)
				{
					newVarieties.Add(varietyName);
					Game1.player.craftingRecipes.Add(itemName, 0);
				}
			}
			return newVarieties;
		}

		private static void Give(string variantKey, int quantity)
		{
			OutdoorPot item = new OutdoorPot(variant: OutdoorPot.GetVariantIndexFromName(variantKey), tileLocation: Vector2.Zero)
			{
				Stack = quantity
			};
			if (!Game1.player.addItemToInventoryBool(item))
			{
				Log.D("Inventory full: Did not add " + variantKey + " raised bed.");
			}
		}

		public static void Cmd_Give(string s, string[] args)
		{
			const int defaultQuantity = 25;
			int quantity = args.Length == 0 ? defaultQuantity : int.TryParse(args[0], out int argQuantity) ? argQuantity : defaultQuantity;
			IEnumerable<string> unlockedKeys = Game1.player.craftingRecipes.Keys.Where(recipe => recipe.StartsWith(OutdoorPot.GenericName));
			foreach (string variantKey in unlockedKeys)
			{
				Give(variantKey: variantKey, quantity: quantity);
			}
		}

		public static void Cmd_GiveAll(string s, string[] args)
		{
			const int defaultQuantity = 25;
			int quantity = args.Length == 0 ? defaultQuantity : int.TryParse(args[0], out int argQuantity) ? argQuantity : defaultQuantity;
			foreach (string variantKey in ItemDefinitions.Keys)
			{
				Give(variantKey: variantKey, quantity: quantity);
			}
		}


		/*
		public static void Cmd_Prebreak(string s, string[] args)
		{
			if (!Config.RaisedBedsMayBreakWithAge)
			{
				Log.D("Breakage is disabled in mod config file!");
				return;
			}
			foreach (OutdoorPot o in Game1.currentLocation.Objects.Values.OfType<OutdoorPot>().Where(o => o.BreakageTimer.Value > 0))
			{
				o.BreakageTimer.Value = 1;
			}
		}

		private static void Cmd_CheckDirt(string arg1, string[] arg2)
		{
			Vector2 tile = Game1.currentCursorTile;
			Game1.currentLocation.Objects.TryGetValue(tile, out StardewValley.Object o);
			if (o != null && o is OutdoorPot op)
			{
				Log.D($"RGB.OP.HD at {tile} == {op.hoeDirt.Value.state.Value} (sprite: {op.showNextIndex.Value})"
					+ $"\nBreakage: {op.BreakageTimer.Value} / {op.BreakageStart.Value}");
				if (false)
				{
					int breakTo = 1;
					Log.D($"Set breakage timer to {breakTo}.");
					op.BreakageTimer.Value = breakTo;
				}
				return;
			}

			Log.D($"No RGB.OP at {tile}.");
		}
		*/
	}
}
