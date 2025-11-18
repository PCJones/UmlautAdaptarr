using Microsoft.Extensions.Caching.Memory;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
	public partial class CacheService(IMemoryCache cache)
    {
        private readonly Dictionary<string, HashSet<string>> VariationIndex = [];
        private readonly Dictionary<string, List<(HashSet<string> TitleVariations, string CacheKey)>> BookVariationIndex = [];
        private readonly Dictionary<string, List<(HashSet<string> TitleVariations, string CacheKey)>> AudioVariationIndex = [];
        private const int VARIATION_LOOKUP_CACHE_LENGTH = 5;
		private const string TitleRenamePrefix = "title_rename_";
		private static readonly TimeSpan TitleRenameCacheDuration = TimeSpan.FromHours(12);

		public void CacheSearchItem(SearchItem item)
        {
            var prefix = item.MediaType;
            var cacheKey = $"{prefix}_extid_{item.ExternalId}";
            cache.Set(cacheKey, item);
            if (item.MediaType == "audio")
            {
                CacheAudioSearchItem(item, cacheKey);
                return;
            }
            else if (item.MediaType == "book")
            {
                    CacheBookSearchItem(item, cacheKey);
                    return;
            }

            var normalizedTitle = item.Title.RemoveAccentButKeepGermanUmlauts().ToLower();

            cache.Set($"{prefix}_title_{normalizedTitle}", item);

            foreach (var variation in item.TitleMatchVariations)
            {
                var normalizedVariation = variation.RemoveAccentButKeepGermanUmlauts().ToLower();
                cacheKey = $"{prefix}_var_{normalizedVariation}";
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

        public void CacheAudioSearchItem(SearchItem item, string cacheKey)
        {
            // Index author and title variations
            foreach (var authorVariation in item.AuthorMatchVariations)
            {
                var normalizedAuthor = authorVariation.NormalizeForComparison();

                if (!AudioVariationIndex.ContainsKey(normalizedAuthor))
                {
                    AudioVariationIndex[normalizedAuthor] = [];
                }

                var titleVariations = item.TitleMatchVariations.Select(titleMatchVariation => titleMatchVariation.NormalizeForComparison()).ToHashSet();
                AudioVariationIndex[normalizedAuthor].Add((titleVariations, cacheKey));
            }
        }

        public void CacheBookSearchItem(SearchItem item, string cacheKey)
        {
            // Index author and title variations
            foreach (var authorVariation in item.AuthorMatchVariations)
            {
                var normalizedAuthor = authorVariation.NormalizeForComparison();

                if (!BookVariationIndex.ContainsKey(normalizedAuthor))
                {
                    BookVariationIndex[normalizedAuthor] = [];
                }

                var titleVariations = item.TitleMatchVariations.Select(titleMatchVariation => titleMatchVariation.NormalizeForComparison()).ToHashSet();
                BookVariationIndex[normalizedAuthor].Add((titleVariations, cacheKey));
            }
        }

        public SearchItem? SearchItemByTitle(string mediaType, string title)
        {
            var normalizedTitle = title.RemoveAccentButKeepGermanUmlauts().ToLower();

            if (mediaType == "audio" || mediaType == "book")
            {
                return FindBestMatchForBooksAndAudio(normalizedTitle.NormalizeForComparison(), mediaType);
            }

            // Use the first few characters of the normalized title for cache prefix search
            var cacheSearchPrefix = normalizedTitle[..Math.Min(VARIATION_LOOKUP_CACHE_LENGTH, normalizedTitle.Length)];

            SearchItem? bestSearchItemMatch = null;
            var bestVariationMatchLength = 0;
            HashSet<string> checkedSearchItems = [];

            if (VariationIndex.TryGetValue(cacheSearchPrefix, out var cacheKeys))
            {
                foreach (var cacheKey in cacheKeys)
                {
                    if (cache.TryGetValue(cacheKey, out SearchItem? item))
                    {
                        if (item == null || item.MediaType != mediaType)
                        {
                            continue;
                        }

                        var searchItemIdentifier = $"{item.MediaType}_{item.ExternalId}";

                        if (checkedSearchItems.Contains(searchItemIdentifier))
                        {
                            continue;
                        }
                        else
                        {
                            checkedSearchItems.Add(searchItemIdentifier);
                        }

                        // After finding a potential item, compare normalizedTitle with each German title variation
                        foreach (var variation in item.TitleMatchVariations ?? [])
                        {
                            var normalizedVariation = variation.RemoveAccentButKeepGermanUmlauts().ToLower();
                            if (normalizedTitle.StartsWith(variation, StringComparison.OrdinalIgnoreCase))
                            {
                                // If we find a variation match that is "longer" then most likely that one is correct and the earlier match was wrong (if it was from another searchItem)
                                if (variation.Length > bestVariationMatchLength)
                                {
                                    bestSearchItemMatch = item;
                                    bestVariationMatchLength = variation.Length;
                                }
                            }
                        }
                    }
                }
            }

            return bestSearchItemMatch;
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

        private SearchItem? FindBestMatchForBooksAndAudio(string normalizedOriginalTitle, string mediaType)
        {
            var index = mediaType == "audio" ? AudioVariationIndex : BookVariationIndex;
            
            foreach (var authorEntry in index)
            {
                if (normalizedOriginalTitle.Contains(authorEntry.Key))
                {
                    var sortedEntries = authorEntry.Value.OrderByDescending(entry => entry.TitleVariations.FirstOrDefault()?.Length).ToList();

                    foreach (var (titleVariations, cacheKey) in sortedEntries)
                    {
                        if (titleVariations.Any(normalizedOriginalTitle.Contains))
                        {
                            if (cache.TryGetValue(cacheKey, out SearchItem? item))
                            {
                                return item;
                            }
                        }
                    }
                }
            }

            return null;
        }

		public void CacheTitleRename(string changedTitle, string originalTitle)
		{
			if (string.IsNullOrWhiteSpace(changedTitle) || string.IsNullOrWhiteSpace(originalTitle))
				return;

			var key = $"{TitleRenamePrefix}{changedTitle.Trim().ToLowerInvariant()}";
			cache.Set(key, originalTitle, TitleRenameCacheDuration);

			// If title contains ":" also add it as "-" for arr/sabnzbd compatibility
			if (changedTitle.Contains(':'))
			{
				var altKey = $"{TitleRenamePrefix}{changedTitle.Replace(':', '-').Trim().ToLowerInvariant()}";
				cache.Set(altKey, originalTitle, TitleRenameCacheDuration);
			}
		}

		public string? GetOriginalTitleFromRenamed(string changedTitle)
		{
			var key = $"{TitleRenamePrefix}{changedTitle.Trim().ToLowerInvariant()}";
			return cache.TryGetValue(key, out string? originalTitle) ? originalTitle : null;
		}
	}
}
