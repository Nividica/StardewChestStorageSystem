using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChestStorageSystem.Storage;
using ChestStorageSystem.UIComponents;
using StardewObject = StardewValley.Object;

namespace ChestStorageSystem.Menus
{
    // Note: Extending ItemGrabMenu makes this play much nicer with other mods, like UI Info Suite 2.
    public class AggregationMenu : ItemGrabMenu
    {
        private enum TimeOfDay
        {
            Day = 0,
            Sunset = 1,
            Night = 2,
        }

        /// <summary>
        /// Remembers what category was selected after the UI is closed
        /// </summary>
        [InstancedStatic]
        private static string categoryRecall = null;

        private static Rectangle SunsetBgTextureCoords = new(639, 858, 1, 144);
        private static Rectangle RainBgTextureCoords = new(640, 858, 1, 184);
        private static Rectangle StarsTextureCoords = new(0, 1453, 640, 195);

        private static InventoryMenu BuildPlayerMenu(int x, int y, int rows, int cols)
        {
            return new InventoryMenu(
                x, y,
                // Not the default player inventory menu
                false,
                // Inventory source is the players inventory
                Game1.player.Items,
                // No special highlighting
                null,
                // Capacity of the inventory
                rows * cols,
                // Number of rows drawn (if it is evenly divisble by capacity!)
                rows)
            {
                showGrayedOutSlots = true,
            };
        }

        private static OptionsDropDown BuildCategoryDropdown(List<StorageOrigin> storages)
        {
            HashSet<string> categories = new(
                // Get the categories from the storages
                storages.Select((storage) => storage.Category)
                // Add the "All" category
                .Prepend(null)
            );

            OptionsDropDown categoryDropdown = new(null, -int.MaxValue, 0, 0)
            {
                dropDownOptions = categories.ToList(),
                dropDownDisplayOptions = categories.Select((catKey) => string.IsNullOrEmpty(catKey) ? "All Categories" : (catKey.Length >= 26 ? catKey[..26] : catKey)).ToList(),
            };

            // Did the user have a previous category set?
            if (categoryRecall is not null)
            {
                int catIdx = categoryDropdown.dropDownOptions.IndexOf(categoryRecall);
                if (catIdx != -1)
                {
                    categoryDropdown.selectedOption = catIdx;
                }
            }

            return categoryDropdown;
        }

        private readonly InventoryMenu playerInventoryMenu;

        /// <summary>
        /// Aggregates all known inventories together
        /// </summary>
        private readonly StorageAggregator aggro = null;

        /// <summary>
        /// Allows the user to scrope the aggregate to a single category
        /// </summary>
        private readonly OptionsDropDown categoryDropdown;

