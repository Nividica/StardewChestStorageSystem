using StardewModdingAPI;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using StardewObject = StardewValley.Object;

namespace ChestStorageSystem.Storage
{
    public class StorageOrigin
    {
        // ToDo: Move to integrations
        private static readonly string ChestsAnywhereCategoryKey = "Pathoschild.ChestsAnywhere/Category";

        /// <summary>
        /// All top-level game locations, and child locations
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<GameLocation> AllLocations()
        {
            foreach (GameLocation location in Game1.locations)
            {
                if (location is null)
                {
                    continue;
                }

                yield return location;

                if (location is BuildableGameLocation buildLocation)
                {
                    foreach (Building building in buildLocation.buildings)
                    {
                        if (building is null || building.indoors.Value is null)
                        {
                            continue;
                        }

                        yield return building.indoors.Value;
                    }
                }
            }
        }

        /// <summary>
        /// All player chests in the given location
        /// </summary>
        /// <param name="location"></param>
        /// <returns>The chest and a validation function</returns>
        public static IEnumerable<(Chest, Func<bool>)> ChestsInLocation(GameLocation location)
        {
            foreach (StardewObject obj in location.objects.Values)
            {
                if (obj is Chest chest && chest.playerChest.Value && chest.CanBeGrabbed && (chest.SpecialChestType == Chest.SpecialChestTypes.None || chest.SpecialChestType == Chest.SpecialChestTypes.AutoLoader))
                {
                    yield return (
                        chest,
                        () => location.objects.ContainsKey(chest.TileLocation) && location.objects[chest.TileLocation] == chest
                    );
                }

                if (obj.heldObject.Value is Chest heldChest && heldChest.SpecialChestType == Chest.SpecialChestTypes.None)
                {
                    yield return (
                        heldChest,
                        () => obj.heldObject.Value == heldChest && location.objects.ContainsKey(obj.TileLocation) && location.objects[obj.TileLocation] == obj
                    );
                }
            }

            // Check for fridge
            if (location is FarmHouse house)
            {
                // Have they unlocked the fridge?
                if (house.fridgePosition != default && house.fridge.Value is Chest fridge)
                {
                    // I don't think this validation could ever fail, not while the UI is open.
                    // But I am adding it just incase.
                    yield return (
                        fridge,
                        () => house.fridge.Value == fridge
                    );
                }
            }
            else if (location is IslandFarmHouse gingerHouse)
            {
                // Have they unlocked the Ginger Island house?
                if (gingerHouse.visited.Value && gingerHouse.fridge.Value is Chest fridge)
                {
                    yield return (
                       fridge,
                       () => gingerHouse.fridge.Value == fridge
                   );
                }

            }
        }

        /// <summary>
        /// Builds a list of all chest storages in all locations
        /// </summary>
        /// <returns></returns>
        public static List<StorageOrigin> BuildStorageList()
        {
            List<StorageOrigin> storages = new();

            foreach (GameLocation location in AllLocations())
            {
                foreach (var (chest, isValid) in ChestsInLocation(location))
                {
                    // All chests _should_ start as valid.
                    if (!isValid())
                    {
                        CSS.Log($"Warning: Invalid chest found, skipping. Location: {location.Name}, Position:({chest.TileLocation.X},{chest.TileLocation.Y}), Object Name:{chest.DisplayName}", LogLevel.Warn);
                        continue;
                    }

                    // Try to get the ChestsAnywhere category, fallback on location name
                    if (!chest.modData.TryGetValue(ChestsAnywhereCategoryKey, out string category) || string.IsNullOrEmpty(category))
                    {
                        category = location.Name;
                        if (location is Cabin cabin)
                        {
                            category += $" ({cabin.owner.displayName})";
                        }
                    }

                    storages.Add(new StorageOrigin() { Chest = chest, Category = category, IsValid = isValid });
                }
            }

            return storages;
        }

        /// <summary>
        /// The actual storage object
        /// </summary>
        public Chest Chest;

        /// <summary>
        /// What group the storage belongs to. Defaults to location name, but can be set by this mod(ToDo), or by the ChestsAnywhere mod.
        /// </summary>
        public string Category;

        /// <summary>
        /// Should return false when this storage container is no longer valid, such as when it has been destroyed
        /// </summary>
        public Func<bool> IsValid;
    }
}
