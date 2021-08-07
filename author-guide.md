← [README](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/README.md)

![](https://i.imgur.com/vic1xOi.png)

This is a guide for mod authors making their own Raised Garden Beds content packs, or writing translations for Content Patcher.  
For info about the mod itself, see the main [README](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/README.md).

All of this mod's content is added using its own framework: you can use the [[RGB]](https://github.com/b-b-blueberry/RaisedGardenBeds/tree/master/%5BRGB%5D%20RaisedGardenBeds) and [[CP]](https://github.com/b-b-blueberry/RaisedGardenBeds/tree/master/%5BCP%5D%20RaisedGardenBeds%20-%20English) folders in this repo as a guide.  
Wherever a `.csproj` file is found, it should be ignored. These aren't used in user content packs.

## Contents
* [Intro](#intro)
  * [What does RGB do?](#what-does-rgb-do)
  * [Requirements](#requirements)
  * [Notes](#notes)
* [Adding new object variants](#adding-new-object-variants)
  * [Required files](#required-files)
  * [Entries](#entries)
  * [Fields](#fields)
  * [Sprites](#sprites)
* [Adding translations](#adding-translations)
  * [Languages](#languages)
  * [Common translations](#common-translations)
  * [Item translations](#item-translations)
* [Release a content pack](#release-a-content-pack)
* [Compatibility](#compatibility)
* [Acknowledgements](#acknowledgements)

## Intro
### What does RGB do?
Raised Garden Beds (RGB) is primarily a mod to add a new type of craftable object to Stardew Valley: a placeable outdoors garden bed that allows players to create custom garden beds on parts of the farm that are normally unusable. With content packs, you can add variants of your own, with different designs, styles, rules, and sprites.

### Requirements
This mod requires other mods in order to work correctly:  
* [Pathoschild's Content Patcher](https://github.com/Pathoschild/StardewMods) is required in order to add all language translations, including the default English translation.  
* [spacechase0's SpaceCore](https://github.com/spacechase0/StardewValleyMods) is also required for custom object serialisation (magical code wonders).

### Notes
Unlike [Digus' Producer Framework Mod](https://github.com/Digus/StardewValleyMods), this mod does not require content pack authors to create any new objects using [spacechase0's Json Assets](https://github.com/spacechase0/StardewValleyMods). The mod handles adding all new variants using its own content packs, and all object variants are treated equally.

## Adding new object variants
### Required files
Your mod folder should contain 3 files:
<table>
<tr>
<th>name</th>
<th>purpose</th>
</tr>
<tr>
<td>

`manifest.json`

</td>
<td>

Common file required by all SMAPI mods.  
See [Modding: Manifest](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest) on the official Stardew Valley wiki for info on manifest files.

</td>
</tr>
<tr>
<td>

`content.json`

</td>
<td>

Content file containing all object variant definitions for your mod. Object definitions should include all of the non-optional fields in [Fields](#fields), as well as any relevant optional fields.

</td>
</tr>
<tr>
<td>

`sprites.png`

</td>
<td>

Image asset file containing all object variant sprites matching the definitions in your content file.

</td>
</tr>
</table>

Example layout: [[RGB] RaisedGardenBeds](https://github.com/b-b-blueberry/RaisedGardenBeds/tree/master/%5BRGB%5D%20RaisedGardenBeds)

### Entries
Each object variant defined in your content file should have a unique name within the file; that is, you can't have two defined variants with the same name, but you don't need to worry about naming conflicts with other mods.  
You can define as many object variants as you like in a single content file, with each `{}` object entry separated by `,` commas. For example:
```js
{
  "Diamond": {
    "SoilHeightAboveGround": 12,
    "RecipeIngredients": [
      {
        "Object": "Wood",
        "Count": 10
      },
      {
        "Object": 72, // Diamond
        "Count": 1
      }
    ],
    "RecipeCraftedCount": 1,
    "RecipeIsDefault": false,
    "RecipeConditions": null
    // The omitted DaysToBreak field will use its default value.
  },
  "Dirt": {
    "SoilHeightAboveGround": 8,
    "RecipeIngredients": [
      {
        "Object": "Dirt Wad",
        "Count" 5
      }
    ],
    "DaysToBreak": 32
    // All other omitted fields will use default values.
  },
  // more object variant definitions . . .
}
```

Example content file: [[RGB] RaisedGardenBeds/content.json](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/%5BRGB%5D%20RaisedGardenBeds/content.json)

### Fields
Each entry in the content file can contain up to 6 fields, of which 4 are optional and will default to some basic value if not included:
<table>
<tr>
<th>field</th>
<th>purpose</th>
</tr>
<tr>
<td>

`SoilHeightAboveGround`

</td>
<td>

Vertical offset of the hoe dirt sprites in the object sprite. Taller or larger variants will have higher values. Adjust to suit your sprite.

Valid range: `6-16`

</td>
</tr>
<tr>
<td>

`RecipeIngredients`

</td>
<td>

A list of json objects containing `Object` & `Count` fields that define the items required in this object's crafting recipe.

</td>
</tr>
<tr>
<td>

`Object` & `Count`

</td>
<td>

Fields that are part of `RecipeIngredients`.

You can add up to 5 different ingredients to a recipe. You can use either the item ID or the name of the object. Object fields that contain a negative value are the generic ID. Example: Rather than using a specific flower, -70 allows for any flower to be used. You cannot use context tags for this field.

</td>
</tr>
<tr>
<td>

`DaysToBreak`

_(Optional)_

</td>
<td>

Minimum number of days before this object is considered broken. Objects are checked for breakage at the end of each season. A value of `0` will render the object unbreakable.

Valid range: `0-999`  
Default: `0`

</td>
</tr>
<tr>
<td>

`RecipeCraftedCount`

_(Optional)_

</td>
<td>

Stack count created each time this object's crafting recipe is used.

Valid range: `0-999`  
Default: `1`

</td>
</tr>
<tr>
<td>

`RecipeIsDefault`

_(Optional)_

</td>
<td>

Determines whether this object's crafting recipe should be given to the player immediately, rather than waiting for the player to view the initial event. If `true`, the `RecipeConditions` field will be ignored if not `null`.

Values: `true`, `false`  
Default: `false`

</td>
</tr>
<tr>
<td>

`RecipeConditions`

_(Optional)_

</td>
<td>

Stack count created each time this object's crafting recipe is used. Multiple preconditions may be used, each separated by `/`. If not `null`, players will first need to view the initial event and then meet the event preconditions before they receive this object's crafting recipe. If `null`, the recipe will be given after viewing the initial event.

Format should match Stardew Valley's event preconditions shown on the official wiki's [Modding: Event Data](https://stardewvalleywiki.com/Modding:Event_data#Event_preconditions) page.

Default: `null`

</td>
</tr>
</table>

### Sprites
Spritesheets for content packs that add more than one object variant must add subsequent sprites vertically, continuing downwards. Sprites are expected to have the same index in their spritesheet as their matching definition's index in the content file.

Your spritesheet should be 160 pixels wide, and a multiple of 32 pixels tall.

The spritesheet should contain as many sprites as you have defined variants. If you have fewer entries in your content file than you have sprites, those sprites will be unused. If you have fewer sprites than you have entries in your content file, those object variants will be unused.

An example spritesheet using the reference template, below, with multiple variants:  
![](https://i.imgur.com/MK56mL8.png)

![](https://i.imgur.com/2YAwqGo.png)

The spritesheet is split into 20 tiles of 16x16 pixels each; 10 columns and 2 rows. As the object sprite is rendered in the world in parts, each tile is used for a different part. Some parts are left blank.  
* AB: The object sprite as it appears when placed single, also used for outside corners.
* C: Blank.
* D: Vertical edges for tall arrangements.
* EF: Horizontal edges for wide arrangements.
* G: Upward inside edges.
* H: Blank.
* I: Hoe dirt sprite, unwatered.
* J: Hoe dirt sprite, watered.
* K: Downward inside edges.
* L: Blank.
* M: Blank.
* N: Right side corner for wide arrangements.
* OP: Blank.
* QR: Broken object sprite.
* ST: Reserved; leave blank.

Creating a spritesheet that aligns and tesselates correctly can take some trial-and-error, so it might take a few tries to get your sprites perfect.  
Blank sprites are still rendered, which you can use to your advantage by playing around with unique details to appear in certain parts of your object sprite.

Example sprites: [[RGB] RaisedGardenBeds/sprites.png](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/%5BRGB%5D%20RaisedGardenBeds/sprites.png)

## Adding translations
Unlike [spacechase0's Json Assets](https://github.com/spacechase0/StardewValleyMods), content packs for this mod don't include the translations for the object names. Instead, a separate [Content Patcher](https://github.com/Pathoschild/StardewMods) content pack is used to add translations. To create a translation pack you'll need to be comfortable with making content packs for Content Patcher in particular, info and documentation is available on the official Stardew Valley wiki's [Modding: Content Patcher](https://stardewvalleywiki.com/Modding:Content_Patcher) page.

A single translation pack can include the translations for this mod's common strings, as well as the content for all content packs the translation author knows of. Content pack authors can also create translation packs of their own for a content pack they release if they so choose. If no translation for an object variant name is found, the object will use the name given to it within the content file.

### Languages
Translation packs need to specify the language they're translating to by providing a _language code_ as a field in the translation pack content file. You can choose from any one of the official language codes below.

If you're writing a translation pack for an unofficial language; such as Polish, Thai, or Vietnamese; you should target the language that any other unofficial translation mod uses. For example, if an unofficial Vietnamese translation replaces the official English/`en` translation, you should use the field `en` for your Vietnamese translation pack.

<table>
<tr>
<th>

Language

</th>
<th>

Code

</th>
</tr>
<tr>
<td>
Chinese
</td>
<td>
zh
</td>
</tr>
<tr>
<td>
English
</td>
<td>
en
</td>
</tr>
<tr>
<td>
French
<td>
fr
</td>
</tr>
<tr>
<td>
German
</td>
<td>
de
</td>
</tr>
<tr>
<td>
Hungarian
</td>
<td>
hu
</td>
</tr>
<tr>
<td>
Italian
</td>
<td>
it
</td>
</tr>
<tr>
<td>
Japanese
</td>
<td>
ja
</td>
</tr>
<tr>
<td>
Korean
</td>
<td>
ko
</td>
</tr>
<tr>
<td>
Portuguese
</td>
<td>
pt
</td>
</tr>
<tr>
<td>
Russian
</td>
<td>
ru
</td>
</tr>
<tr>
<td>
Spanish
</td>
<td>
es
</td>
</tr>
<tr>
<td>
Turkish
</td>
<td>
tr
</td>
</tr>
</table>

Example translation pack: [[CP] RaisedGardenBeds - English](https://github.com/b-b-blueberry/RaisedGardenBeds/tree/master/%5BCP%5D%20RaisedGardenBeds%20-%20English)

### Common translations
All of this mod's text strings, event dialogue, HUD messages, and config options are translatable by targeting the `CommonTranslations` asset. 

To translate common text strings throughout the mod, you need to provide your language code as a field, and then match the keys used in [[CP] RaisedGardenBeds - English/content.json](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/%5BCP%5D%20RaisedGardenBeds%20-%20English/content.json). For example:
```js
"Entries":
{
 "ja":
 {
  "item.name": "花畑",
  "item.name.variant": "{0} 花畑",
  // . . .
 }
}
```

Example common translations: [[CP] RaisedGardenBeds - English/content.json](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/%5BCP%5D%20RaisedGardenBeds%20-%20English/content.json)

### Item translations
All objects from all RGB content packs can be translated in a single translation pack by targeting the `ItemTranslations` asset. Only object names have translations: all objects share the same generic description.

To translate items from an RGB content pack, you need to use its `manifest.json` file's `UniqueID` field as a key within your language entry. For example, using [[RGB] RaisedGardenBeds/manifest.json](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/%5BRGB%5D%20RaisedGardenBeds/manifest.json):
```js
"Entries":
{
 "ja":
 {
  "blueberry.RaisedGardenBeds.RGB":
  {
   "Wood": "木の",
   "Stone": "石の",
   "Old": "古い",
   "Gold": "金の",
   "Candy": "菓子な",
   "Void": "ダーク",
   // more object variant names . . .
  },
  // more content packs . . .
 }
}
```

Example item translations: [[CP] RaisedGardenBeds - English/content.json](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/%5BCP%5D%20RaisedGardenBeds%20-%20English/content.json)

## Release a content pack
See [Modding: Content Packs](https://stardewvalleywiki.com/Modding:Content_packs) on the official Stardew Valley wiki for info on creating and publishing content packs.

To create an [RGB] content pack, you can provide the `"ContentPackFor"` field with the UniqueID of this mod, as used in [RaisedGardenBeds/manifest.json](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/%5BRGB%5D%20RaisedGardenBeds/manifest.json).

## Compatibility
All content packs for this mod are compatible with one-another, regardless of the name each object variant uses: all objects are referenced internally with the content pack's `manifest.json` `UniqueID` field.

Content packs may be added and removed without worrying about Json Assets IDs being shuffled: this mod doesn't use JA to add custom objects.
Removing or uninstalling any of this mod's requirements without removing or trashing all instances of Raised Garden Bed objects from your save file will render the save file non-loadable until they're reinstalled.

Translation packs are compatible with one-another, and incomplete translations can be filled in by other translation packs using the same language code.

## Acknowledgements
Some parts of this guide, including markdown formatting and text, are based on or verbatim lifted from the author-guide pages of [Pathoschild's Content Patcher](https://github.com/Pathoschild/StardewMods) and [spacechase0's Json Assets](https://github.com/spacechase0/StardewValleyMods).  
Raised Garden Beds uses SMAPI's content frameworks to manage its content packs.
