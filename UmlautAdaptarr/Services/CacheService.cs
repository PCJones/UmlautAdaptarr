using Microsoft.Extensions.Caching.Memory;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public class CacheService(IMemoryCache cache)
    {
        private readonly Dictionary<string, HashSet<string>> VariationIndex = [];
        private const int VARIATION_LOOKUP_CACHE_LENGTH = 5;

        public void CacheSearchItem(SearchItem item)
        {
            var prefix = item.MediaType;
            var normalizedTitle = item.Title.RemoveAccentButKeepGermanUmlauts().ToLower();
            // TODO maybe we need to also add the media type (movie/book/show etc)

            cache.Set($"{prefix}_extid_{item.ExternalId}", item);
            cache.Set($"{prefix}_title_{normalizedTitle}", item);

            foreach (var variation in item.TitleSearchVariations)
            {
                var normalizedVariation = item.Title.RemoveAccentButKeepGermanUmlauts().ToLower();
                var cacheKey = $"{prefix}_var_{normalizedVariation}";
                cache.Set(cacheKey, item);

                // Indexing by prefix
                var indexPrefix = normalizedVariation[..Math.Min(VARIATION_LOOKUP_CACHE_LENGTH, variation.Length)].ToLower();
                if (!VariationIndex.ContainsKey(indexPrefix))
                {
                    VariationIndex[indexPrefix] = new HashSet<string>();
                }
                VariationIndex[indexPrefix].Add(cacheKey);
            }
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
    }
}
