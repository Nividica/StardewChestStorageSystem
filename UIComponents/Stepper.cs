using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChestStorageSystem.UIComponents
{
    public class Stepper : ClickableComponent
    {
        public class OnSteppedArgs
        {
            public int Direction { get; private set; }

            public OnSteppedArgs(int direction)
            {
                this.Direction = direction;
            }
        }

        private const int BG_BORDER_SIZE = 2;

        private static Rectangle MinusButtonTextureCoords = new(178, 347, 5, 5);
        private static Rectangle PlusButtonTextureCoords = new(185, 347, 5, 5);
        private static Rectangle BackgroundTextureCoords = new(256, 256, 10, 10);


        public EventHandler<OnSteppedArgs> OnStepped;

        private readonly ClickableTextureComponent minusButton;
        private readonly ClickableTextureComponent plusButton;
        private readonly Rectangle minusBgTxCoords;
        private readonly Rectangle plusBgTxCoords;
        private Rectangle minusButtonBg;
        private Rectangle plusButtonBg;

        public Stepper(Point position, float scale) : base(new Rectangle(position, Point.Zero), "stepper")
        {
            // Start with the full background
            this.minusBgTxCoords = BackgroundTextureCoords;
            // Chop off the right border
            this.minusBgTxCoords.Width -= BG_BORDER_SIZE;
            // Offset the right so it chops the left border
            this.plusBgTxCoords = minusBgTxCoords;
            this.plusBgTxCoords.X += BG_BORDER_SIZE;

            this.minusButton = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, MinusButtonTextureCoords, 1f);
            this.plusButton = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, PlusButtonTextureCoords, 1f);
            this.minusButtonBg = Rectangle.Empty;
            this.plusButtonBg = Rectangle.Empty;

            this.scale = scale;
            this.RecalculateBounds();
        }

        public void Draw(SpriteBatch batch)
        {
            batch.Draw(Game1.mouseCursors, this.minusButtonBg, this.minusBgTxCoords, Color.White);
            batch.Draw(Game1.mouseCursors, this.plusButtonBg, this.plusBgTxCoords, Color.White);
            this.minusButton.draw(batch, Color.White * 0.5f, 2f);
            this.plusButton.draw(batch, Color.White * 0.5f, 2f);
        }

        public bool PerformHoverAction(int x, int y)
        {
            this.minusButton.tryHover(x, y, 0.1f * this.scale);
            this.plusButton.tryHover(x, y, 0.1f * this.scale);

            return this.containsPoint(x, y);
        }

        public bool ReceiveLeftClick(int x, int y)
        {
            if (this.minusButton.containsPoint(x, y))
            {
                this.OnStepped(this, new OnSteppedArgs(-1));

                Game1.playSoundPitched("drumkit6", 1100);

                return true;
            }

            if (this.plusButton.containsPoint(x, y))
            {
                this.OnStepped(this, new OnSteppedArgs(1));

                Game1.playSoundPitched("drumkit6", 1300);

                return true;
            }

            return this.containsPoint(x, y);
        }

        public void RecalculateBounds()
        {
            this.scale = 4f;
            // Calculate bg bounds
            this.minusButtonBg = new Rectangle(
                this.bounds.X,
                this.bounds.Y,
                (int)(this.minusBgTxCoords.Width * this.scale),
                (int)(this.minusBgTxCoords.Height * this.scale)
            );
            this.plusButtonBg = new Rectangle(
                this.minusButtonBg.Right,
                this.minusButtonBg.Y,
                (int)(this.plusBgTxCoords.Width * this.scale),
                (int)(this.plusBgTxCoords.Height * this.scale)
            );

            // Calculate the button bounds
            this.minusButton.bounds = new Rectangle(
                (int)(this.minusButtonBg.X + (2 * this.scale)),
                (int)(this.minusButtonBg.Y + (2 * this.scale)),
                (int)((MinusButtonTextureCoords.Width + 1) * this.scale),
                (int)((MinusButtonTextureCoords.Height + 1) * this.scale)
            );
            this.plusButton.bounds = new Rectangle(
                this.minusButton.bounds.Right,
                this.minusButton.bounds.Y,
                (int)((PlusButtonTextureCoords.Width + 1) * this.scale),
                (int)((PlusButtonTextureCoords.Height + 1) * this.scale)
            );

            // Set the button scales
            this.minusButton.baseScale = this.minusButton.scale = (this.minusButton.bounds.Width / (float)MinusButtonTextureCoords.Width);
            this.plusButton.baseScale = this.plusButton.scale = (this.plusButton.bounds.Width / (float)PlusButtonTextureCoords.Width);

            // Calculate the overall width and height from the backgrounds
            this.bounds.Height = Math.Max(this.minusButtonBg.Height, this.plusButtonBg.Height);
            this.bounds.Width = this.minusButtonBg.Width + this.plusButtonBg.Width;
        }
    }
}