        /// <summary>
        /// Private field "clicked" of the OptionsDropDown class
        /// </summary>
        private readonly FieldInfo optionsDropDownField_Clicked = typeof(OptionsDropDown)
            .GetField("clicked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Allows the user to search for items
        /// </summary>
        private readonly TextBox searchTextbox;

        /// <summary>
        /// Scrolls the AggroMenu
        /// </summary>
        private readonly ScrollBar scrollbar;

        /// <summary>
        /// Displays how much free space there is
        /// </summary>
        private readonly Gauge capacityGauge;

        /// <summary>
        /// Changes which background underlay is used.
        /// </summary>
        private TimeOfDay timeOfDay = TimeOfDay.Day;

        /// <summary>
        /// Ensures the same click that opened the dropdown doesn't also close it
        /// </summary>
        private bool categoryDropdownHeldOpen = false;

        /// <summary>
        /// Aggregated inventories graphical menu
        /// </summary>
        private InventoryMenu aggroMenu = null;

        private string hoverTitle = "";

        private readonly int aggroMenuColumnCount;
        private readonly int aggroMenuRowCount;

        private bool invertShiftTransfering = false;

        private readonly BorderBox playerInventoryBox = new();
        private readonly BorderBox aggroInventoryBox = new();
        private readonly BorderBox dropDownBox = new();
        private readonly BorderBox searchBox = new();

        public AggregationMenu() : base(new List<Item>(), false, false, null, null, null)
        {
            // Build the storage list
            List<StorageOrigin> storages = StorageOrigin.BuildStorageList();

            int playerInventoryCols = 12;
            int playerInventoryRows =
             (int)Math.Ceiling(Game1.player.MaxItems / (float)playerInventoryCols);


            List<BorderBox> allBoxes = new() { this.dropDownBox, this.aggroInventoryBox, this.playerInventoryBox, this.searchBox };

            bool smallRes = Game1.uiViewport.Height <= 800;
            int gap = smallRes ? 0 : 32;
            int centerX = (Game1.uiViewport.Width / 2);
            if (smallRes)
            {
                allBoxes.ForEach((box) => box.Padding = BorderBox.defaultPadding / 2);
            }

            // Bounds of the player inventory window
            this.playerInventoryBox
                .SetContentWidth((Game1.tileSize * playerInventoryCols))
                .SetContentHeight((Game1.tileSize * playerInventoryRows))
                .CenterHorizontally(centerX)
                .AlignBottomTo(Game1.uiViewport.Height - gap);

            // Create the player inventory menu
            this.playerInventoryMenu = BuildPlayerMenu(
                this.playerInventoryBox.ContentBounds.X,
                this.playerInventoryBox.ContentBounds.Y,
                playerInventoryRows,
                playerInventoryCols
            );

            // Create category dropdown
            this.categoryDropdown = BuildCategoryDropdown(storages);

            // Bounds of the dropdown window
            int dropdownImplicitRightMargin = 8;
            this.dropDownBox.Padding = 0;
            this.dropDownBox
                .AligntTopTo(gap)
                .SetContentHeight(this.categoryDropdown.bounds.Height)
                .SetContentWidth(this.categoryDropdown.bounds.Width - dropdownImplicitRightMargin);

            int availableWidth = (Game1.uiViewport.Width - ((this.aggroInventoryBox.BorderWidth + this.aggroInventoryBox.Padding) * 2));
            this.aggroMenuColumnCount = Math.Min(18, availableWidth / Game1.tileSize);

            // Dimensions of the aggro window
            this.aggroInventoryBox
                .SetContentWidth(Game1.tileSize * this.aggroMenuColumnCount)
                .CenterHorizontally(centerX)
                // Overlap borders of the aggro and dropdown window
                .SetY(this.dropDownBox.BorderBounds.Bottom)
                .ExpandDownTo(this.playerInventoryBox.BorderBounds.Top - gap);


            // InventoryMenu quirks
            // 1. Drawing starts at position.X - 4px
            // 2. Slots are 64px wide and tall, BUT there are 4px tall gaps between slot rows
            int slotVerticalGap = 4;
            int slotHeight = Game1.tileSize + slotVerticalGap;

            // Shrink down to an even number of rows

            // Add a fake gap at the bottom to make the math easier, then remove it from the final height
            this.aggroMenuRowCount = Math.Max(1, (this.aggroInventoryBox.ContentBounds.Height + slotVerticalGap) / slotHeight);
            this.aggroInventoryBox.SetContentHeight((slotHeight * this.aggroMenuRowCount) - (3 * slotVerticalGap));

            // Reposition the dropdown window (align right)
            // and move the dropdown into the window
            this.dropDownBox.RightAlignWith(this.aggroInventoryBox);
            this.categoryDropdown.bounds.X = this.dropDownBox.Bounds.X;
            this.categoryDropdown.bounds.Y = this.dropDownBox.Bounds.Y;
            this.categoryDropdown.RecalculateBounds();

            // Bounds of the user search textbox
            this.searchBox.Padding = 0;
            this.searchBox
                .AligntTopTo(gap)
                .LeftAlignWith(this.aggroInventoryBox)
                .SetContentHeight(this.categoryDropdown.bounds.Height)
                .SetContentWidth(this.categoryDropdown.bounds.Width);

            // Create the user search textbox
            int textboxImplicitLeftPadding = 8;
            this.searchTextbox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Color.Black)
            {
                X = this.searchBox.ContentBounds.X - textboxImplicitLeftPadding,
                Y = this.searchBox.ContentBounds.Y,
                Width = this.searchBox.ContentBounds.Width + textboxImplicitLeftPadding,
                Text = "",
                Selected = true,
            };

            // Create the scrollbar
            this.scrollbar = new ScrollBar(
                new Rectangle(
                    this.aggroInventoryBox.BorderBounds.Right + 8,
                    this.aggroInventoryBox.BorderBounds.Y,
                    0,
                    this.aggroInventoryBox.BorderBounds.Height
                ),
                0, 0
            );
            // Make sure it doesn't go off screen
            if (this.scrollbar.bounds.Right > Game1.uiViewport.Width)
            {
                this.scrollbar.bounds.X = Game1.uiViewport.Width - this.scrollbar.bounds.Width;
                this.scrollbar.RecalculatePositions();
            }
            // Attach to the change event
            // Note: Since I have no reason to ever detach, I am using a simple lambda here
            this.scrollbar.OnValueChanged += (sender, args) =>
            {
                this.aggro.SlotShift = args.Value * this.aggroMenuColumnCount;
                Game1.playSound("shiny4");
            };

            // Capacity Gauge (x3 scale)
            this.capacityGauge = new Gauge(new Rectangle(this.aggroInventoryBox.BorderBounds.X - 36, this.aggroInventoryBox.Bounds.Y, 36, 135));

            // Build the aggregation of all storages
            this.aggro = new StorageAggregator(storages, this.categoryDropdown.dropDownOptions[this.categoryDropdown.selectedOption], null);

            // Create the aggregate menu
            this.RebuildAggregateMenu();

            // Calculate the overall bounds
            allBoxes
                .Select((box) => box.Bounds)
                .Append(this.scrollbar.bounds)
                .Aggregate((a, b) => Rectangle.Union(a, b))
                .Deconstruct(out this.xPositionOnScreen, out this.yPositionOnScreen, out this.width, out this.height);

            this.populateClickableComponentList();

            //snapToDefaultClickableComponent();
        }

