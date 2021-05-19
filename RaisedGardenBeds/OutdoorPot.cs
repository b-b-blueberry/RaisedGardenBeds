using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Locations;
using StardewModdingAPI;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using StardewValley.TerrainFeatures;
using System.Xml.Serialization;

namespace RaisedGardenBeds
{
	[XmlType("Mods_Blueberry_RaisedGardenBeds_OutdoorPot")]	// SpaceCore serialisation signature
	public class OutdoorPot : StardewValley.Objects.IndoorPot
	{
		/// <summary>
		/// Temporary one-tick variable used in <see cref="OutdoorPot.AdjustAllOnNextTick(GameLocation)"/> 
		/// in order to indirectly provide the locations to check to the <see cref="AdjustAll(GameLocation)"/> method.
		/// </summary>
		[XmlIgnore]
		private static GameLocation LocationToIdentifyOutdoorPots;
		/// <summary>
		/// Shared spritesheet containing object icon, component, breakage, and soil sprites.
		/// </summary>
		[XmlIgnore]
		public static Texture2D Sprites;
		/// <summary>
		/// Array of axes that contain a neighbouring OutdoorPot object, projecting outwards from each corner of the object tile.
		/// </summary>
		[XmlIgnore]
		public readonly Axis[] Neighbours = new Axis[4];

		[Flags]
		public enum Axis
		{
			None = 0,
			Vertical = 1,
			Horizontal = 2,
			Diagonal = 4
		}

		internal const int EndpieceIndexInSheet = 6;
		/// <summary>
		/// Horizontal index of soil sprites, arranged vertically, in the shared <see cref="OutdoorPot.Sprites"/> spritesheet.
		/// </summary>
		internal const int SoilIndexInSheet = 4;
		/// <summary>
		/// Visual Y-offset of soil sprites from object tile Y-position.
		/// </summary>
		internal const int SoilHeightFromGround = 8;
		/// <summary>
		/// Default number of days before the object can be broken at the end of the season.
		/// </summary>
		internal const int BreakageDefault = 28;
		/// <summary>
		/// If <see cref="StardewValley.Object.MinutesUntilReady"/> is above this value, the object will not be broken at the end of the season.
		/// </summary>
		internal const int BreakageGracePeriod = 6;
		/// <summary>
		/// When broken, <see cref="StardewValley.Object.MinutesUntilReady"/> is set to this value.
		/// </summary>
		internal const int BreakageDefinite = -300;
		/// <summary>
		/// Whether the object is marked as broken, preventing it from planting or growing seeds and crops.
		/// </summary>
		public bool IsBroken => this.MinutesUntilReady <= OutdoorPot.BreakageDefinite;


		public OutdoorPot() : this(tileLocation: Vector2.Zero) {}

		public OutdoorPot(Vector2 tileLocation)
		{
			// Object ()
			this.initNetFields();

			// Object (Vector2, int, bool) : Object ()
			this.ParentSheetIndex = ModEntry.JsonAssets.GetBigCraftableId(ModEntry.ItemName);
			this.TileLocation = tileLocation;
			this.CanBeSetDown = true;
			this.bigCraftable.Value = true;
			Game1.bigCraftablesInformation.TryGetValue(this.ParentSheetIndex, out string objectInformation);
			if (objectInformation != null)
			{
				string[] objectInfoArray = objectInformation.Split('/');
				this.Name = objectInfoArray[0];
				this.Price = int.Parse(objectInfoArray[1]);
				this.Edibility = int.Parse(objectInfoArray[2]);
				string[] typeAndCategory = objectInfoArray[3].Split(' ');
				this.Type = typeAndCategory[0];
				if (typeAndCategory.Length > 1)
				{
					this.Category = Convert.ToInt32(typeAndCategory[1]);
				}
				this.setOutdoors.Value = bool.Parse(objectInfoArray[5]);
				this.setIndoors.Value = bool.Parse(objectInfoArray[6]);
				this.Fragility = int.Parse(objectInfoArray[7]);
				this.isLamp.Value = false;
				this.IsRecipe = false;
			}
			this.boundingBox.Value = new Rectangle((int)tileLocation.X * Game1.tileSize, (int)tileLocation.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);

			// IndoorPot (Vector2) : Object (Vector2, int, bool)
			this.hoeDirt.Value = new HoeDirt();
			if (Game1.isRaining && Game1.currentLocation.IsOutdoors)
			{
				this.hoeDirt.Value.state.Value = 1;
			}
			this.showNextIndex.Value = this.hoeDirt.Value.state.Value == 1;

			// OutdoorPot (Vector2)
			this.MinutesUntilReady = OutdoorPot.BreakageDefault;
		}

