using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;

namespace ChestStorageSystem.UIComponents
{
    public class Gauge
    {
        public static readonly Rectangle GaugeTextureCoords = new(268, 419, 12, 45);

        public float Value
        {
            get { return this._value; }
            set
            {
                if (value == this._value) return;
                this._value = Math.Clamp(value, 0f, 1f);
                this.RecalculateBounds();
            }
        }

        public Rectangle Bounds;

        private float _value;
        private Rectangle barBounds = new();
        private Color barColor = Color.Transparent;

        public Gauge(Rectangle bounds, float initialValue = 0.0f)
        {
            this.Bounds = bounds;
            this._value = initialValue;
            this.RecalculateBounds();
        }

        public void Draw(SpriteBatch batch)
        {
            // Draw the border
            batch.Draw(Game1.mouseCursors, this.Bounds, GaugeTextureCoords, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 1f);

            // Draw the bar
            batch.Draw(Game1.staminaRect, this.barBounds, Game1.staminaRect.Bounds, this.barColor, 0f, Vector2.Zero, SpriteEffects.None, 1f);
        }

        public void RecalculateBounds()
        {
            // Calculate the scaling
            float hScale = this.Bounds.Width / (float)(GaugeTextureCoords.Width);
            float vScale = this.Bounds.Height / (float)(GaugeTextureCoords.Height);

            // Copy the border bounds
            this.barBounds = this.Bounds;

            // Deflate/Shrink the bar bounds by the scale
            this.barBounds.Inflate((int)Math.Round(-3 * hScale), (int)Math.Round(-2 * vScale));

            // Reposition based on the percentage
            int bottomAnchor = this.barBounds.Bottom;
            this.barBounds.Height = (int)(this.barBounds.Height * this._value);
            this.barBounds.Y = bottomAnchor - this.barBounds.Height;

            // Get the color of the bar
            this.barColor = Utility.getRedToGreenLerpColor(this._value);

        }
    }
}
