using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewObject = StardewValley.Object;

namespace ChestStorageSystem.Storage
{
    public class StorageAggregator : IList<Item>
    {
        /// <summary>
        /// A *chest* will be targeted if it contains an item matching these rules
        /// </summary>
        [Flags]
        public enum CanStackRules
        {
            #pragma warning disable format
            ItemOnly                = 0,
            Default                 = ~0,
            MaxStackGreaterThanOne  = 0b001,
            MatchingQuality         = 0b010,
            MatchingColor           = 0b100,
            IgnoreStackSize         = Default & ~MaxStackGreaterThanOne,
            IgnoreQuality           = Default & ~MatchingQuality,
            IgnoreColor             = Default & ~MatchingColor,
            #pragma warning restore format
        }

        /// <summary>
        /// We never want to store references to the actual Items, as they could be moved or removed from the source inventory,
        /// but we would still be referencing them as-if they are still where we first saw it.
        /// 
        /// All operations should always reference the external inventory to see what is stored in the indexed slot.
        /// </summary>
        private class MappedSlot : IComparable<MappedSlot>
        {
            public Chest Chest { get; private set; }
            public int SlotIndex { get; private set; }
            public int StorageIndex { get; private set; }

            public MappedSlot(Chest chest, int invIndex, int slotIndex)
            {
                this.SlotIndex = slotIndex;
                this.Chest = chest;
                this.StorageIndex = invIndex;
            }

            public Item GetItem()
            {
                if (this.SlotIndex >= this.Chest.items.Count)
                {
                    return null;
                }

                return this.Chest.items[this.SlotIndex];
            }

            public void SetItem(Item item)
            {
                // If the inventory has shrunk (e.g. via clearNulls()), add nulls up to this index
                while (this.SlotIndex >= this.Chest.items.Count)
                {
                    this.Chest.items.Add(null);
                }

                this.Chest.items[this.SlotIndex] = item;

                ItemGrabMenu.organizeItemsInList(this.Chest.items);
            }

            public int CompareTo(MappedSlot other)
            {
                Item myItem = this.GetItem();
                Item otherItem = other.GetItem();
                int itemOrder;
                // Slots should be ordered first by their item order, then by their inventory index, then by their slot index
                // Empty slots should go after non-empty slots
                if (myItem is null && otherItem is null)
                {
                    // Both are empty
                    itemOrder = 0;
                }
                else if (myItem is null)
                {
                    // This empty slot should be after the other non-empty slot
                    itemOrder = 1;
                }
                else if (otherItem is null)
                {
                    // The other empty slot should be after this non-empty slot
                    itemOrder = -1;
                }
                else
                {
                    // Both have an item
                    // Use the games item sorting
                    itemOrder = myItem.CompareTo(otherItem);
                }

                // Not the same?
                if (itemOrder != 0)
                {
                    return itemOrder;
                }

                // Same item order, sort by storage index
                int invOrder = this.StorageIndex - other.StorageIndex;
                if (invOrder != 0)
                {
                    // Slots are in different inventories, sort by inventory index
                    return invOrder;
                }

                // Both slots are from the same inventory, sort by slot index
                return this.SlotIndex - other.SlotIndex;
            }
        }

        private static Item AddItemToChest(Chest chest, Item itemToAdd)
        {
            if (itemToAdd is null)
            {
                return null;
            }
            // Attempt to add to this inventory
            int prevStackSize = itemToAdd.Stack;
            itemToAdd = chest.addItem(itemToAdd);

            // Anything change?
            if (itemToAdd is null || prevStackSize != itemToAdd.Stack)
            {
                // Sort the inventory
                ItemGrabMenu.organizeItemsInList(chest.items);

                // If fully consumed, done, return null
                if (itemToAdd is null)
                {
                    return null;
                }
            }

            return itemToAdd;
        }

