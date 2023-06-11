using StardewModdingAPI;
using StardewValley;
using System;
using StardewObject = StardewValley.Object;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Netcode;


namespace ChestStorageSystem.Storage
{
    public class ItemSearch
    {
        public enum Mode
        {
            None = 0,
            NameDesc = 1,
            ItemCategory = 2,
            FoodBuff = 3,
        }

        private const int StardewMagicNumber_Inedible = -300;
        private const int ItemInfoBuffsField = 7;
        private const string AssetStringItemHoverBuff = "Strings\\UI:ItemHover_Buff";

        private static readonly Dictionary<int, string[]> FoodBuffCache = new();

        /// <summary>
        /// Returns the source if no search should be performed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="query"></param>
        /// <param name="getItem"></param>
        /// <returns></returns>
        public static IEnumerable<T> Search<T>(IEnumerable<T> source, string query, Func<T, Item> getItem)
        {
            var (term, mode) = ParseSearchQuery(query);
            if (mode == Mode.None)
            {
                return source;
            }

            return source.Where((element) => DoesItemMatch(getItem(element), term, mode));
        }

        /// <summary>
        /// Parses the query for the mode and term.
        ///
        /// If the query is prefixed, like "#", the term is returned without the prefix.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static (string, Mode) ParseSearchQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return (query, Mode.None);
            }

            // Determine the search mode
            Mode mode;
            if (query.StartsWith("#"))
            {
                mode = Mode.ItemCategory;
            }
            else if (query.StartsWith("+"))
            {
                mode = Mode.FoodBuff;
            }
            else
            {
                mode = Mode.NameDesc;
            }

            // Prefixed?
            string term;
            if (mode > Mode.NameDesc)
            {
                term = query[1..];
            }
            else
            {
                term = query;
            }

            return (term, mode);
        }

        /// <summary>
        /// Returns true if there is no search term, or the mode is null.
        /// Returns false if there is a search term, but no item.
        /// Otherswise returns true if the item matches the term+mode
        /// </summary>
        /// <param name="item"></param>
        /// <param name="term"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static bool DoesItemMatch(Item item, string term, Mode mode)
        {
            if (mode == Mode.None || string.IsNullOrEmpty(term))
            {
                // Not searching, all items match
                return true;
            }
            else if (item is null)
            {
                // No item to check
                return false;
            }

            return mode switch
            {
                // Search in the item and description
                Mode.NameDesc => MatchText(item, term),
                // Search in the item category, with support for no-category items
                Mode.ItemCategory => MatchCategory(item, term),
                // Search the items food buffs
                Mode.FoodBuff => MatchFoodBuff(item, term),
                // Unsupported mode
                _ => false,
            };
        }

        /// <summary>
        /// Returns true if the term appears anywhere in the Name or Description of the item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        private static bool MatchText(Item item, string term)
        {
            if (item.DisplayName.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (item.getDescription() is string desc && desc.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the term appears anywhere in the Category of the item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        private static bool MatchCategory(Item item, string term)
        {
            string itemCategory = item.getCategoryName();
            bool noCategory = string.IsNullOrEmpty(itemCategory);
            if (term == string.Empty)
            {
                return noCategory;
            }
            else
            {
                return !noCategory && itemCategory.Contains(term, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Returns true if item has food buffs, and the term appears anywhere in any of those buffs
        /// </summary>
        /// <param name="item"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        private static bool MatchFoodBuff(Item item, string term)
        {
            // Some items have 7+ info fields, but only those with an edibility != -300 is the 7th field food buffs.
            if (term.Length == 0 || item is not StardewObject itemObject || itemObject.Edibility == StardewMagicNumber_Inedible)
            {
                return false;
            };

            // Is there any info for this item?
            string[] buffPowers = GetBaselineFoodBuffPowers(itemObject.ParentSheetIndex);
            if (buffPowers is null)
            {
                return false;
            }

            // Get any item-specific buffs
            buffPowers = itemObject.ModifyItemBuffs(buffPowers);

            // Search every buff power > 0
            for (int buffId = 0; buffId < buffPowers.Length; buffId++)
            {
                string power = buffPowers[buffId];
                if (power == "0") continue;

                string buffDescription = Game1.content.LoadString($"{AssetStringItemHoverBuff}{buffId}");
                if (string.IsNullOrEmpty(buffDescription))
                {
                    continue;
                }
                if (buffDescription.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the array of buff powers for the item
        /// </summary>
        /// <param name="parentSheetIndex"></param>
        /// <returns></returns>
        private static string[] GetBaselineFoodBuffPowers(int parentSheetIndex)
        {
            string[] buffPowers = null;

            // Are the buffs not yet cached?
            if (FoodBuffCache.ContainsKey(parentSheetIndex))
            {
                //CSS.Log($"Cache Hit");
                buffPowers = FoodBuffCache[parentSheetIndex];
            }
            else
            {
                // Ask for any information about the item
                Game1.objectInformation.TryGetValue(parentSheetIndex, out string itemInfo);
                if (!string.IsNullOrEmpty(itemInfo))
                {
                    // Does the item have baseline buff powers?
                    string[] infoFields = itemInfo.Split('/');
                    if (infoFields.Length > ItemInfoBuffsField)
                    {
                        // Powers are provided as a space delimited string of ints
                        buffPowers = infoFields[ItemInfoBuffsField].Split(' ');
                    }
                }

                // Store the buffs(or null) in the cache
                FoodBuffCache.Add(parentSheetIndex, buffPowers);
            }

            // Return a new array in-case the item mutates it during ModifyItemBuffs()
            return buffPowers is not null ? (string[])buffPowers.Clone() : null;
        }

    }
}