        public override void draw(SpriteBatch batch)
        {
            // Draw bg
            this.drawBackground(batch);

            // Draw aggregate window
            this.aggroInventoryBox.Draw();

            // Draw the slots
            this.aggroMenu.draw(batch);

            // Draw the capacity gauge
            this.capacityGauge.Draw(batch);

            // Draw player inventory window
            this.playerInventoryBox.Draw();

            // Draw the player inventory
            this.playerInventoryMenu.draw(batch);

            // Draw transfering items
            foreach (TransferredItemSprite itemSprite in this._transferredItemSprites)
            {
                itemSprite.Draw(batch);
            }

            // Draw scroll bar
            this.scrollbar.Draw(batch);

            // Draw user search
            this.searchBox.Draw();
            this.searchTextbox.Draw(batch);
            if (string.IsNullOrEmpty(this.searchTextbox.Text))
            {
                batch.DrawString(Game1.smallFont, "Search...", new Vector2(this.searchTextbox.X + 24, this.searchTextbox.Y + 8), Color.Gray);
            }

            // Draw category dropdown
            this.dropDownBox.Draw();
            this.categoryDropdown.draw(batch, 0, 0);

            if (this.hoveredItem is not null)
            {
                drawToolTip(batch, this.hoveredItem.getDescription(), this.hoveredItem.DisplayName, this.hoveredItem, this.heldItem is not null);
            }
            else if (!string.IsNullOrEmpty(this.hoverText))
            {
                drawToolTip(batch, this.hoverText, this.hoverTitle, null, this.heldItem is not null);
            }

            // Draw the held item
            this.heldItem?.drawInMenu(batch, new Vector2((float)(Game1.getOldMouseX() + 8), (float)(Game1.getOldMouseY() + 8)), 1f);

            this.drawMouse(batch, true);
        }

        public override void drawBackground(SpriteBatch b)
        {
            if (Game1.options.showMenuBackground)
            {
                // Draw the menu background
                base.drawBackground(b);
            }
            else
            {
                bool isRaining = Game1.IsRainingHere();
                // isSnowing
                // isLightning

                Rectangle viewportBounds = Game1.graphics.GraphicsDevice.Viewport.Bounds;


                // Twilight
                // Day & Raining
                // Day | Night
                if (this.timeOfDay == TimeOfDay.Sunset)
                {
                    // Sunset BG
                    b.Draw(Game1.mouseCursors, viewportBounds, SunsetBgTextureCoords, Color.White * 0.5f);
                }
                else if (isRaining && this.timeOfDay == TimeOfDay.Day)
                {
                    // Rain BG
                    b.Draw(Game1.mouseCursors, viewportBounds, RainBgTextureCoords, Color.White * 0.1f);
                }
                else // Day Clear or night
                {
                    // Black BG
                    b.Draw(Game1.fadeToBlackRect, viewportBounds, Color.Black * (this.timeOfDay == TimeOfDay.Night ? 0.75f : 0.5f));
                }

                // Raining
                // !Day
                if (isRaining)
                {
                    // Rain overlay
                    b.Draw(Game1.staminaRect, viewportBounds, Color.Blue * 0.2f);
                }
                else if (this.timeOfDay >= TimeOfDay.Sunset)
                {
                    // Stars overlay
                    b.Draw(Game1.mouseCursors, new Rectangle(viewportBounds.X, viewportBounds.Y, viewportBounds.Width, viewportBounds.Height / 2), StarsTextureCoords, Color.White * 0.5f);
                }

            }
        }

