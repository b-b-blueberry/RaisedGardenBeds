using Harmony; // el diavolo
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Linq;

namespace RaisedGardenBeds
{
	public static class HarmonyPatches
	{
		internal static void Patch(string id)
		{
			HarmonyInstance harmony = HarmonyInstance.Create(id);

			Log.T(typeof(HarmonyPatches).GetMethods().Take(typeof(HarmonyPatches).GetMethods().Count() - 4).Select(mi => mi.Name)
				.Aggregate("Applying Harmony patches:", (str, s) => str + Environment.NewLine + s));

			// Utility
			harmony.Patch(
				original: AccessTools.Method(typeof(Utility), "isThereAnObjectHereWhichAcceptsThisItem"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Utility_IsThereAnObjectHereWhichAcceptsThisItem_Prefix)));
			harmony.Patch(
				original: AccessTools.Method(typeof(Utility), "isViableSeedSpot"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Utility_IsViableSeedSpot_Prefix)));

			// Object
			harmony.Patch(
				original: AccessTools.Method(typeof(StardewValley.Object), "ApplySprinkler"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_ApplySprinkler_Prefix)));

			// GameLocation
			harmony.Patch(
				original: AccessTools.Method(typeof(GameLocation), "isTileOccupiedForPlacement"),
				postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_IsTileOccupiedForPlacement_Postfix)));

			// Crafting
			harmony.Patch(
				original: AccessTools.Method(typeof(CraftingPage), "performHoverAction"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingPage_PerformHoverAction_Postfix)));
			harmony.Patch(
				original: AccessTools.Method(typeof(CraftingPage), "clickCraftingRecipe"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingPage_ClickCraftingRecipe_Prefix)));
			harmony.Patch(
				original: AccessTools.Method(typeof(CraftingRecipe), "getIndexOfMenuView"),
				postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingRecipe_GetIndexOfMenuView_Postfix)));
		}

		private static void ErrorHandler(Exception e)
		{
			Log.E($"{ModEntry.Instance.ModManifest.UniqueID} failed in harmony prefix.{Environment.NewLine}{e}");
		}

		public static bool Utility_IsThereAnObjectHereWhichAcceptsThisItem_Prefix(
			ref bool __result,
			GameLocation location,
			Item item,
			int x,
			int y)
		{
			try
			{
				Vector2 tileLocation = new Vector2(x / Game1.tileSize, y / Game1.tileSize);
				if (location.Objects.TryGetValue(tileLocation, out StardewValley.Object o) && o != null && o is OutdoorPot op)
				{
					if (!OutdoorPot.IsItemPlantable(item) && op.IsOpenForPlacement())
					{
						__result = op.performObjectDropInAction(dropInItem: (StardewValley.Object)item, probe: true, who: Game1.player);
					}
					else
					{
						__result = false;
					}
					return false;
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}

		public static bool Utility_IsViableSeedSpot_Prefix(
			GameLocation location,
			Vector2 tileLocation,
			Item item)
		{
			try
			{
				if (location.Objects.TryGetValue(tileLocation, out StardewValley.Object o) && o != null && o is OutdoorPot op)
				{
					if (OutdoorPot.IsItemPlantable(item) && op.CanPlantHere(item) && op.IsOpenForPlacement())
					{
						return true;
					}
					return false;
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}

		public static bool Object_ApplySprinkler_Prefix(
			GameLocation location,
			Vector2 tile)
		{
			try
			{
				if (ModEntry.Config.SprinklersEnabled
					&& location.Objects.TryGetValue(tile, out StardewValley.Object o) && o != null && o is OutdoorPot op)
				{
					if (op.IsOpenForPlacement(ignoreCrops: true))
					{
						op.Water();
					}
					return false;
				}
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}

		/// <summary>
		/// Replace logic for choosing whether objects can be placed into a custom garden bed.
		/// </summary>
		public static void GameLocation_IsTileOccupiedForPlacement_Postfix(
			GameLocation __instance,
			ref bool __result,
			Vector2 tileLocation,
			StardewValley.Object toPlace)
		{
			if (__instance.Objects.TryGetValue(tileLocation, out StardewValley.Object o) && o != null && o is OutdoorPot op)
			{
				bool isPlantable = OutdoorPot.IsItemPlantable(toPlace)
					&& op.hoeDirt.Value.canPlantThisSeedHere(toPlace.ParentSheetIndex, (int)tileLocation.X, (int)tileLocation.Y, toPlace.Category == -19);
				if (op.IsOpenForPlacement() && isPlantable)
				{
					__result = false;
				}
			}
		}

		/// <summary>
		/// Replace crafting recipe hover logic to instead show garden bed display names for each variant.
		/// </summary>
		public static void CraftingPage_PerformHoverAction_Postfix(
			CraftingRecipe ___hoverRecipe)
		{
			// Add display name in crafting pages
			if (___hoverRecipe == null)
				return;
			if (___hoverRecipe.name.StartsWith(OutdoorPot.GenericName))
				___hoverRecipe.DisplayName = OutdoorPot.GetDisplayNameFromName(___hoverRecipe.name);
		}

		/// <summary>
		/// Replace logic for crafting objects in base game crafting menu to create the appropriate garden bed for the crafting recipe variant.
		/// </summary>
		public static bool CraftingPage_ClickCraftingRecipe_Prefix(
			CraftingPage __instance,
			int ___currentCraftingPage,
			ref Item ___heldItem,
			ClickableTextureComponent c,
			bool playSound = true)
		{
			try
			{
				// Fetch an instance of any clicked-on craftable in the crafting menu
				CraftingRecipe recipe = __instance.pagesOfCraftingRecipes[___currentCraftingPage][c];

				// Fall through to default method for any other craftables
				if (!recipe.name.StartsWith(OutdoorPot.GenericName))
					return true;

				OutdoorPot item = new OutdoorPot(
					variantKey: OutdoorPot.GetVariantKeyFromName(recipe.name),
					tileLocation: Vector2.Zero);

				// Behaviours as from base method
				recipe.consumeIngredients(null);
				if (playSound)
					Game1.playSound("coin");
				if (___heldItem == null)
				{
					___heldItem = item;
				}
				else if (___heldItem.canStackWith(item))
				{
					___heldItem.addToStack(item);
				}
				if (Game1.player.craftingRecipes.ContainsKey(recipe.name))
					Game1.player.craftingRecipes[recipe.name] += recipe.numberProducedPerCraft;
				Game1.stats.checkForCraftingAchievements();
				if (Game1.options.gamepadControls && ___heldItem != null && Game1.player.couldInventoryAcceptThisItem(___heldItem))
				{
					Game1.player.addItemToInventoryBool(___heldItem);
					___heldItem = null;
				}

				return false;
			}
			catch (Exception e)
			{
				HarmonyPatches.ErrorHandler(e);
			}
			return true;
		}

		/// <summary>
		/// Replace logic determining sprite index to draw from in crafting page to instead choose the garden bed variant sprite for this recipe.
		/// </summary>
		public static void CraftingRecipe_GetIndexOfMenuView_Postfix(
			CraftingRecipe __instance,
			ref int __result)
		{
			if (__instance.name.StartsWith(OutdoorPot.GenericName))
			{
				__result = OutdoorPot.GetParentSheetIndexFromName(name: __instance.name);
			}
		}
	}
}
