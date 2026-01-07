using StardewModdingAPI;
using StardewValley;
using System;
using StardewObject = StardewValley.Object;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Netcode;
using static ChestStorageSystem.Storage.ItemSearch;
using StardewValley.GameData.Objects;

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
            ItemQuality = 4,
        }

        private const int StardewMagicNumber_Inedible = -300;
        private const string AssetStringItemHoverBuff = "Strings\\UI:ItemHover_Buff";

        /// <summary>
        /// Returns the source if no search should be performed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="queryString"></param>
        /// <param name="getItem"></param>
        /// <returns></returns>
        public static IEnumerable<T> Search<T>(IEnumerable<T> source, string queryString, Func<T, Item> getItem)
        {
            // Parse the query for terms and modes
            var predicates = ParseSearchQuery(queryString);
            if (predicates.Count == 0)
            {
                return source;
            }

            // Apply the search predicates to each item
            // All must match
            return source.Where((element) =>
            {
                if (getItem(element) is not Item item)
                {
                    return false;
                }

                return predicates.All(
                    (tm) => DoesItemMatch(item, tm.term, tm.mode)
                );
            });
        }

        /// <summary>
        /// Parses the query string for the modes and terms.
        ///
        /// If the query is prefixed, like "#", the term is returned without the prefix.
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public static List<(string term, Mode mode)> ParseSearchQuery(string queryString)
        {
            List<(string, Mode)> predicates = new();
            if (string.IsNullOrEmpty(queryString))
            {
                return predicates;
            }

            // Split the query
            string[] queries = queryString.Split(' ');

            foreach (string query in queries)
            {
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
                else if (query.StartsWith("="))
                {
                    mode = Mode.ItemQuality;
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

                predicates.Add((term, mode));
            }

            return predicates;
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
            if (mode == Mode.None || term is null)
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
                // Compare item quality
                Mode.ItemQuality => MatchQuality(item, term),
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

            try
            {

                IEnumerable<Buff> buffs = item.GetFoodOrDrinkBuffs();

                return buffs.Any((buff) =>
                {
                    if (!buff.HasAnyEffects())
                    {
                        return false;
                    }

                    string[] buffPowers = buff.effects.ToLegacyAttributeFormat();

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
                });
            }
            catch
            {
                return false;
            }


        }

        private static bool MatchQuality(Item item, string term)
        {
            // Only objects have quality
            if (item is not StardewObject itemObject)
            {
                return false;
            }

            // Default to no quality
            if (!int.TryParse(term, out int nTerm))
            {
                nTerm = 0;
            }
            else if (nTerm > 2)
            {
                // I don't know why, but 3 was skipped
                // 0 = No stars, 1 = Silver, 2 = Gold, 4 = Iridium
                nTerm++;
            }

            return itemObject.Quality == nTerm;
        }

    }
}
