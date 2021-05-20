using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Locations;
using StardewValley.Tools;
using StardewModdingAPI;
using Microsoft.Xna.Framework;
using StardewValley.Menus;
using Harmony; // el diavolo

namespace RaisedGardenBeds
{
	public static class HarmonyPatches
	{
		public static void Patch(string id)
		{
			HarmonyInstance harmony = HarmonyInstance.Create(id);
			// GameLocation
			harmony.Patch(
				original: AccessTools.Method(typeof(StardewValley.GameLocation), "isTileOccupiedForPlacement"),
				postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_IsTileOccupiedForPlacement_Postfix)));
			// Crafting
			harmony.Patch(
				original: AccessTools.Method(typeof(CraftingPage), "performHoverAction"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingPage_PerformHoverAction_Postfix)));
			harmony.Patch(
				original: AccessTools.Method(typeof(CraftingPage), "clickCraftingRecipe"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingPage_ClickCraftingRecipe_Prefix)));
		}

		public static void GameLocation_IsTileOccupiedForPlacement_Postfix(
			StardewValley.GameLocation __instance,
			ref bool __result,
			Vector2 tileLocation, StardewValley.Object toPlace)
		{
			__instance.Objects.TryGetValue(tileLocation, out StardewValley.Object o);
			if (toPlace != null && (toPlace.Category == -74 || toPlace.Category == -19) && o != null && o is OutdoorPot)
			{
				if ((o as OutdoorPot).hoeDirt.Value.canPlantThisSeedHere(toPlace.ParentSheetIndex, (int)tileLocation.X, (int)tileLocation.Y, toPlace.Category == -19) && (o as OutdoorPot).bush.Value == null)
				{
					__result = false;
				}
			}
		}

		internal static void CraftingPage_PerformHoverAction_Postfix(CraftingRecipe ___hoverRecipe)
		{
			// Add display name in crafting pages
			if (___hoverRecipe == null)
				return;
			if (___hoverRecipe.name.StartsWith(ModEntry.ItemName))
				___hoverRecipe.DisplayName = ModEntry.Instance.i18n.Get("item.name");
		}

		internal static bool CraftingPage_ClickCraftingRecipe_Prefix(
			CraftingPage __instance,
			int ___currentCraftingPage, Item ___heldItem,
			ClickableTextureComponent c, bool playSound = true)
		{
			try
			{
				// Fetch an instance of any clicked-on craftable in the crafting menu
				CraftingRecipe recipe = __instance.pagesOfCraftingRecipes[___currentCraftingPage][c];
				Item tempItem = recipe.createItem();

				// Fall through to default method for any other craftables
				if (!tempItem.Name.StartsWith(ModEntry.ItemName))
					return true;

				// Behaviours as from base method
				if (___heldItem == null)
				{
					recipe.consumeIngredients(null);
					___heldItem = tempItem;
					if (playSound)
						Game1.playSound("coin");
				}
				if (Game1.player.craftingRecipes.ContainsKey(recipe.name))
					Game1.player.craftingRecipes[recipe.name] += recipe.numberProducedPerCraft;
				if (___heldItem == null || !Game1.player.couldInventoryAcceptThisItem(___heldItem))
					return false;

				// Add the machine to the user's inventory
				OutdoorPot item = new OutdoorPot();
				if (Game1.player.addItemToInventoryBool(item))
					___heldItem = null;
				return false;
			}
			catch (Exception e)
			{
				Log.E(ModEntry.Instance.ModManifest.UniqueID + " failed in " + nameof(CraftingPage_ClickCraftingRecipe_Prefix) + "\n" + e);
				return true;
			}
		}
	}
}
