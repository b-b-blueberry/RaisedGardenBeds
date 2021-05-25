using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RaisedGardenBeds
{
	// TODO: FEATURE: Look into sprinkler radius

	public class ModEntry : Mod
	{
		// common
		internal static ModEntry Instance;
		internal static Config Config;
		internal static IJsonAssetsApi JsonAssets;
		internal ITranslationHelper i18n => Helper.Translation;

		// definitions
		internal static Dictionary<string, Dictionary<string, string>> ItemDefinitions = null;
		internal static Dictionary<string, string> EventData = null;
		internal static List<Dictionary<string, string>> Events = null;

		// others
		internal const string ItemName = "blueberry.rgb.raisedbed";
		internal const string CommandPrefix = "rgb";
		internal static int ModUpdateKey = -1;
		internal static int EventRoot => ModUpdateKey * 10000;


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
			if (Context.IsMainPlayer)
			{
				// Reinitialise objects to recalculate XmlIgnore values
				OutdoorPot.AdjustAll(reinitialise: true);
			}
		}

		private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			// Invalidate sprites at the start of each day in case patches target the asset
			OutdoorPot.ReloadSprites();
		}

		private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			if (Config.RaisedBedsMayBreakWithAge && Game1.dayOfMonth == 28)
			{
				OutdoorPot.BreakAll();
			}

			// Add any newly-available object recipes to player list
			NewRecipeMenu.Push(GetNewRecipes());
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

			Helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			Helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;
			Helper.Events.GameLoop.DayEnding += this.GameLoop_DayEnding;

			Helper.ConsoleCommands.Add(name: CommandPrefix + "give", "Drop some raised bed items.", Cmd_Give);
			Helper.ConsoleCommands.Add(name: CommandPrefix + "prebreak", "Mark all raised beds as ready to break.", Cmd_Prebreak);
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
	}
}
