using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaisedGardenBeds
{
	public class NewRecipeMenu : IClickableMenu
	{
		public Dictionary<string, int> NewVarieties = new Dictionary<string, int>();
		public ClickableTextureComponent OkButton;
		public ClickableTextureComponent StarIcon;
		private MouseState _oldMouseState;
		private int _timerBeforeStart;
		private bool _informationUp;
		private bool _isActive;

		public static readonly Vector2 Dimensions = new Vector2(768, 512);
		private const int OkButtonId = 101;


		public NewRecipeMenu(List<string> newVarieties)
			: base(x: 0,  y: 0, width: 0,  height: 0)
		{
			this.NewVarieties = newVarieties.ToDictionary(variety => variety, variety => OutdoorPot.GetParentSheetIndexFromName(variety));
			this.width = (int)Dimensions.X;
			this.height = ((int)Dimensions.Y / 2) + (NewVarieties.Count * Game1.smallestTileSize * 2 * Game1.pixelZoom);
			this.OkButton = new ClickableTextureComponent(
				bounds: Rectangle.Empty,
				texture: Game1.mouseCursors,
				sourceRect: Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46),
				scale: 1f)
			{
				myID = NewRecipeMenu.OkButtonId
			};

			this._timerBeforeStart = 250;
			Game1.player.completelyStopAnimatingOrDoingAction();
			Game1.player.freezePause = 100;
			this.gameWindowSizeChanged(Rectangle.Empty, Rectangle.Empty);
			this.populateClickableComponentList();
		}

		public override void snapToDefaultClickableComponent()
		{
			this.currentlySnappedComponent = this.OkButton;
			this.snapCursorToCurrentSnappedComponent();
		}

		public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
		{
			this.xPositionOnScreen = (Game1.uiViewport.Width / 2) - (this.width / 2);
			this.yPositionOnScreen = (Game1.uiViewport.Height / 2) - (this.height / 2);
			this.OkButton.bounds = new Rectangle(
				this.xPositionOnScreen + this.width + 4,
				this.yPositionOnScreen + this.height - 64 - IClickableMenu.borderWidth,
				64,
				64);
		}

		public override void receiveLeftClick(int x, int y, bool playSound = true)
		{
		}

		public override void receiveRightClick(int x, int y, bool playSound = true)
		{
		}

		public override void performHoverAction(int x, int y)
		{
		}

		public override void receiveKeyPress(Keys key)
		{
			if (Game1.options.SnappyMenus
				&& !Game1.options.doesInputListContain(Game1.options.cancelButton, key)
				&& !Game1.options.doesInputListContain(Game1.options.menuButton, key))
			{
				base.receiveKeyPress(key);
			}
		}

		public override void receiveGamePadButton(Buttons b)
		{
			base.receiveGamePadButton(b);
			if ((b == Buttons.Start || b == Buttons.B) && this._isActive)
			{
				this.OkButtonClicked();
			}
		}

		public override void update(GameTime time)
		{
			if (!this._isActive)
			{
				this.exitThisMenu();
				return;
			}
			
			if (this._timerBeforeStart > 0)
			{
				this._timerBeforeStart -= time.ElapsedGameTime.Milliseconds;
				if (this._timerBeforeStart <= 0 && Game1.options.SnappyMenus)
				{
					this.populateClickableComponentList();
					this.snapToDefaultClickableComponent();
				}
				return;
			}

			this._oldMouseState = Game1.input.GetMouseState();
			if (this._isActive && !this._informationUp && this.StarIcon != null)
			{
				if (this.StarIcon.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
				{
					this.StarIcon.sourceRect.X = 294;
				}
				else
				{
					this.StarIcon.sourceRect.X = 310;
				}
			}
			if (this._isActive && this.StarIcon != null && !this._informationUp
				&& (this._oldMouseState.LeftButton == ButtonState.Pressed || (Game1.options.gamepadControls && Game1.oldPadState.IsButtonDown(Buttons.A)))
				&& this.StarIcon.containsPoint(this._oldMouseState.X, this._oldMouseState.Y))
			{
				this._informationUp = true;
				Game1.player.completelyStopAnimatingOrDoingAction();
				Game1.playSound("bigSelect");
				Game1.player.freezePause = 100;
			}

			if (!this._isActive || !this._informationUp)
				return;
			
			Game1.player.completelyStopAnimatingOrDoingAction();
			if (this.OkButton.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
			{
				this.OkButton.scale = Math.Min(1.1f, this.OkButton.scale + 0.05f);
				if ((this._oldMouseState.LeftButton == ButtonState.Pressed || (Game1.options.gamepadControls && Game1.oldPadState.IsButtonDown(Buttons.A))) && readyToClose())
				{
					this.OkButtonClicked();
				}
			}
			else
			{
				this.OkButton.scale = Math.Max(1f, this.OkButton.scale - 0.05f);
			}
			Game1.player.freezePause = 100;
		}

		public override void draw(SpriteBatch b)
		{
			if (this._timerBeforeStart > 0)
				return;

			// Blackout
			b.Draw(
				texture: Game1.fadeToBlackRect,
				destinationRectangle: new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
				color: Color.Black * 0.5f);
			
			// ???
			b.Draw(
				texture: Game1.mouseCursors,
				position: new Vector2(
					this.xPositionOnScreen + (width / 2) - 116,
					this.yPositionOnScreen - 32 + 12),
				sourceRectangle: new Rectangle(363, 87, 58, 22),
				color: Color.White,
				rotation: 0f,
				origin: Vector2.Zero,
				scale: Game1.pixelZoom,
				effects: SpriteEffects.None,
				layerDepth: 1f);
			
			if (!this._informationUp && this._isActive && this.StarIcon != null)
			{
				this.StarIcon.draw(b);
			}
			else
			{
				if (!this._informationUp)
					return;
				
				Game1.drawDialogueBox(
					x: this.xPositionOnScreen,
					y: this.yPositionOnScreen,
					width: this.width,
					height: this.height,
					speaker: false,
					drawOnlyBox: true);

				int x = this.xPositionOnScreen + (this.width / 2);
				int y = this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 80;
				
				foreach (KeyValuePair<string, int> nameAndIndex in NewVarieties)
				{
					string crafting = Game1.content.LoadString("Strings\\UI:LearnedRecipe_crafting");
					string message = Game1.content.LoadString("Strings\\UI:LevelUp_NewRecipe", crafting, ModEntry.Instance.i18n.Get($"item.name.{nameAndIndex.Key}"));
					int xOffset = (int)((Game1.smallFont.MeasureString(message).X / 2) - (Game1.smallestTileSize * Game1.pixelZoom));
					b.DrawString(
						spriteFont: Game1.smallFont,
						text: message,
						position: new Vector2(
							x - xOffset,
							y + ((Game1.smallestTileSize + 3) * 2)),
						color: Game1.textColor);

					b.Draw(
						texture: Game1.bigCraftableSpriteSheet,
						sourceRectangle: StardewValley.Object.getSourceRectForBigCraftable(nameAndIndex.Value),
						position: new Vector2(
							x - xOffset - (Game1.smallestTileSize * 1.5f * Game1.pixelZoom),
							y - (Game1.smallestTileSize * Game1.pixelZoom)),
						color: Color.White,
						rotation: 0f,
						origin: Vector2.Zero,
						scale: Game1.pixelZoom,
						effects: SpriteEffects.None,
						layerDepth: 1f);
					
					y += (Game1.smallestTileSize + 1) * 2 * Game1.pixelZoom;
				}
				this.OkButton.draw(b);
				
				if (!Game1.options.SnappyMenus)
				{
					Game1.mouseCursorTransparency = 1f;
					this.drawMouse(b);
				}
			}
		}

		private void OkButtonClicked()
		{
			this._isActive = false;
			this._informationUp = false;
		}

		public static void Push(List<string> newVarieties)
		{
			if (newVarieties == null)
				return;

			for (int i = newVarieties.Count - 1; i >= 0; --i)
			{
				if (!ModEntry.ItemDefinitions.ContainsKey(newVarieties[i]))
				{
					newVarieties.RemoveAt(i);
				}
			}
			if (newVarieties.Count > 0)
			{
				Game1.endOfNightMenus.Push(new NewRecipeMenu(newVarieties: newVarieties));
			}
		}
	}
}