        /// <summary>
        /// Based on Item.canStackWith(other)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private static bool CanStackWith(Item source, Item target, CanStackRules rules)
        {
            if (source is not StardewObject sourceObj || target is not StardewObject targetObj)
            {
                return false;
            }

            // Check sheet index
            if (sourceObj.ParentSheetIndex != targetObj.ParentSheetIndex)
            {
                return false;
            }

            // Check is-bigCraftable
            if (sourceObj.bigCraftable.Value != targetObj.bigCraftable.Value)
            {
                return false;
            }

            // Check name
            if (!source.Name.Equals(target.Name))
            {
                return false;
            }

            // Check order data
            if (targetObj.orderData.Value != sourceObj.orderData.Value)
            {
                return false;
            }

            // Check quality
            if (rules.HasFlag(CanStackRules.MatchingQuality) && (sourceObj.Quality != targetObj.Quality))
            {
                return false;
            }

            // Check size
            if (rules.HasFlag(CanStackRules.MaxStackGreaterThanOne) && (source.maximumStackSize() > 1 && target.maximumStackSize() > 1))
            {
                return false;
            }

            // Check color
            if (rules.HasFlag(CanStackRules.MatchingColor) && ((source is ColoredObject sourceColor && target is ColoredObject targetColor) && !sourceColor.color.Value.Equals(targetColor.color.Value)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// When accessing the aggregate via `this[index]` the `index` will be shifted by this amount.
        /// Used to simulate scrolling.
        /// 
        /// Reading a value from a slot shifted out-of-bounds will return null.
        /// Writing a value to a slot shifted out-of-bounds will thrown an exception.
        /// </summary>
        public int SlotShift = 0;

        public int TotalSlots { get; private set; } = 0;
        public int OccupiedSlots { get; private set; } = 0;

        private readonly List<StorageOrigin> storages;
        private readonly List<StorageOrigin> filteredStorages = new();
        private readonly List<MappedSlot> externalSlots = new();
        private List<MappedSlot> filteredSlots;
        private string storageCategory;
        private string itemSearchTerm;
        private double nextUtilizationTick = 0;

        public StorageAggregator(List<StorageOrigin> storages, string storageCategory = null, string searchTerm = null)
        {
            this.storages = storages;
            this.storageCategory = storageCategory;
            this.itemSearchTerm = searchTerm;
            this.BuildSlots();
        }

        /// <summary>
        /// This is the only supported way of accessing this aggregate
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Item this[int index]
        {
            get
            {
                int sftIdx = index + this.SlotShift;
                if (sftIdx < 0 || sftIdx >= this.filteredSlots.Count)
                {
                    return null;
                }
                return this.filteredSlots[sftIdx].GetItem();
            }
            set
            {
                int sftIdx = index + this.SlotShift;
                if (sftIdx < 0 || sftIdx >= this.filteredSlots.Count)
                {
                    throw new NotSupportedException("Slot Out Of Bounds For Set");
                }
                this.filteredSlots[sftIdx].SetItem(value);
            }
        }

        public int Count => this.filteredSlots.Count;

        public bool IsReadOnly => false;

        #region Unimplemented IList members
        public void Add(Item item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(Item item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Item[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<Item> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(Item item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, Item item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(Item item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        #endregion

        public void SearchAndSort()
        {
            // Search
            IEnumerable<MappedSlot> searchResults = ItemSearch.Search(this.externalSlots, this.itemSearchTerm, (slot) => slot.GetItem());
            this.filteredSlots = (searchResults == this.externalSlots) ? this.externalSlots : searchResults.ToList();

            // Then sort
            this.filteredSlots.Sort();
        }

        public void ApplyStorageCategoryFilter(string category)
        {
            if (category == this.storageCategory)
            {
                return;
            }

            this.storageCategory = category;
            this.BuildSlots();
        }

        public void ApplyTextSearch(string text)
        {
            if (text == this.itemSearchTerm)
            {
                return;
            }

            this.itemSearchTerm = text;
            this.SearchAndSort();
        }

        /// <summary>
        /// If the item can stack, search for existing stacks to merge into. If none can be found insert it.
        /// If the item can't stack, search for a slot to insert into.
        ///
        /// If a prefered slot is provided, and that slot contains the same item as the incoming item, the
        /// incoming item will be first added to the chest from which that slot comes from.
        ///
        /// </summary>
        /// <param name="incomingItem"></param>
        /// <param name="preferedSlotIdx">Slot shifting is applied</param>
        /// <returns></returns>
        public Item Upsert(Item incomingItem, int preferedSlotIdx)
        {
            if (incomingItem is null)
            {
                return null;
            }

            // Try to merge with the prefered slot
            if (preferedSlotIdx >= 0 && (preferedSlotIdx + this.SlotShift) < this.Count)
            {
                MappedSlot preferedSlot = this.filteredSlots[preferedSlotIdx + this.SlotShift];
                if (CanStackWith(preferedSlot.GetItem(), incomingItem, CanStackRules.IgnoreStackSize))
                {
                    incomingItem = AddItemToChest(this.storages[preferedSlot.StorageIndex].Chest, incomingItem);

                    // If fully consumed, done, return null
                    if (incomingItem is null)
                    {
                        return null;
                    }
                }
            }

            // Try to merge with existing items
            incomingItem = this.StackWithExisting(incomingItem, CanStackRules.ItemOnly);
            if (incomingItem is null)
            {
                // If fully consumed, done, return null
                return null;
            }

            // Attempt to add to any inventory
            foreach (StorageOrigin storage in this.filteredStorages)
            {
                incomingItem = AddItemToChest(storage.Chest, incomingItem);

                // If fully consumed, done, return null
                if (incomingItem is null)
                {
                    return null;
                }
            }

            // Not fully consumed, return any remaining
            return incomingItem;
        }

        public int LastSlotIndexWithItem()
        {
            for (int idx = this.filteredSlots.Count - 1; idx >= 0; idx--)
            {
                if (this.filteredSlots[idx].GetItem() is not null)
                {
                    return idx;
                }
            }

            return -1;
        }

        public Item StackWithExisting(Item incomingItem, CanStackRules rules)
        {
            // Search each inventory for a like-item
            foreach (StorageOrigin storage in this.filteredStorages)
            {
                foreach (Item item in storage.Chest.items)
                {
                    if (CanStackWith(incomingItem, item, rules))
                    {
                        // If one is found, target that inventory
                        incomingItem = AddItemToChest(storage.Chest, incomingItem);

                        // If fully consumed, done, return null
                        if (incomingItem is null)
                        {
                            return null;
                        }

                        // Stop searching this inventory
                        break;
                    }
                }
            }

            // Not fully consumed, return any remaining
            return incomingItem;
        }

        public void ValidateAndUpdate(GameTime time)
        {
            bool needsRebuild = false;
            this.storages.RemoveAll((storage) =>
            {
                if (storage.IsValid())
                {
                    return false;
                }

                // Rebuild if the filtered storages contain this storage
                needsRebuild |= this.filteredStorages.Contains(storage);

                return true;
            });

            if (needsRebuild)
            {
                this.BuildSlots();
            }

            this.nextUtilizationTick -= time.ElapsedGameTime.TotalSeconds;
            if (this.nextUtilizationTick < 0)
            {
                // Check every X seconds
                this.nextUtilizationTick = 0.60;
                this.TotalSlots = this.externalSlots.Count;
                this.OccupiedSlots = this.externalSlots.Aggregate(0, (count, slot) => count + (slot.GetItem() is null ? 0 : 1));
            }
        }

        /// <summary>
        /// Builds the external slots for all the external storages
        /// </summary>
        /// <param name="category"></param>
        private void BuildSlots()
        {
            this.externalSlots.Clear();
            this.filteredStorages.Clear();

            for (int invIdx = 0; invIdx < this.storages.Count; ++invIdx)
            {
                StorageOrigin storage = this.storages[invIdx];

                // Is there a category to filter by?
                if (this.storageCategory is not null && this.storageCategory != storage.Category)
                {
                    continue;
                }

                // Add to the filtered list
                this.filteredStorages.Add(storage);

                // Get the capacity of the chest(including overflow)
                int capacity = Math.Max(storage.Chest.GetActualCapacity(), storage.Chest.items.Count);

                // Add each slot from the chest
                for (int i = 0; i < capacity; ++i)
                {
                    this.externalSlots.Add(new MappedSlot(storage.Chest, invIdx, i));
                }
            }

            this.SearchAndSort();
        }

    }
}
