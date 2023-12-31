﻿using Microsoft.Xna.Framework;
using StardewValley;

namespace ChestStorageSystem.UIComponents
{
    public class BorderBox
    {
        public const int defaultBorderThickness = Game1.tileSize / 4;

        public const int defaultPadding = 32;

        /// <summary>
        /// The actual bounds of the box
        /// </summary>
        public Rectangle Bounds
        {
            get { return this._bounds; }
            set { this._bounds = value; this.dirty = true; }
        }

        /// <summary>
        /// When placing content inside the box, use these bounds as this factors in the padding.
        /// </summary>
        public Rectangle ContentBounds
        {
            get
            {
                if (this.dirty)
                {
                    this.RecalculateBounds();
                }

                return this._contentBounds;
            }
        }

        /// <summary>
        /// Boxes are drawn with a border outside the bounds, these bounds represent where those borders will be drawn.
        ///
        /// Setting this directly has no effect on what is drawn, this should only be used to calculate where borders will be.
        /// </summary>
        public Rectangle BorderBounds
        {
            get
            {
                if (this.dirty)
                {
                    this.RecalculateBounds();
                }
                return this._borderBounds;
            }
        }

        public int Padding
        {
            get { return this._padding; }
            set
            {
                this.dirty = true;
                this._padding = value;
            }
        }

        public int BorderThickness
        {
            get { return this._borderThickness; }
            set
            {
                this.dirty = true;
                this._borderThickness = value;
            }
        }

        private Rectangle _bounds;
        private Rectangle _contentBounds;
        private Rectangle _borderBounds;
        private int _padding;
        private int _borderThickness;

        /// <summary>
        /// When this is true the bounds have been changed, but the content and border bounds have not been updated to match.
        /// </summary>
        private bool dirty = true;

        public BorderBox(int x = 0, int y = 0, int width = 0, int height = 0, int padding = defaultPadding, int borderWidth = defaultBorderThickness)
        {
            this._bounds = new Rectangle(x, y, width, height);
            this._padding = padding;
            this._borderThickness = borderWidth;
            this.RecalculateBounds();
        }

        public BorderBox SetPadding(int padding)
        {
            this._padding = padding;
            this.dirty = true;
            return this;
        }

        public BorderBox SetBorderWidth(int borderWidth)
        {
            this._borderThickness = borderWidth;
            this.dirty = true;
            return this;
        }

        public BorderBox SetX(int x)
        {
            this._bounds.X = x;
            this.dirty = true;
            return this;
        }

        public BorderBox SetY(int y)
        {
            this._bounds.Y = y;
            this.dirty = true;
            return this;
        }

        public BorderBox SetWidth(int w)
        {
            this._bounds.Width = w;
            this.dirty = true;
            return this;
        }

        public BorderBox SetHeight(int h)
        {
            this._bounds.Height = h;
            this.dirty = true;
            return this;
        }

        public BorderBox Offset(int x, int y)
        {
            if (x != 0)
            {
                this.SetX(this.Bounds.X + x);
            }
            if (y != 0)
            {
                this.SetY(this.Bounds.Y + y);
            }

            return this;
        }

        public BorderBox ExpandDownTo(int y) => SetHeight(y - (this._bounds.Y + this._borderThickness));

        public BorderBox CenterHorizontally(int onX) => SetX(onX - (this._bounds.Width / 2));

        public BorderBox LeftAlignWith(Rectangle other) => SetX(other.X);

        public BorderBox LeftAlignWith(BorderBox other) => LeftAlignWith(other._bounds);

        public BorderBox RightAlignWith(Rectangle other) => SetX(other.Right - this._bounds.Width);

        public BorderBox RightAlignWith(BorderBox other) => RightAlignWith(other._bounds);

        public BorderBox AligntTopTo(int y) => SetY(y + this._borderThickness);

        public BorderBox AlignBottomTo(int y) => SetY((y - this.BorderBounds.Height) + this.BorderThickness);

        public BorderBox AlignRightTo(int x) => SetX((x - this.BorderBounds.Width) + this.BorderThickness);

        public BorderBox SetContentWidth(int w) => SetWidth(w + (this._padding * 2));

        public BorderBox SetContentHeight(int h) => SetHeight(h + (this._padding * 2));

        public void Draw()
        {
            // Colors: DarkOrange, Olive, ForestGreen, MediumPurple, RosyBrown, SaddleBrown, Peru
            Game1.DrawBox(this._bounds.X, this._bounds.Y, this._bounds.Width, this._bounds.Height);
        }

        /// <summary>
        /// Updates the content and border bounds
        /// </summary>
        private void RecalculateBounds()
        {
            this._contentBounds = this._bounds;
            this._contentBounds.Inflate(-this._padding, -this._padding);

            this._borderBounds = this._bounds;
            this._borderBounds.Inflate(this._borderThickness, this._borderThickness);

            this.dirty = false;
        }
    }
}
