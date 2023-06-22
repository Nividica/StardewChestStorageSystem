using ChestStorageSystem.UIComponents;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChestStorageSystem.Menus
{
#if DEBUG
    internal class TextureExplorerMenu : IClickableMenu
    {
        /// <summary>
        /// In Texture space
        /// </summary>
        private Vector2 offset = Vector2.Zero;

        /// <summary>
        /// In Screen space
        /// </summary>
        private Vector2 dragStart = Vector2.Zero;

        /// <summary>
        /// In Texture space
        /// </summary>
        private Vector2 dragOffset = Vector2.Zero;

        /// <summary>
        /// In Texture space
        /// </summary>
        private Vector2 mousePositionInTexture = Vector2.Zero;

        /// <summary>
        /// In Texture space
        /// </summary>
        private Vector2 alignedMousePositionInTexture = Vector2.Zero;

        private Dropdown<Texture2D> textureDropdown;

        private Texture2D selectedTexture;

        private float scale = 1f;

        public TextureExplorerMenu()
        {
            List<DropdownItem<Texture2D>> textureOptions = new();

            var gameTextures = this.GetTextures();

            foreach (var (name, texture) in gameTextures)
            {
                textureOptions.Add(new DropdownItem<Texture2D>(name, texture));
            }

            this.textureDropdown = new(new Rectangle(), textureOptions, 0);
            this.textureDropdown.bounds.X = Game1.uiViewport.Width - this.textureDropdown.bounds.Width + 8;
            this.textureDropdown.RecalculateBounds();
            this.textureDropdown.OnSelectedItemChanged += (sender, args) =>
            {
                this.selectedTexture = args.Item.Value;
                this.scale = 1f;
                this.SetOffsets(0, 0);
            };

            this.selectedTexture = this.textureDropdown.SelectedItem.Value;
            this.SetOffsets(0, 0);
        }

        public override void draw(SpriteBatch batch)
        {
            // BG
            batch.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Tan * 0.98f);

            // Texture
            if (this.selectedTexture is not null)
            {
                batch.Draw(selectedTexture, this.offset * this.scale, null, Color.White, 0f, Vector2.Zero, this.scale, SpriteEffects.None, 1f);
            }

            // Status text
            string tip = $"x{this.scale} | ({alignedMousePositionInTexture.X},{alignedMousePositionInTexture.Y})";
            batch.DrawString(Game1.smallFont, tip, Vector2.Zero, Color.White);
            batch.DrawString(Game1.smallFont, tip, new Vector2(0, 2), Color.White * 0.5f);
            batch.DrawString(Game1.smallFont, tip, new Vector2(2, 0), Color.White * 0.25f);
            batch.DrawString(Game1.smallFont, tip, new Vector2(2, 2), Color.Gray);
            batch.DrawString(Game1.smallFont, tip, Vector2.One, Color.Black * 0.75f);

            // Hover highlight
            if (this.scale >= 16f)
            {
                Vector2 highlightPos = this.TextureSpaceToScreenSpace(alignedMousePositionInTexture);
                batch.Draw(Game1.mouseCursors, new Rectangle((int)highlightPos.X, (int)highlightPos.Y, (int)this.scale, (int)this.scale), new Rectangle(448, 128, 64, 64), Color.White * 0.75f);
            }

            // Texture dropdown
            this.textureDropdown.Draw(batch);

            // Cursor
            this.drawMouse(batch, true);
        }

        public override void performHoverAction(int x, int y)
        {
            if (this.textureDropdown.PerformHoverAction(x, y))
            {
                return;
            }

            this.UpdateMousePositionInTexture(x, y);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.textureDropdown.ReceiveLeftClick(x, y))
            {
                return;
            }

            this.dragStart.X = x;
            this.dragStart.Y = y;
            this.dragOffset = this.offset;
            this.UpdateMousePositionInTexture(x, y);
        }

        public override void leftClickHeld(int x, int y)
        {
            if (this.textureDropdown.IsOpen)
            {
                return;
            }

            this.SetOffsets(
                this.dragOffset.X + ((x - this.dragStart.X) / this.scale),
                this.dragOffset.Y + ((y - this.dragStart.Y) / this.scale)
            );
            this.UpdateMousePositionInTexture(x, y);
        }

        public override void releaseLeftClick(int x, int y)
        {
            this.textureDropdown.ReleaseLeftClick(x, y);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            if (this.textureDropdown.ReceiveScrollWheelAction(direction))
            {
                return;
            }

            // We want the mouse position within the texture to be the same after scaling
            // In other words, zoom into the cursor
            this.UpdateMousePositionInTexture(null, null);
            Vector2 prevScreenPos = this.TextureSpaceToScreenSpace(this.alignedMousePositionInTexture);

            // Adjust the scaling in factors of 2
            this.scale = (direction > 0) ? this.scale * 2 : Math.Max(1f, this.scale / 2);

            // Calculate where the mouse would be now if it were over the same texture pixel it was before
            Vector2 newScreenPos = this.TextureSpaceToScreenSpace(this.alignedMousePositionInTexture);

            // Update the offset
            this.SetOffsets(
                this.offset.X + ((prevScreenPos.X - newScreenPos.X) / this.scale),
                this.offset.Y + ((prevScreenPos.Y - newScreenPos.Y) / this.scale)
            );
            this.UpdateMousePositionInTexture(null, null);

        }

        private void UpdateMousePositionInTexture(int? x = null, int? y = null)
        {
            this.mousePositionInTexture = this.ScreenSpaceToTextureSpace(new Vector2(x ?? Game1.getOldMouseX(), y ?? Game1.getOldMouseY()));
            this.alignedMousePositionInTexture = new Vector2(
                (float)Math.Floor(this.mousePositionInTexture.X),
                (float)Math.Floor(this.mousePositionInTexture.Y)
            );
        }

        private Vector2 ScreenSpaceToTextureSpace(Vector2 screenSpace)
        {
            return new Vector2(
                (float)((screenSpace.X / this.scale) - this.offset.X),
                (float)((screenSpace.Y / this.scale) - this.offset.Y)
            );
        }

        private Vector2 TextureSpaceToScreenSpace(Vector2 textureSpace)
        {
            return new Vector2(
                (float)((textureSpace.X + this.offset.X) * this.scale),
                (float)((textureSpace.Y + this.offset.Y) * this.scale)
            );
        }

        private void SetOffsets(float x, float y)
        {
            if (this.selectedTexture is null)
            {
                return;
            }
            Vector2 screenBR = new(Game1.uiViewport.Width / this.scale, Game1.uiViewport.Height / this.scale);

            // Allow the texture, at any scale, to take up at minimum half of the screen
            Vector2 screenCenter = screenBR / 2f;

            // Don't allow the right side of the texture to go beyond the center
            float minX = screenCenter.X - this.selectedTexture.Width;
            float maxX = screenCenter.X;
            float minY = screenCenter.Y - this.selectedTexture.Height;
            float maxY = screenCenter.Y;


            this.offset.X = Math.Clamp(x, minX, maxX);
            this.offset.Y = Math.Clamp(y, minY, maxY);
        }

        private IEnumerable<(string name, Texture2D texture)> GetTextures()
        {
            var textureFields = typeof(Game1).GetFields(BindingFlags.Static | BindingFlags.Public)
                .Select((field) => field.GetValue(null) is Texture2D tex ? (field.Name, tex) : (null, null));

            var textureProperties = typeof(Game1).GetProperties(BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public)
                .Select((prop) => prop.CanRead && prop.GetValue(null, null) is Texture2D tex ? (prop.Name, tex) : (null, null));

            return textureFields.Concat(textureProperties)
                .Where((p) => p.tex is not null);
        }

    }
#endif
}