		public override bool placementAction(GameLocation location, int x, int y, Farmer who = null)
		{
			Vector2 tileLocation = new Vector2(x, y) / Game1.tileSize;
			location.Objects[tileLocation] = new OutdoorPot(tileLocation: tileLocation);
			OutdoorPot.AdjustAll(specificLocation: location);
			return false;
		}

		public override void performRemoveAction(Vector2 tileLocation, GameLocation environment)
		{
			base.performRemoveAction(tileLocation, environment);
			OutdoorPot.AdjustAllOnNextTick(specificLocation: environment);
		}

		public override bool performToolAction(Tool t, GameLocation location)
		{
			bool whacked = base.performToolAction(t, location);
			if (whacked)
			{
				OutdoorPot.AdjustAllOnNextTick(specificLocation: location);
			}
			return whacked;
		}

		public override bool performObjectDropInAction(Item dropInItem, bool probe, Farmer who)
		{
			// Block seed, crop, and fertiliser actions when breakage is enabled and object is broken
			if (ModEntry.Config.RaisedBedsBreakWithAge && this.IsBroken && dropInItem is OutdoorPot)
			{
				if (!probe)
				{
					this.MinutesUntilReady = OutdoorPot.BreakageDefault;
					who.currentLocation.playSound("Ship");
					OutdoorPot.AdjustAll(specificLocation: who.currentLocation);
				}
				return true;
			}
			return base.performObjectDropInAction(dropInItem, probe, who);
		}

		public override bool canBePlacedHere(GameLocation l, Vector2 tile)
		{
			bool clearTiles = l.isTileLocationTotallyClearAndPlaceableIgnoreFloors(tile);
			bool clearObjects = !l.Objects.ContainsKey(tile);
			return l.IsFarm && l.IsOutdoors && clearTiles && clearObjects;
		}

		public override void DayUpdate(GameLocation location)
		{
			base.DayUpdate(location);
			if (ModEntry.Config.RaisedBedsBreakWithAge)
			{
				if (this.MinutesUntilReady == 0)
				{
					// Reset breakage timer if enabled in config after objects were forced to the neutral 0 days value
					this.MinutesUntilReady = OutdoorPot.BreakageDefault;
				}
				else if (this.IsBroken)
				{
					// If this object is broken, its crop shouldn't be allowed to grow
					this.hoeDirt.Value.crop?.ResetPhaseDays();
				}
				--this.MinutesUntilReady;
			}
			else
			{
				// Ignore breakage timer when disabled
				this.MinutesUntilReady = 0;
			}
		}

		public override void draw(SpriteBatch spriteBatch, int x, int y, float alpha = 1)
		{
			Vector2 position = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * Game1.tileSize, (y * Game1.tileSize) - Game1.tileSize));
			Rectangle destination = new Rectangle(
				(int)position.X + ((this.shakeTimer > 0) ? Game1.random.Next(-1, 2) : 0),
				(int)position.Y + ((this.shakeTimer > 0) ? Game1.random.Next(-1, 2) : 0),
				Game1.tileSize,
				Game1.tileSize * 2);

			Rectangle[] source = new Rectangle[4];
			const int w = Game1.smallestTileSize / 2;
			const int h = Game1.smallestTileSize;

			// Layer depth used in base game calculations for illusion of depth when rendering world objects
			float layerDepth = Math.Max(0f, (((y + 1f) * Game1.tileSize) - (OutdoorPot.SoilHeightFromGround * Game1.pixelZoom)) / 10000f) + (1 / 10000f);
			float layerDepth2 = Math.Max(0f, ((y * Game1.tileSize) - (OutdoorPot.SoilHeightFromGround * Game1.pixelZoom)) / 10000f) + (1 / 10000f);

			// Broken OutdoorPot
			if (this.IsBroken)
			{
				spriteBatch.Draw(
					texture: OutdoorPot.Sprites,
					destinationRectangle: destination,
					sourceRectangle: new Rectangle(Game1.smallestTileSize * 8, 0, Game1.smallestTileSize, Game1.smallestTileSize * 2),
					color: Color.White * alpha,
					rotation: 0f,
					origin: Vector2.Zero,
					effects: SpriteEffects.None,
					layerDepth: layerDepth);
				return;
			}

