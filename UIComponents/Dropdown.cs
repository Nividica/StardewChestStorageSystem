using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ChestStorageSystem.UIComponents
{

    public class DropdownItem<T>
    {
        public string Name;
        public T Value;

        public DropdownItem(string name, T value)
        {
            this.Name = name;
            this.Value = value;
        }
    }

    public abstract class DropdownBase : ClickableComponent
    {
        /// <summary>
        /// True when the dropdown is open and displaying it's options
        /// </summary>
        /// <returns></returns>
        public bool IsOpen
        {
            get
            {
                if (optionsDropDownField_Clicked is null || this.internalDropdown is null)
                {
                    return false;
                }
                // Reflect on the dropdown to determine if it is open
                return (bool)optionsDropDownField_Clicked.GetValue(this.internalDropdown);
            }
        }

        /// <summary>
        /// Private field "clicked" of the OptionsDropDown class
        /// </summary>
        private readonly FieldInfo optionsDropDownField_Clicked = typeof(OptionsDropDown)
            .GetField("clicked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        protected readonly OptionsDropDown internalDropdown;

        /// <summary>
        /// Ensures the same click that opened the dropdown doesn't also close it
        /// </summary>
        private bool ignoreNextLeftClickRelease = false;

        private int scrollOffset = 0;

        public DropdownBase(Rectangle bounds, int selectedIndex) : base(bounds, "dropdown")
        {
            this.internalDropdown = new OptionsDropDown(null, -int.MaxValue, 0, 0)
            {
                selectedOption = selectedIndex
            };
        }

        public void Draw(SpriteBatch batch)
        {
            this.internalDropdown.draw(batch, 0, 0);
        }

        public bool PerformHoverAction(int x, int y)
        {
            // Change the highlighed option in the dropdown
            if (this.IsOpen && this.internalDropdown.dropDownBounds.Contains(x, y))
            {
                int py = this.internalDropdown.dropDownBounds.Y;
                this.internalDropdown.leftClickHeld(x, y - this.scrollOffset);
                this.internalDropdown.dropDownBounds.Y = py;
                return true;
            }

            return false;
        }

        public bool ReceiveLeftClick(int x, int y)
        {
            if (this.IsOpen && this.internalDropdown.dropDownBounds.Contains(x, y))
            {
                // Select option
                this.internalDropdown.leftClickHeld(x, y - this.scrollOffset);
                return true;
            }

            if (this.internalDropdown.bounds.Contains(x, y))
            {
                // Open the dropdown
                this.internalDropdown.receiveLeftClick(x, y);
                this.ignoreNextLeftClickRelease = true;
                this.internalDropdown.dropDownBounds.Y += this.scrollOffset;

                return true;
            }

            return false;
        }

        public void ReleaseLeftClick(int x, int y)
        {
            if (this.ignoreNextLeftClickRelease)
            {
                this.ignoreNextLeftClickRelease = false;
                return;
            }

            if (!this.IsOpen)
            {
                return;
            }

            // Is the mouse over the options?
            // Did the selected category change?
            if (this.internalDropdown.dropDownBounds.Contains(x, y - this.scrollOffset) && this.internalDropdown.selectedOption != this.internalDropdown.startingSelected)
            {
                // Set the current selection as the starting (so that leftClickReleased resets to it) 
                this.internalDropdown.startingSelected = this.internalDropdown.selectedOption;

                // Get the category name
                string category = this.internalDropdown.dropDownOptions[this.internalDropdown.selectedOption];

                this.SelectedIndexChanged();
            }

            // Close the dropdown (fake coords to prevent dirtying Game options)
            this.internalDropdown.leftClickReleased(-1, -1);


        }

        /// <summary>
        /// Scrolls the dropdown list if needed.
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public bool ReceiveScrollWheelAction(int direction)
        {
            // Ignore if the DD isn't open
            if (!this.IsOpen)
            {
                return false;
            }

            // Ignore if the mouse isn't over the DD
            Rectangle ddBounds = this.internalDropdown.dropDownBounds;
            if (!ddBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
            {
                return false;
            }

            // Is the DD large enough to need scrolling?
            if (ddBounds.Height > Game1.uiViewport.Height)
            {
                if (direction > 0)
                {
                    // up -Y
                    if (ddBounds.Bottom > Game1.uiViewport.Height)
                    {
                        this.internalDropdown.dropDownBounds.Y -= this.bounds.Height;
                        this.scrollOffset -= this.bounds.Height;
                    }
                }
                else
                {
                    // down +Y
                    if (ddBounds.Y < 0)
                    {
                        this.internalDropdown.dropDownBounds.Y += this.bounds.Height;
                        this.scrollOffset += this.bounds.Height;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Call this when `bounds` is mutated to update the bounds of the dropdown list
        /// </summary>
        public void RecalculateBounds()
        {
            // Copy the position to the internal dropdown
            this.internalDropdown.bounds.X = this.bounds.X;
            this.internalDropdown.bounds.Y = this.bounds.Y;

            this.internalDropdown.RecalculateBounds();

            // Copy the calculated width and height back here
            this.bounds.Width = this.internalDropdown.bounds.Width;
            this.bounds.Height = this.internalDropdown.bounds.Height;

            this.scrollOffset = 0;
        }

        protected virtual void SelectedIndexChanged() { }
    }


    /// <summary>
    /// Wraps the options dropdown
    /// </summary>
    public class Dropdown<T> : DropdownBase
    {
        public class ItemChangedArgs
        {
            public DropdownItem<T> Item { get; private set; }

            public ItemChangedArgs(DropdownItem<T> item) { Item = item; }
        }

        /// <summary>
        /// List of items and display names to show when the dropdown is opened
        /// </summary>
        public readonly List<DropdownItem<T>> Items;

        public EventHandler<ItemChangedArgs> OnSelectedItemChanged;

        /// <summary>
        /// The currently selected index.
        /// -1 if nothing is selected.
        /// </summary>
        public int SelectedIndex
        {
            get { return this.internalDropdown.selectedOption; }
            set
            {
                int safeValue = Math.Clamp(value, -1, this.Items.Count - 1);
                if (this.internalDropdown.selectedOption == safeValue)
                {
                    return;
                }

                this.internalDropdown.selectedOption = value;
                this.SelectedIndexChanged();
            }
        }

        /// <summary>
        /// The currently selected item.
        /// null if nothing is selected.
        /// </summary>
        public DropdownItem<T> SelectedItem
        {
            get
            {
                if (this.internalDropdown.selectedOption < 0 || this.internalDropdown.selectedOption >= this.Items.Count)
                {
                    return null;
                }

                return this.Items[this.SelectedIndex];
            }
        }

        public Dropdown(Rectangle bounds, List<DropdownItem<T>> items, int selectedIndex) : base(bounds, selectedIndex)
        {
            this.Items = items;
            this.UpdateDropdownList();
        }

        /// <summary>
        /// Call this when the `Items` list is mutated.
        ///
        /// Recalculates the bounds before returning.
        /// </summary>
        public void UpdateDropdownList()
        {
            List<string> options = this.Items.Select((item) => item.Name).ToList();
            this.internalDropdown.dropDownOptions = options;
            this.internalDropdown.dropDownDisplayOptions = options;
            this.RecalculateBounds();
            this.SelectedIndex = this.internalDropdown.selectedOption;
        }

        protected override void SelectedIndexChanged()
        {
            this.OnSelectedItemChanged?.Invoke(this, new ItemChangedArgs(this.SelectedItem));
        }
    }
}
