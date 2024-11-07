using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib; // el diavolo nuevo
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace RaisedGardenBeds
{
	public static class HarmonyPatches
	{
		internal class PatchTemplate(HarmonyPatchType type, MethodInfo original, string patch = null, HarmonyMethod method = null)
		{
			public readonly HarmonyPatchType type = type;
			public readonly MethodInfo original = original;
			public readonly string patch = patch ?? method.methodName;
			public readonly HarmonyMethod method = method ?? new HarmonyMethod(
				methodType: typeof(HarmonyPatches),
				methodName: patch);
		}

		internal static void Patch(string id)
		{
			Harmony harmony = new(id: id);

			List<PatchTemplate> patches =
			[
				// Utility
				new(
					type: HarmonyPatchType.Prefix,
					original: AccessTools.Method(typeof(StardewValley.Utility), nameof(StardewValley.Utility.isThereAnObjectHereWhichAcceptsThisItem)),
					patch: nameof(HarmonyPatches.Utility_IsThereAnObjectHereWhichAcceptsThisItem_Prefix)),

				// Object
				new(
					type: HarmonyPatchType.Prefix,
					original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.ApplySprinkler)),
					patch: nameof(HarmonyPatches.Object_ApplySprinkler_Prefix)),
				
				// GameLocation
				new(
					type: HarmonyPatchType.Postfix,
					original: AccessTools.Method(typeof(StardewValley.GameLocation), nameof(StardewValley.GameLocation.IsTileOccupiedBy)),
					patch: nameof(HarmonyPatches.GameLocation_IsTileOccupiedForPlacement_Postfix)),
			];

			foreach (PatchTemplate patch in patches)
			{
				harmony.Patch(
					original: patch.original,
					prefix: patch.type == HarmonyPatchType.Prefix ? patch.method : null,
					postfix: patch.type == HarmonyPatchType.Postfix ? patch.method : null,
					transpiler: patch.type == HarmonyPatchType.Transpiler ? patch.method : null,
					finalizer: patch.type == HarmonyPatchType.Finalizer ? patch.method : null);
			}
		}

		private static void ErrorHandler(Exception e)
		{
			Log.E($"{ModEntry.Instance.ModManifest.UniqueID} failed in harmony patch method.{Environment.NewLine}{e}");
		}

		/// <summary>
		/// Replace logic determining item drop-in actions on garden bed objects.
		/// </summary>
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
				if (location.Objects.TryGetValue(tileLocation, out StardewValley.Object o) && o is OutdoorPot op)
				{
					if (!OutdoorPot.CanAcceptItemOrSeed(item: item) && OutdoorPot.CanAcceptAnything(op: op))
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

		/// <summary>
		/// Replace logic for garden bed objects being watered by sprinklers.
		/// </summary>
		public static bool Object_ApplySprinkler_Prefix(
			StardewValley.Object __instance)
		{
			try
			{
				if (ModEntry.Config.SprinklersEnabled
					&& __instance is OutdoorPot op)
				{
					if (OutdoorPot.CanAcceptAnything(op: op, ignoreCrops: true))
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
			Vector2 tile)
		{
			if (__instance.Objects.TryGetValue(tile, out StardewValley.Object o) && o is OutdoorPot op)
			{
				Item item = Game1.player.ActiveItem;
				bool isPlantable = OutdoorPot.CanAcceptItemOrSeed(item)
					&& op.hoeDirt.Value.canPlantThisSeedHere(itemId: item.ItemId, isFertilizer: item.Category == StardewValley.Object.fertilizerCategory);
				if (OutdoorPot.CanAcceptAnything(op: op) && isPlantable)
				{
					__result = false;
				}
			}
		}
	}
}