			// Soil
			spriteBatch.Draw(
				texture: OutdoorPot.Sprites,
				destinationRectangle: new Rectangle(destination.X, destination.Y + ((Game1.smallestTileSize - OutdoorPot.SoilHeightFromGround) * Game1.pixelZoom), Game1.tileSize, Game1.tileSize),
				sourceRectangle: new Rectangle(Game1.smallestTileSize * OutdoorPot.SoilIndexInSheet, this.showNextIndex.Value ? Game1.smallestTileSize : 0, Game1.smallestTileSize, Game1.smallestTileSize),
				color: Color.White,
				rotation: 0f,
				origin: Vector2.Zero,
				effects: SpriteEffects.None,
				layerDepth: ((this.Neighbours[0].HasFlag(Axis.Vertical)) ? layerDepth2 : layerDepth) - (1 / 10000f));

			// Neutral OutdoorPot
			if (this.TileLocation == Vector2.Zero)
			{
				spriteBatch.Draw(
					texture: OutdoorPot.Sprites,
					destinationRectangle: destination,
					sourceRectangle: new Rectangle(0, 0, Game1.smallestTileSize, Game1.smallestTileSize * 2),
					color: Color.White * alpha,
					rotation: 0f,
					origin: Vector2.Zero,
					effects: SpriteEffects.None,
					layerDepth: layerDepth);
				return;
			}

			// Source rectangles for corners are based on their neighbouring OutdoorPots
			source[0] = new Rectangle(((int)this.Neighbours[0] * w * 2), 0, w, h);
			source[1] = new Rectangle(w + ((int)this.Neighbours[1] * w * 2), 0, w, h);
			source[2] = new Rectangle(((int)this.Neighbours[2] * w * 2), h, w, h);
			source[3] = new Rectangle(w + ((this.Neighbours[2] == Axis.Horizontal && this.Neighbours[3] == Axis.None ? OutdoorPot.EndpieceIndexInSheet : (int)this.Neighbours[3]) * w * 2), h, w, h);

			// Corners are drawn individually to allow for all placement permutations
			for (int i = 0; i < 4; ++i)
			{
				Rectangle cornerDestination = new Rectangle(
						destination.X + (i % 2 == 1 ? destination.Width / 2 : 0),
						destination.Y + (i > 1 ? destination.Height / 2 : 0),
						destination.Width / 2,
						destination.Height / 2);

				spriteBatch.Draw(
					texture: OutdoorPot.Sprites,
					destinationRectangle: cornerDestination,
					sourceRectangle: source[i],
					color: Color.White * alpha,
					rotation: 0f,
					origin: Vector2.Zero,
					effects: SpriteEffects.None,
					layerDepth: layerDepth + ((i + 1) / 10000f));
			}

			// Fertiliser
			if (this.hoeDirt.Value.fertilizer.Value != 0)
			{
				Rectangle fertilizer_rect = this.hoeDirt.Value.GetFertilizerSourceRect(this.hoeDirt.Value.fertilizer.Value);
				fertilizer_rect.Width = 13;
				fertilizer_rect.Height = 13;

				spriteBatch.Draw(
					texture: Game1.mouseCursors,
					position: Game1.GlobalToLocal(Game1.viewport, new Vector2((this.TileLocation.X * Game1.tileSize) + (1 * Game1.pixelZoom), (this.TileLocation.Y * Game1.tileSize) - (OutdoorPot.SoilHeightFromGround * 2) - (2 * Game1.pixelZoom))),
					sourceRectangle: fertilizer_rect,
					color: Color.White,
					rotation: 0f,
					origin: Vector2.Zero,
					scale: Game1.pixelZoom,
					effects: SpriteEffects.None,
					layerDepth: ((this.TileLocation.Y + 0.65f) * Game1.tileSize / 10000f) + (1 / 10000f));
			}

			// Seeds and crops
			if (this.hoeDirt.Value.crop != null)
			{
				this.hoeDirt.Value.crop.drawWithOffset(
					spriteBatch,
					tileLocation: this.TileLocation,
					toTint: (this.hoeDirt.Value.state.Value == 1 && this.hoeDirt.Value.crop.currentPhase.Value == 0 && !this.hoeDirt.Value.crop.raisedSeeds.Value)
						? (new Color(180, 100, 200) * 1f)
						: Color.White,
					rotation: this.hoeDirt.Value.getShakeRotation(),
					offset: new Vector2(Game1.tileSize / 2, 0f));
			}

			// ???
			if (this.heldObject.Value != null)
			{
				this.heldObject.Value.draw(
					spriteBatch,
					xNonTile: x * Game1.tileSize,
					yNonTile: (y * Game1.tileSize) - (OutdoorPot.SoilHeightFromGround * Game1.pixelZoom),
					layerDepth: ((this.TileLocation.Y + 0.66f) * Game1.tileSize / 10000f) + (1 / 10000f),
					alpha: 1f);
			}

