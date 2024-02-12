using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public partial class CacheService(IMemoryCache cache)
    {
        private readonly Dictionary<string, HashSet<string>> VariationIndex = [];
        private readonly Dictionary<string, List<SearchItem>> AudioFuzzyIndex = [];
        private const int VARIATION_LOOKUP_CACHE_LENGTH = 5;

        public void CacheSearchItem(SearchItem item)
        {
            var prefix = item.MediaType;
            cache.Set($"{prefix}_extid_{item.ExternalId}", item);
            if (item.MediaType == "audio")
            {
                CacheAudioSearchItem(item);
                return;
            }

            var normalizedTitle = item.Title.RemoveAccentButKeepGermanUmlauts().ToLower();

            cache.Set($"{prefix}_title_{normalizedTitle}", item);

            foreach (var variation in item.TitleMatchVariations)
            {
                var normalizedVariation = variation.RemoveAccentButKeepGermanUmlauts().ToLower();
                var cacheKey = $"{prefix}_var_{normalizedVariation}";
                cache.Set(cacheKey, item);

                // Indexing by prefix
                var indexPrefix = normalizedVariation[..Math.Min(VARIATION_LOOKUP_CACHE_LENGTH, variation.Length)].ToLower();
                if (!VariationIndex.ContainsKey(indexPrefix))
                {
                    VariationIndex[indexPrefix] = [];
                }
                VariationIndex[indexPrefix].Add(cacheKey);
            }
        }

        private void CacheAudioSearchItem(SearchItem item)
        {
            // Normalize and simplify the title and author for fuzzy matching
            var key = NormalizeForFuzzyMatching(item.ExternalId);

            if (!AudioFuzzyIndex.ContainsKey(key))
            {
                AudioFuzzyIndex[key] = new List<SearchItem>();
            }
            AudioFuzzyIndex[key].Add(item);
        }

        private string NormalizeForFuzzyMatching(string input)
        {
            // Normalize the input string by removing accents, converting to lower case, and removing non-alphanumeric characters
            var normalized = input.RemoveAccentButKeepGermanUmlauts().RemoveSpecialCharacters().ToLower();
            normalized = WhiteSpaceRegex().Replace(normalized, "");
            return normalized;
        }

        public SearchItem? SearchItemByTitle(string mediaType, string title)
        {
            var normalizedTitle = title.RemoveAccentButKeepGermanUmlauts().ToLower();

            // Use the first few characters of the normalized title for cache prefix search
            var cacheSearchPrefix = normalizedTitle[..Math.Min(VARIATION_LOOKUP_CACHE_LENGTH, normalizedTitle.Length)];

            if (VariationIndex.TryGetValue(cacheSearchPrefix, out var cacheKeys))
            {
                foreach (var cacheKey in cacheKeys)
                {
                    if (cache.TryGetValue(cacheKey, out SearchItem? item))
                    {
                        if (item?.MediaType != mediaType)
                        {
                            continue;
                        }
                        // After finding a potential item, compare normalizedTitle with each German title variation
                        foreach (var variation in item?.TitleSearchVariations ?? [])
                        {
                            var normalizedVariation = variation.RemoveAccentButKeepGermanUmlauts().ToLower();
                            if (normalizedTitle.StartsWith(variation, StringComparison.OrdinalIgnoreCase))
                            {
                                return item;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public SearchItem? GetSearchItemByExternalId(string mediaType, string externalId)
        {
            if (cache.TryGetValue($"{mediaType}_extid_{externalId}", out SearchItem? item))
            {
                return item;
            }
            return null;
        }

        public SearchItem? GetSearchItemByTitle(string mediaType, string title)
        {
            var normalizedTitle = title.RemoveAccentButKeepGermanUmlauts().ToLower();

            if (mediaType == "generic")
            {
                // TODO
            }
            cache.TryGetValue($"{mediaType}_var_{normalizedTitle}", out SearchItem? item);
            if (item == null)
            {
                cache.TryGetValue($"{mediaType}_title_{normalizedTitle}", out item);
            }
            return item;
        }

        [GeneratedRegex("\\s")]
        private static partial Regex WhiteSpaceRegex();
    }
}
