using HarmonyLib; // el diavolo nuevo
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaisedGardenBeds
{
	public static class HarmonyPatches
	{
		internal static void Patch(string id)
		{
			Harmony harmony = new Harmony(id);

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
				original: AccessTools.Method(typeof(CraftingPage), "layoutRecipes"),
				postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingPage_LayoutRecipes_Postfix)));
			harmony.Patch(
				original: AccessTools.Method(typeof(CraftingPage), "clickCraftingRecipe"),
				prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingPage_ClickCraftingRecipe_Prefix)));
			/*
			harmony.Patch(
				original: AccessTools.Method(typeof(CraftingRecipe), "drawMenuView"),
				postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CraftingRecipe_DrawMenuView_Postfix)));
			*/

			// DISPLAY NAME TRANSPILER
			/*
			var sub = AccessTools.Constructor(type: typeof(CraftingRecipe), parameters: new Type[] { typeof(string), typeof(bool) });
			var dom = new HarmonyMethod(typeof(HarmonyPatches), nameof(Transpile_CraftingRecipeConstructor));
			harmony.Patch(
				original: sub,
				transpiler: dom
			);
			*/
		}

		private static void ErrorHandler(Exception e)
		{
			Log.E($"{ModEntry.Instance.ModManifest.UniqueID} failed in harmony prefix.{Environment.NewLine}{e}");
		}
		/*
		public static IEnumerable<CodeInstruction> Transpile_CraftingRecipeConstructor(ILGenerator gen, MethodBase original, IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction i in instructions)
			{
				if (i.opcode != OpCodes.Call || !(i.operand is MethodInfo method && method.Name == "get_CurrentLanguageCode"))
				{
					yield return i;
				}
				else
				{
					// Original: Call LocalizedContentManager.get_CurrentLanguageCode()
					// Goal: Call HarmonyPatches.CheckDisplayName(int, bool)

					yield return new CodeInstruction(OpCodes.Ldloc_1, null) { labels = i.labels };	// infoSplit
					yield return new CodeInstruction(OpCodes.Ldlen, null);      // Length
					yield return new CodeInstruction(OpCodes.Conv_I4, null);    // (int)

					yield return new CodeInstruction(OpCodes.Ldarg_2, null);    // isCookingRecipe

					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HarmonyPatches), nameof(CheckDisplayName)));
				}
			}
		}

		public static int CheckDisplayName(int infoSplitLength, bool isCookingRecipe)
		{
			return infoSplitLength < (isCookingRecipe ? 5 : 6) ? 0 : 1;
		}
		*/
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
		/// Required to draw correct object sprites and strings in crafting menu.
		/// Event handlers on StardewModdingAPI.Events.Display.MenuChanged were inconsistent.
		/// </summary>
		public static void CraftingPage_LayoutRecipes_Postfix(
			CraftingPage __instance)
		{
			__instance.pagesOfCraftingRecipes
				.ForEach(dict => dict
					.Where(pair => pair.Value.name.StartsWith(OutdoorPot.GenericName))
					.ToList()
					.ForEach(pair =>
					{
						string variantKey = OutdoorPot.GetVariantKeyFromName(name: pair.Value.name);

						// Sprite
						pair.Key.texture = ModEntry.Sprites[ModEntry.ItemDefinitions[variantKey].SpriteKey];
						pair.Key.sourceRect = OutdoorPot.GetSourceRectangle(spriteIndex: ModEntry.ItemDefinitions[variantKey].SpriteIndex);

						// Strings
						pair.Value.DisplayName = OutdoorPot.GetDisplayNameFromName(pair.Value.name);
						pair.Value.description = OutdoorPot.GetRawDescription();
					}));
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
		/*
		/// <summary>
		/// 
		/// </summary>
		public static void CraftingRecipe_DrawMenuView_Postfix(
			CraftingRecipe __instance,
			Microsoft.Xna.Framework.Graphics.SpriteBatch b,
			int x,
			int y,
			float layerDepth)
		{
			if (__instance.name.StartsWith(OutdoorPot.GenericName))
			{
				string variantKey = OutdoorPot.GetVariantKeyFromName(name: __instance.name);
				Utility.drawWithShadow(
					b,
					texture: ModEntry.Sprites[ModEntry.ItemDefinitions[variantKey].SpriteKey],
					position: new Vector2(x, y),
					sourceRect: OutdoorPot.GetSourceRectangle(spriteIndex: ModEntry.ItemDefinitions[variantKey].SpriteIndex),
					color: Color.White,
					rotation: 0f,
					origin: Vector2.Zero,
					scale: Game1.pixelZoom,
					flipped: false,
					layerDepth: layerDepth);
			}
		}
		*/
	}
}