			// Plantable bushes (eg. Tea)
			if (this.bush.Value != null)
			{
				this.bush.Value.draw(
					spriteBatch,
					tileLocation: new Vector2(x, y),
					yDrawOffset: -(OutdoorPot.SoilHeightFromGround * Game1.pixelZoom));
			}
		}

		public override Item getOne()
		{
			return new OutdoorPot();
		}

		/// <summary>
		/// Check all objects to qualify for breakage, and mark for breakage if qualified.
		/// </summary>
		public static void BreakAll(GameLocation specificLocation = null)
		{
			void breakAllInLocation(GameLocation location)
			{
				List<OutdoorPot> pots = location.Objects.Values.Where(
						o => o is OutdoorPot p
							&& p.MinutesUntilReady < OutdoorPot.BreakageGracePeriod)
					.Cast<OutdoorPot>().ToList();
				foreach (OutdoorPot pot in pots)
				{
					pot.Break(location: location, adjust: false);
				}
				OutdoorPot.AdjustAll(specificLocation: location);
			}

			if (specificLocation != null)
				breakAllInLocation(specificLocation);
			else
				foreach (GameLocation location in Game1.locations)
					breakAllInLocation(location);
		}

		/// <summary>
		/// Set values to mark the object as broken, leaving it unable to continue to grow crops.
		/// </summary>
		/// <param name="adjust">Whether to call <see cref="OutdoorPot.AdjustAll(GameLocation)"/> after breaking.</param>
		public void Break(GameLocation location, bool adjust)
		{
			MinutesUntilReady = OutdoorPot.BreakageDefinite;
			if (adjust)
			{
				OutdoorPot.AdjustAll(specificLocation: location);
			}
		}

		/// <summary>
		/// Calls <see cref="OutdoorPot.AdjustAll(GameLocation)"/> on the next tick.
		/// Useful when adjusting sprites after some base game checks, or when removing multiple objects simultaneously.
		/// </summary>
		public static void AdjustAllOnNextTick(GameLocation specificLocation = null)
		{
			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked += OutdoorPot.Event_AdjustAllOnNextTick;
			OutdoorPot.LocationToIdentifyOutdoorPots = specificLocation;
		}

		private static void Event_AdjustAllOnNextTick(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
		{
			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked -= OutdoorPot.Event_AdjustAllOnNextTick;
			OutdoorPot.AdjustAll(specificLocation: OutdoorPot.LocationToIdentifyOutdoorPots);
			OutdoorPot.LocationToIdentifyOutdoorPots = null;
		}

		/// <summary>
		/// Adjusts sprites of all objects by reconfirming the positions of other nearby objects.
		/// Required to form complete shapes using the four-corners method of building raised bed areas from objects.
		/// </summary>
		public static void AdjustAll(GameLocation specificLocation = null)
		{
			void adjustAllInLocation(GameLocation location)
			{
				List<Vector2> tiles = location.Objects.Keys.Where(t => location.Objects[t] is OutdoorPot).Reverse().ToList();
				foreach (Vector2 tile in tiles)
				{
					for (int i = 0; i < 4; ++i)
					{
						Axis n = Axis.None;
						if (tiles.IndexOf(new Vector2(tile.X, tile.Y + (i > 1 ? 1 : -1))) is int v1 && v1 != -1 && !((OutdoorPot)location.Objects[tiles[v1]]).IsBroken)
							n |= Axis.Vertical;
						if (tiles.IndexOf(new Vector2(tile.X + (i % 2 == 1 ? 1 : -1), tile.Y)) is int v2 && v2 != -1 && !((OutdoorPot)location.Objects[tiles[v2]]).IsBroken)
							n |= Axis.Horizontal;
						if ((n & (Axis.Vertical | Axis.Horizontal)) != Axis.None
							&& tiles.IndexOf(new Vector2(tile.X + (i % 2 == 1 ? 1 : -1), tile.Y + (i > 1 ? 1 : -1))) is int v3 && v3 != -1 && !((OutdoorPot)location.Objects[tiles[v3]]).IsBroken)
							n |= Axis.Diagonal;
						if (n == (Axis.Diagonal | Axis.Horizontal))
							n = Axis.Horizontal;
						((OutdoorPot)location.Objects[tile]).Neighbours[i] = n;
					}
				}
			}

			if (specificLocation != null)
				adjustAllInLocation(specificLocation);
			else
				foreach (GameLocation location in Game1.locations)
					adjustAllInLocation(location);
		}
	}
}
