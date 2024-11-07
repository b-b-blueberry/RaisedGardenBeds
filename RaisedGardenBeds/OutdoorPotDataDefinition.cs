using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace RaisedGardenBeds
{
	public class OutdoorPotDataDefinition : BaseItemDataDefinition
	{
		public static string TypeDefinitionId => "(BB_RGB)";

		public override string Identifier => OutdoorPotDataDefinition.TypeDefinitionId;

		public override Item CreateItem(ParsedItemData data)
		{
			return new OutdoorPot(itemId: data.ItemId, tile: Vector2.Zero);
		}

		public override bool Exists(string itemId)
		{
			return ModEntry.ItemDefinitions.Values.Any(entry => entry.ItemName == itemId);
		}

		public override IEnumerable<string> GetAllIds()
		{
			return ModEntry.ItemDefinitions.Values.Select(entry => entry.ItemName);
		}

		public override ParsedItemData GetData(string itemId)
		{
			ItemDefinition entry = ModEntry.ItemDefinitions.Values.FirstOrDefault(entry => entry.ItemName == itemId);
			ParsedItemData data = new ParsedItemData(
				itemType: this,
				itemId: itemId,
				spriteIndex: entry.SpriteIndex,
				textureName: ItemDefinition.GetTextureName(packKey: OutdoorPot.GetItemDefinitionFromItemName(itemId).ContentPackName),
				internalName: entry.ItemName,
				displayName: OutdoorPot.GetDisplayNameFromVariantKey(variantKey: entry.VariantName),
				description: OutdoorPot.GetRawDescription(),
				category: OutdoorPot.CraftingCategory,
				objectType: "Crafting",
				rawData: entry,
				isErrorItem: false,
				excludeFromRandomSale: true);
			return data;
		}

		public override Rectangle GetSourceRect(ParsedItemData data, Texture2D texture, int spriteIndex)
		{
			return OutdoorPot.GetSpriteSourceRectangle(spriteIndex: spriteIndex);
		}
	}
}
