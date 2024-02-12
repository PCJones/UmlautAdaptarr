using UmlautAdaptarr.Models;
using UmlautAdaptarr.Providers;

namespace UmlautAdaptarr.Services
{
    public class SearchItemLookupService(CacheService cacheService, SonarrClient sonarrClient)
    {
        public async Task<SearchItem?> GetOrFetchSearchItemByExternalId(string mediaType, string externalId)
        {
            // Attempt to get the item from the cache first
            var cachedItem = cacheService.GetSearchItemByExternalId(mediaType, externalId);
            if (cachedItem != null)
            {
                return cachedItem;
            }

            // If not found in cache, fetch from the appropriate source
            SearchItem? fetchedItem = null;
            switch (mediaType)
            {
                case "tv":
                    fetchedItem = await sonarrClient.FetchItemByExternalIdAsync(externalId);
                    break;
                    // TODO Add cases for other sources like Radarr, Lidarr, etc.
            }

            // If an item is fetched, cache it
            if (fetchedItem != null)
            {
                cacheService.CacheSearchItem(fetchedItem);
            }

            return fetchedItem;
        }

        public async Task<SearchItem?> GetOrFetchSearchItemByTitle(string mediaType, string title)
        {
            // Attempt to get the item from the cache first
            var cachedItem = cacheService.GetSearchItemByTitle(mediaType, title);
            if (cachedItem != null)
            {
                return cachedItem;
            }

            // If not found in cache, fetch from the appropriate source
            SearchItem? fetchedItem = null;
            switch (mediaType)
            {
                case "tv":
                    fetchedItem = await sonarrClient.FetchItemByTitleAsync(title);
                    break;
                    // TODO add cases for other sources as needed, such as Radarr, Lidarr, etc.
            }

            // If an item is fetched, cache it
            if (fetchedItem != null)
            {
                cacheService.CacheSearchItem(fetchedItem);
            }

            return fetchedItem;
        }
    }

}
