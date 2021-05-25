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
		public static void Patch(string id)
		{
			HarmonyInstance harmony = HarmonyInstance.Create(id);

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

		/// <summary>
		/// Replace logic for choosing whether objects can be placed into a custom garden bed.
		/// </summary>
		public static void GameLocation_IsTileOccupiedForPlacement_Postfix(
			GameLocation __instance,
			ref bool __result,
			Vector2 tileLocation,
			StardewValley.Object toPlace)
		{
			__instance.Objects.TryGetValue(tileLocation, out StardewValley.Object o);
			if (toPlace != null && (toPlace.Category == -74 || toPlace.Category == -19) && o != null && o is OutdoorPot op
				&& op.hoeDirt.Value.canPlantThisSeedHere(toPlace.ParentSheetIndex, (int)tileLocation.X, (int)tileLocation.Y, toPlace.Category == -19)
				&& op.bush.Value == null)
			{
				__result = false;
			}
		}

		/// <summary>
		/// Replace crafting recipe hover logic to instead show garden bed display names for each variant.
		/// </summary>
		internal static void CraftingPage_PerformHoverAction_Postfix(
			CraftingRecipe ___hoverRecipe)
		{
			// Add display name in crafting pages
			if (___hoverRecipe == null)
				return;
			if (___hoverRecipe.name.StartsWith(ModEntry.ItemName))
				___hoverRecipe.DisplayName = OutdoorPot.GetDisplayNameFromVariantKey(___hoverRecipe.name);
		}

		/// <summary>
		/// Replace logic for crafting objects in base game crafting menu to create the appropriate garden bed for the crafting recipe variant.
		/// </summary>
		internal static bool CraftingPage_ClickCraftingRecipe_Prefix(
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
				if (!recipe.name.StartsWith(ModEntry.ItemName))
					return true;

				OutdoorPot item = new OutdoorPot(
					variant: ModEntry.ItemDefinitions.Keys.ToList()
						.IndexOf(ModEntry.ItemDefinitions.First(kvp => kvp.Value["RecipeItems"].Split(' ')[0] == recipe.recipeList.Keys.ToList()[0].ToString()).Key),
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
				Log.E(ModEntry.Instance.ModManifest.UniqueID + " failed in " + nameof(CraftingPage_ClickCraftingRecipe_Prefix) + "\n" + e);
				return true;
			}
		}

		/// <summary>
		/// Replace logic determining sprite index to draw from in crafting page to instead choose the garden bed variant sprite for this recipe.
		/// </summary>
		internal static void CraftingRecipe_GetIndexOfMenuView_Postfix(
			CraftingRecipe __instance,
			ref int __result)
		{
			if (__instance.name.StartsWith(ModEntry.ItemName))
			{
				__result = OutdoorPot.GetParentSheetIndexFromName(name: __instance.name);
			}
		}
	}
}