        public override void performHoverAction(int x, int y)
        {
            this.hoveredItem = null;
            this.hoverText = "";
            this.hoverTitle = "";

            this.scrollbar.PerformHoverAction(x, y);

            if (this.capacityGauge.Bounds.Contains(x, y))
            {
                this.hoverTitle = "Storage Space";
                int freeSlots = this.aggro.TotalSlots - this.aggro.OccupiedSlots;
                double utilization = Math.Round((1f - this.capacityGauge.Value) * 1000f) / 10.0;
                this.hoverText = $"Capacity: {this.aggro.TotalSlots} Stacks\nEmpty Slots: {freeSlots}\nUtilization: {utilization}%";
                return;
            }

            if (this.searchBox.ContentBounds.Contains(x, y))
            {
                this.hoverTitle = "Search Selected Category";
                this.hoverText = "Searches item names and descriptions"
                    + "\nRight-click to clear"
                    + "\n-- Advanced Search Modes --"
                    + "\n> The # prefix searches item category. E.g: \"#forage\""
                    + "\n> The + prefix searches food buff names. E.g: \"+luck\""
                    + "\n> The =(equals) prefix and 1-3 searches quality. E.g: \"=2\"";
                return;
            }

            bool ddOpen = this.IsCategoryDropdownOpen();
            // Change the highlighed option in the dropdown
            if (ddOpen && this.categoryDropdown.dropDownBounds.Contains(x, y))
            {
                this.categoryDropdown.leftClickHeld(x, y);
                return;
            }

            if (this.aggroMenu.isWithinBounds(x, y))
            {
                // Over the aggregate inventory
                this.hoveredItem = this.aggroMenu.hover(x, y, this.heldItem);
                return;
            }

            if (this.playerInventoryMenu.isWithinBounds(x, y))
            {
                // Over the player inventory
                this.hoveredItem = this.playerInventoryMenu.hover(x, y, this.heldItem);
                return;
            }

            if (!ddOpen && this.dropDownBox.ContentBounds.Contains(x, y))
            {
                this.hoverTitle = "Category Selection";
                this.hoverText = "Select which grouping of chests you would like to interact with.";
                return;
            }

        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            this.searchTextbox.Update();

            if (!this.isWithinBounds(x, y))
            {
                // If they click over empty space with a held item, drop it
                if (this.heldItem is not null)
                {
                    this.DropHeldItem();
                }
                return;
            }

            if (this.IsCategoryDropdownOpen() && this.categoryDropdown.dropDownBounds.Contains(x, y))
            {
                // Select option
                this.categoryDropdown.leftClickHeld(x, y);

                return;
            }

            if (this.categoryDropdown.bounds.Contains(x, y))
            {
                // Open the dropdown
                this.categoryDropdown.receiveLeftClick(x, y);
                this.categoryDropdownHeldOpen = true;

                return;
            }

            if (this.scrollbar.ReceiveLeftClick(x, y))
            {
                return;
            }

            // If true, try to skip holding the item and directly transfer items between the inventories
            bool hotSwap = Game1.oldKBState.IsKeyDown(Keys.LeftShift) && !this.invertShiftTransfering;

            if (this.playerInventoryMenu.isWithinBounds(x, y))
            {
                this.heldItem = this.TransferItemFromPlayerInventory(x, y, this.heldItem, playSound, hotSwap);

                return;
            }

            if (this.aggroMenu.isWithinBounds(x, y))
            {
                this.heldItem = this.TransferItemFromFromAggregateInventory(x, y, this.heldItem, playSound, hotSwap);

                return;
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            if (this.scrollbar.LeftClickHeld(x, y))
            {
                return;
            }

            bool hotSwap = Game1.oldKBState.IsKeyDown(Keys.LeftShift) && !this.invertShiftTransfering;

            // When dragging leftclick over the player inventory and hotswaping, attempt to transfer the items under the cursor
            if (hotSwap && this.heldItem is null && this.playerInventoryMenu.isWithinBounds(x, y) && this.playerInventoryMenu.getItemAt(x, y) is not null)
            {
                this.heldItem = this.TransferItemFromPlayerInventory(x, y, this.heldItem, true, hotSwap);

                return;
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            if (!this.categoryDropdownHeldOpen && this.IsCategoryDropdownOpen())
            {
                // Is the mouse over the options?
                // Did the selected category change?
                if (this.categoryDropdown.dropDownBounds.Contains(x, y) && this.categoryDropdown.selectedOption != this.categoryDropdown.startingSelected)
                {
                    // Set the current selection as the starting (so that leftClickReleased resets to it) 
                    this.categoryDropdown.startingSelected = this.categoryDropdown.selectedOption;

                    // Get the category name
                    string category = this.categoryDropdown.dropDownOptions[this.categoryDropdown.selectedOption];

                    // Remember the farmers selection
                    categoryRecall = category;

                    // Only show storages from this category in the aggregator
                    this.aggro.ApplyStorageCategoryFilter(category);

                    // Update the inventory menu
                    this.RebuildAggregateMenu();
                }

                // Close the dropdown (fake coords to prevent dirtying Game options)
                this.categoryDropdown.leftClickReleased(-1, -1);

            }

            this.categoryDropdownHeldOpen = false;

            this.scrollbar.ReleaseLeftClick();
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (!this.isWithinBounds(x, y))
            {
                return;
            }

            if (this.playerInventoryMenu.isWithinBounds(x, y))
            {
                this.heldItem = this.playerInventoryMenu.rightClick(x, y, this.heldItem, playSound);
                return;
            }

            if (this.aggroMenu.isWithinBounds(x, y))
            {
                this.heldItem = this.aggroMenu.rightClick(x, y, this.heldItem, playSound);
                return;
            }

            // Right clicking the search should clear and focus it
            if (this.searchBox.Bounds.Contains(x, y))
            {
                this.searchTextbox.Text = string.Empty;
                this.searchTextbox.SelectMe();
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            this.scrollbar.Value -= Math.Sign(direction);
        }

        public override void receiveKeyPress(Keys key)
        {
            // Skip key processing if the user is typing in the textbox
            // Except for escape
            if (this.searchTextbox.Selected && key != Keys.Escape)
            {
                return;
            }

            if (Game1.options.doesInputListContain(Game1.options.menuButton, key) && this.readyToClose())
            {
                this.exitThisMenu(true);
            }
        }

        public override void applyMovementKey(int direction)
        {
            base.applyMovementKey(direction);
        }

        public override void update(GameTime time)
        {
            base.update(time);

            // Search
            this.aggro.ApplyTextSearch(this.searchTextbox.Text);

            // Determine the time of day
            this.timeOfDay = Game1.isStartingToGetDarkOut()
            ? TimeOfDay.Sunset
            : Game1.isDarkOut()
            ? TimeOfDay.Night
            : TimeOfDay.Day;

            // Ensure all the chests are valid
            this.aggro.ValidateAndUpdate(time);
            // Calculate how much free space the aggro has
            this.capacityGauge.Value = 1.0f - Math.Clamp((this.aggro.TotalSlots > 0 ? (this.aggro.OccupiedSlots / (float)this.aggro.TotalSlots) : 0f), 0f, 1f);

            // Update scrollbar
            int numberOfItemRows = (int)Math.Ceiling((this.aggro.LastSlotIndexWithItem() + 1) / (float)this.aggroMenuColumnCount);
            int numberOfOverflowRows = Math.Max(0, numberOfItemRows - this.aggroMenuRowCount);
            this.scrollbar.Steps = numberOfOverflowRows;
        }

        private void RebuildAggregateMenu()
        {
            // Reset scroll
            this.aggro.SlotShift = 0;

            // Build the menu
            this.aggroMenu = new InventoryMenu(
                // X, Centered on the page
                this.aggroInventoryBox.ContentBounds.X,
                // Y
                this.aggroInventoryBox.ContentBounds.Y,
                // Not the default player inventory menu
                false,
                // Inventory source is the aggregator
                this.aggro,
                // No special highlighting
                null,
                // Capacity of the inventory
                this.aggroMenuRowCount * this.aggroMenuColumnCount,
                // Number of rows drawn (if it is evenly divisble by capacity!)
                this.aggroMenuRowCount
                );
        }

        /// <summary>
        /// If swapItem is null and hotSwap is false, will attempt to extract the item under the cursor.
        /// If swapItem is null and hotSwap is true, will attempt to move item to aggregate inventory.
        /// If swapItem is not null, hotSwap is ignored, will do the default behavior of inserting/swapping the item at the target slot.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="swapItem"></param>
        /// <param name="playSound"></param>
        /// <param name="hotSwap"></param>
        /// <returns></returns>
        private Item TransferItemFromPlayerInventory(int x, int y, Item swapItem, bool playSound, bool hotSwap)
        {
            bool playPickupSound = playSound && (swapItem is not null || !hotSwap);

            // Pass to player inventory
            Item extractedItem = this.playerInventoryMenu.leftClick(x, y, swapItem, playPickupSound);

            // Transfering?
            if (!hotSwap || swapItem is not null || extractedItem is null)
            {
                // No
                return extractedItem;
            }

            // Attempt to transfer to the aggregate
            int prevStackSize = extractedItem.Stack;
            Item remainingItem = this.aggro.Upsert(extractedItem, -1);
            if (remainingItem is null || prevStackSize != remainingItem.Stack)
            {
                if (playSound)
                {
                    Game1.playSound("dwop");
                }

                // Add to the transfered sprites to play animation
                int sourceSlotIdx = this.playerInventoryMenu.getInventoryPositionOfClick(x, y);
                Rectangle sourceSlotBounds = (sourceSlotIdx >= 0 && sourceSlotIdx < this.playerInventoryMenu.inventory.Count)
                    ? this.playerInventoryMenu.inventory[sourceSlotIdx].bounds
                    : new Rectangle(x, y, 0, 0);
                this._transferredItemSprites.Add(new TransferredItemSprite(extractedItem.getOne(), sourceSlotBounds.X, sourceSlotBounds.Y));

                this.aggro.SearchAndSort();
            }

            //var q = extractedItem.getOne();
            //q.Stack = 100;
            //return q;
            return remainingItem;

        }

        /// <summary>
        /// If itemToAdd is null and hotSwap is false, will attempt to extract the item under the cursor.
        /// If itemToAdd is null and hotSwap is true, will attempt to move item to player inventory.
        /// If itemToAdd is not null, hotSwap is ignored, will attempt to insert itemToAdd.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="itemToAdd"></param>
        /// <param name="playSound"></param>
        /// <param name="hotSwap"></param>
        /// <returns></returns>
        private Item TransferItemFromFromAggregateInventory(int x, int y, Item itemToAdd, bool playSound, bool hotSwap)
        {
            Item extractedItem;

            if (itemToAdd is null)
            {
                bool playPickupSound = playSound && !hotSwap;

                // Attempt to extract the item
                extractedItem = this.aggroMenu.leftClick(x, y, null, playPickupSound);
                if (extractedItem is not null)
                {
                    this.aggro.SearchAndSort();

                    if (hotSwap)
                    {
                        extractedItem = this.playerInventoryMenu.tryToAddItem(extractedItem, playSound ? "coin" : "");
                    }
                }
            }
            else
            {
                // If they are holding an item, attempt to insert it
                int startingStackSize = itemToAdd.Stack;
                int preferedSlot = this.aggroMenu.getInventoryPositionOfClick(x, y);
                extractedItem = this.aggro.Upsert(itemToAdd, preferedSlot);

                // Sort if something changed
                if (extractedItem is null || itemToAdd.Stack != startingStackSize)
                {
                    if (playSound)
                    {
                        Game1.playSound("stoneStep");
                    }
                    this.aggro.SearchAndSort();
                }

            }

            return extractedItem;
        }

        private bool IsCategoryDropdownOpen()
        {
            if (optionsDropDownField_Clicked is null || this.categoryDropdown is null)
            {
                return false;
            }
            // Reflect on the dropdown to determine if it is open
            return (bool)optionsDropDownField_Clicked.GetValue(this.categoryDropdown);
        }

    }
}
