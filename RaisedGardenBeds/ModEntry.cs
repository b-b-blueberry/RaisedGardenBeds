using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RaisedGardenBeds
{
	public class ModEntry : Mod, IAssetEditor
	{
		internal static ModEntry Instance;
		internal static Config Config;
		internal static IJsonAssetsApi JsonAssets;
		internal ITranslationHelper i18n => Helper.Translation;

		// assets
		internal const string ItemName = "blueberry.rgb.raisedbed";
		internal static readonly string ContentPackPath = Path.Combine("assets", "ContentPack");
		internal static readonly string ItemSpritesPath = Path.Combine("assets", "sprites");
		// others
		internal const string CommandPrefix = "rgb";


		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<Config>();
			OutdoorPot.Sprites = helper.Content.Load<Texture2D>(ItemSpritesPath);
			HarmonyPatches.Patch(id: ModManifest.UniqueID);

			helper.Events.GameLoop.GameLaunched += this.GameLoop_GameLaunched;
			helper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded;
			helper.Events.GameLoop.DayEnding += this.GameLoop_DayEnding;
			helper.ConsoleCommands.Add(name: CommandPrefix + "give", "Drop some raised bed items.", Give);
			helper.ConsoleCommands.Add(name: CommandPrefix + "prebreak", "Mark all raised beds as ready to break.", Mark);
		}

		private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked += this.Event_LoadApisOnNextTick;
		}

		private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			OutdoorPot.AdjustAll();
		}

		private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
			if (Config.RaisedBedsBreakWithAge && Game1.dayOfMonth == 28)
			{
				OutdoorPot.BreakAll();
			}
		}

		private void Event_LoadApisOnNextTick(object sender, OneSecondUpdateTickedEventArgs e)
		{
			Helper.Events.GameLoop.OneSecondUpdateTicked -= this.Event_LoadApisOnNextTick;

			this.LoadApis();
		}

		private void LoadApis()
		{
			// SpaceCore
			ISpaceCoreAPI spacecoreApi = Helper.ModRegistry.GetApi<ISpaceCoreAPI>("spacechase0.SpaceCore");
			spacecoreApi.RegisterSerializerType(typeof(OutdoorPot));

			// Json Assets
			JsonAssets = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
			if (JsonAssets == null)
			{
				Log.E("Can't access the Json Assets API. Is the mod installed correctly?");
				return;
			}
			JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, ContentPackPath));
		}

		public static void Give(string s, string[] args)
		{
			const int defaultQuantity = 25;
			int quantity = args.Length == 0 ? defaultQuantity : int.TryParse(args[0], out int argQuantity) ? argQuantity : defaultQuantity;
			OutdoorPot item = new OutdoorPot()
			{
				Stack = quantity
			};
			if (!Game1.player.addItemToInventoryBool(item))
			{
				Game1.createItemDebris(item: item, origin: Game1.player.Position, direction: -1);
			}
		}

		public static void Mark(string s, string[] args)
		{
			foreach (OutdoorPot o in Game1.getFarm().Objects.Values.Where(o => o is OutdoorPot p && p.MinutesUntilReady > 0).Cast<OutdoorPot>())
			{
				o.MinutesUntilReady = OutdoorPot.BreakageTarget;
			}
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			return (JsonAssets != null && !(Game1.activeClickableMenu is StardewValley.Menus.TitleMenu)
				&& (asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables"))
					|| asset.AssetNameEquals(Path.Combine("Data", "BigCraftablesInformation"))));
		}

		public void Edit<T>(IAssetData asset)
		{
			if (asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables")))
			{
				// Patch complete shared spritesheet sprite to game craftables sheet
				Rectangle destination = StardewValley.Object.getSourceRectForBigCraftable(JsonAssets.GetBigCraftableId(ItemName));
				Rectangle source;
				// soil
				source = new Rectangle(Game1.smallestTileSize * OutdoorPot.SoilIndexInSheet, 0, Game1.smallestTileSize, Game1.smallestTileSize);
				asset.AsImage().PatchImage(
					source: OutdoorPot.Sprites,
					sourceArea: source,
					targetArea: new Rectangle(destination.X, destination.Y + OutdoorPot.SoilHeightFromGround, source.Width, source.Height),
					patchMode: PatchMode.Overlay);
				// object
				source = new Rectangle(0, 0, destination.Width, destination.Height);
				asset.AsImage().PatchImage(
					source: OutdoorPot.Sprites,
					sourceArea: source,
					targetArea: destination,
					patchMode: PatchMode.Overlay);
				return;
			}
			if (asset.AssetNameEquals(Path.Combine("Data", "BigCraftablesInformation")))
			{
				// Patch display name and description from localisations file
				string[] fields = asset.AsDictionary<int, string>().Data[JsonAssets.GetBigCraftableId(ItemName)].Split('/');
				fields[4] = i18n.Get("item.description");
				fields[8] = i18n.Get("item.name");
				asset.AsDictionary<int, string>().Data[JsonAssets.GetBigCraftableId(ItemName)] = string.Join("/", fields);
				return;
			}
		}
	}
}
