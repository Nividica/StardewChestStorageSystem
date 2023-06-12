using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ChestStorageSystem.UIComponents
{

    /// <summary>
    /// Wraps the options dropdown
    /// </summary>
    public class Dropdown<T> : ClickableComponent
    {
        public class DropdownItem
        {
            public string name;
            public T value;
        }

        public class ItemChangedArgs
        {
            public DropdownItem Item { get; private set; }

            public ItemChangedArgs(DropdownItem item) { Item = item; }
        }

        /// <summary>
        /// List of items and display names to show when the dropdown is opened
        /// </summary>
        public readonly List<DropdownItem> Items;

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
        public DropdownItem SelectedItem
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

        private readonly OptionsDropDown internalDropdown;

        /// <summary>
        /// Ensures the same click that opened the dropdown doesn't also close it
        /// </summary>
        private bool ignoreNextLeftClickRelease = false;

        public Dropdown(Rectangle bounds, List<DropdownItem> items, int selectedIndex) : base(bounds, "dropdown")
        {
            this.Items = items;
            this.internalDropdown = new OptionsDropDown(null, -int.MaxValue, 0, 0)
            {
                selectedOption = selectedIndex
            };
            this.UpdateDropdownList();
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
                this.internalDropdown.leftClickHeld(x, y);
                return true;
            }

            return false;
        }

        public bool ReceiveLeftClick(int x, int y)
        {

            if (this.IsOpen && this.internalDropdown.dropDownBounds.Contains(x, y))
            {
                // Select option
                this.internalDropdown.leftClickHeld(x, y);
                return true;
            }

            if (this.internalDropdown.bounds.Contains(x, y))
            {
                // Open the dropdown
                this.internalDropdown.receiveLeftClick(x, y);
                this.ignoreNextLeftClickRelease = true;

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
            if (this.internalDropdown.dropDownBounds.Contains(x, y) && this.internalDropdown.selectedOption != this.internalDropdown.startingSelected)
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
        /// Call this when the `Items` list is mutated.
        ///
        /// Recalculates the bounds before returning.
        /// </summary>
        public void UpdateDropdownList()
        {
            List<string> options = this.Items.Select((item) => item.name).ToList();
            this.internalDropdown.dropDownOptions = options;
            this.internalDropdown.dropDownDisplayOptions = options;
            this.RecalculateBounds();
            this.SelectedIndex = this.internalDropdown.selectedOption;
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
        }


        private void SelectedIndexChanged()
        {
            this.OnSelectedItemChanged?.Invoke(this, new ItemChangedArgs(this.SelectedItem));
        }

    }
}
