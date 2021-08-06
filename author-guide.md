← [README](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/README.md)

![](https://i.imgur.com/vic1xOi.png)

This is a guide for mod authors making their own Raised Garden Beds content packs, or writing translations for Content Patcher.  
For info about the mod itself, see the main [README](https://github.com/b-b-blueberry/RaisedGardenBeds/blob/master/README.md).

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
  * [todo](#todo-1)
* [Release a content pack](#release-a-content-pack)
  * [todo](#todo-2)
* [Compatibility](#compatibility)
  * [todo](#todo-3)
* [Acknowledgements](#acknowledgements)

## Intro
### What does RGB do?
Raised Garden Beds (RGB) is primarily a mod to add a new type of craftable object to Stardew Valley: a placeable outdoors garden bed that allows players to create wide garden beds on normally unusable parts of the farm. With content packs, you can add variants of your own, with different designs, styles, rules, and sprites.

### Requirements
This mod requires other mods in order to work correctly:  
[Pathoschild's Content Patcher](https://github.com/Pathoschild/StardewMods) is required in order to add all language translations, including the default English translation.  
[Routine's PyTK](https://github.com/Platonymous/Stardew-Valley-Mods) is also required for custom object serialisation (magical code wonders).

### Notes
Unlike [Digus' Producer Framework Mod](https://github.com/Digus/StardewValleyMods), this mod does not require content pack authors to create any new objects using [spacechase0's Json Assets](https://github.com/spacechase0). The mod handles adding all new variants using its own content packs, and all object variants are treated equally.

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
      manifest.json
    </td>
    <td>
      Common file required by all SMAPI mods. Read more: https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest
    </td>
  </tr>
  <tr>
    <td>
      content.json
    </td>
    <td>
      Content file containing all object variant definitions for your mod. Object definitions should include all of the non-optional fields in [Fields](#fields), as well as any relevant optional fields.
    </td>
  </tr>
  <tr>
    <td>
      sprites.png
    </td>
    <td>
      Image asset file containing all object variant sprites matching the definitions in your content file.
    </td>
  </tr>
</table>

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
  // ...
}
```

### Fields
Each entry in the content file can contain up to 6 fields, of which 4 are optional and will default to some basic value if not included:
<table>
  <tr>
    <th>field</th>
    <th>purpose</th>
  </tr>
  <tr>
    <td>
      SoilHeightAboveGround
    </td>
    <td>
      Vertical offset of the hoe dirt sprites in the object sprite. Taller or larger variants will have higher values. Adjust to suit your sprite. Valid range: `6-16`.
    </td>
  </tr>
  <tr>
    <td>
      RecipeIngredients
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
      Fields that are part of `RecipeIngredients`. You can add up to 5 different ingredients to a recipe. You can use either the item ID or the name of the object. Object fields that contain a negative value are the generic ID. Example: Rather than using a specific flower, -70 allows for any flower to be used. You cannot use context tags for this field.
    </td>
  </tr>
  <tr>
    <td>
      DaysToBreak
    </td>
    <td>
      _(Optional)_ — Minimum number of days before this object is considered broken. Objects are checked for breakage at the end of each season. A value of `0` will render the object unbreakable. Valid range: `0-999`. Default: `0`.
    </td>
  </tr>
  <tr>
    <td>
      RecipeCraftedCount
    </td>
    <td>
      _(Optional)_ — Stack count created each time this object's crafting recipe is used. Valid range: `0-999`. Default: `1`.
    </td>
  </tr>
  <tr>
    <td>
      RecipeIsDefault
    </td>
    <td>
      _(Optional)_ — Determines whether this object's crafting recipe should be given to the player immediately, rather than waiting for the player to view the initial event. If `true`, the `RecipeConditions` field will be ignored if not `null`. Values: `true`, `false`. Default: `false`.
    </td>
  </tr>
  <tr>
    <td>
      RecipeConditions
    </td>
    <td>
      _(Optional)_ — Stack count created each time this object's crafting recipe is used. Format should match ![Stardew Valley's event preconditions](https://stardewvalleywiki.com/Modding:Event_data#Event_preconditions). Multiple preconditions may be used, each separated by `/`. If not `null`, players will first need to view the initial event and then meet the event preconditions before they receive this object's crafting recipe. If `null`, the recipe will be given after viewing the initial event. Default: `null`.
    </td>
  </tr>
</table>

### Sprites
Spritesheets for content packs that add more than one object variant must add subsequent sprites vertically, continuing downwards. Sprites are expected to have the same index in their spritesheet as their matching definition's index in the content file.

An example spritesheet using the reference template, below, with multiple variants:  
![](https://i.imgur.com/MK56mL8.png)

![](https://i.imgur.com/2YAwqGo.png)

The spritesheet is split into 20 tiles of 16x16 pixels each. As the object sprite is rendered in the world in parts, each tile is used for a different part. Some parts are left blank.  
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

## Adding translations
TODO: Adding translations

## Release a content pack
TODO: Release a content pack

## Compatibility
TODO: Compatibility

## Acknowledgements
Some parts of this guide, including markdown formatting and text, are based on or verbatim lifted from the author-guide pages of [Pathoschild's Content Patcher](https://github.com/Pathoschild/StardewMods) and [spacechase0's Json Assets](https://github.com/spacechase0).  
Raised Garden Beds uses SMAPI's frameworks to manage its content packs.
