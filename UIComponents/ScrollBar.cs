using StardewValley.Menus;
using StardewValley;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChestStorageSystem.UIComponents
{
    public class ScrollBar : ClickableComponent
    {
        public class ValueChangedArgs
        {
            public int Value { get; private set; }

            public ValueChangedArgs(int value) { Value = value; }
        }

        public static readonly Rectangle UpButtonTextureCoords = new(421, 459, 11, 12);
        public static readonly Rectangle DownButtonTextureCoords = new(421, 472, 11, 12);
        public static readonly Rectangle ThumbButtonTextureCoords = new(435, 463, 6, 10);
        public static readonly Rectangle TrackTextureCoords = new(403, 383, 6, 6);

        /// <summary>
        /// Distance between the buttons and the track
        /// </summary>
        public int Gap = 12;

        public int Steps
        {
            get { return this._steps; }
            set
            {
                this.UpdateThumb(this._value, value);
            }
        }

        public int Value
        {
            get { return this._value; }
            set
            {
                this.UpdateThumb(value, this._steps);
            }
        }

        public EventHandler<ValueChangedArgs> OnValueChanged;

        private int _steps = -1;
        private int _value = -1;
        private double dynamicThumbHeight = 0.0;
        private bool isDragging = false;
        private readonly ClickableTextureComponent upButton;
        private readonly ClickableTextureComponent downButton;
        private readonly ClickableTextureComponent thumb;
        private Rectangle track;

        public ScrollBar(Rectangle bounds, int steps, int initialValue = 0) : base(bounds, "scrollbar")
        {
            this.bounds.Width = 44;
            this.upButton = new ClickableTextureComponent(new Rectangle(0, 0, 44, 48), Game1.mouseCursors, UpButtonTextureCoords, 4f);
            this.downButton = new ClickableTextureComponent(new Rectangle(0, 0, 44, 48), Game1.mouseCursors, DownButtonTextureCoords, 4f);
            this.thumb = new ClickableTextureComponent(new Rectangle(0, 0, 24, 40), Game1.mouseCursors, ThumbButtonTextureCoords, 4f);
            this.track = new Rectangle(0, 0, this.thumb.bounds.Width, 0);

            this._steps = steps;
            this._value = initialValue;

            this.RecalculatePositions();
        }

        public void Draw(SpriteBatch batch)
        {
            if (!this.visible) return;

            // Draw the track
            IClickableMenu.drawTextureBox(batch, Game1.mouseCursors, TrackTextureCoords, this.track.X, this.track.Y, this.track.Width, this.track.Height, Color.White, 4f);

            // Then the buttons
            this.upButton.draw(batch);
            this.downButton.draw(batch);

            // ToDo: Clean this up
            // And lastly the thumb
            // Head cap
            int scale = 4;
            int capSize = 4;
            Rectangle headTBTC = ThumbButtonTextureCoords;
            Rectangle headBounds = this.thumb.bounds;
            headTBTC.Height = capSize;
            headBounds.Height = capSize * scale;
            batch.Draw(Game1.mouseCursors, headBounds, headTBTC, Color.White);

            // Body
            if (this.thumb.bounds.Height > 28)
            {
                Rectangle bodyTBTC = ThumbButtonTextureCoords;
                Rectangle bodyBounds = this.thumb.bounds;
                bodyTBTC.Height = ThumbButtonTextureCoords.Height - (capSize * 2);
                bodyTBTC.Y += capSize;
                bodyBounds.Height = this.thumb.bounds.Height - (scale * (capSize * 2));
                bodyBounds.Y += scale * capSize;
                batch.Draw(Game1.mouseCursors, bodyBounds, bodyTBTC, Color.White);
            }

            // Tail cap
            Rectangle tailTBTC = ThumbButtonTextureCoords;
            Rectangle tailBounds = this.thumb.bounds;
            tailTBTC.Height = capSize;
            tailTBTC.Y = ThumbButtonTextureCoords.Bottom - tailTBTC.Height;
            tailBounds.Height = tailTBTC.Height * scale;
            tailBounds.Y = this.thumb.bounds.Bottom - tailBounds.Height;
            batch.Draw(Game1.mouseCursors, tailBounds, tailTBTC, Color.White);
        }

        public void RecalculatePositions()
        {
            this.upButton.bounds.X = this.bounds.X;
            this.upButton.bounds.Y = this.bounds.Y;

            this.downButton.bounds.X = this.bounds.X;
            this.downButton.bounds.Y = this.bounds.Bottom - this.downButton.bounds.Height;

            this.track.X = 1 + ((this.upButton.bounds.X + (this.upButton.bounds.Width / 2)) - (this.track.Width / 2));
            this.track.Y = this.upButton.bounds.Bottom + this.Gap;
            this.track.Height = this.downButton.bounds.Y - this.Gap - this.track.Y;

            this.thumb.bounds.X = this.track.X;

            this.UpdateThumb(this._value, this._steps);
        }

        public void PerformHoverAction(int x, int y)
        {
            // ToDo: Constants/Properties
            this.upButton.scale = this.upButton.containsPoint(x, y) ? 4.3f : 4f;
            this.downButton.scale = this.downButton.containsPoint(x, y) ? 4.3f : 4f;
        }

        public bool ReceiveLeftClick(int x, int y)
        {
            if (!this.bounds.Contains(x, y))
            {
                return false;
            }

            if (this.upButton.containsPoint(x, y))
            {
                this.Value--;
                this.upButton.scale = 4f;
            }
            else if (this.downButton.containsPoint(x, y))
            {
                this.Value++;
                this.downButton.scale = 4f;
            }
            else if (this._steps > 0)
            {
                this.isDragging = true;
                this.MoveThumbToMouse(y);
            }

            return true;
        }

        public bool LeftClickHeld(int x, int y)
        {
            if (!this.isDragging)
            {
                return false;
            }

            this.MoveThumbToMouse(y);

            return true;
        }

        public void ReleaseLeftClick()
        {
            this.isDragging = false;
        }

        private void MoveThumbToMouse(int y)
        {
            if (this._steps == 0)
            {
                return;
            }
            double mouseYPercent = Math.Clamp((y - this.track.Y - (this.dynamicThumbHeight / 2.0)) / (this.track.Height - this.dynamicThumbHeight), 0.0, 1.0);
            this.Value = (int)Math.Round(mouseYPercent * this._steps);
        }

        private void UpdateThumb(int proposedValue, int proposedSteps)
        {
            // Bounds checks
            int oldValue = this._value;
            this._steps = Math.Max(0, proposedSteps);
            this._value = Math.Clamp(proposedValue, 0, this._steps);

            // What percentage is the value in steps?
            double offsetPercent = this._steps == 0 ? 0.0 : (this._value / (double)this._steps);

            // Calculate the height of the thumb
            this.dynamicThumbHeight = Math.Max(28.0, this.track.Height / (this._steps + 1.0));

            // Calculate the Y offset of the thumb
            double thumbOffset = (this.track.Height - this.dynamicThumbHeight) * offsetPercent;

            // Update the thumb bounds
            this.thumb.bounds.Y = this.track.Y + (int)Math.Round(thumbOffset);
            this.thumb.bounds.Height = (int)Math.Round(this.dynamicThumbHeight);

            // Did the value change?
            if (oldValue != this._value)
            {
                this.OnValueChanged(this, new ValueChangedArgs(this._value));
            }
        }
    }
}
