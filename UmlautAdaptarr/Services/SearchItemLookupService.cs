using UmlautAdaptarr.Models;
using UmlautAdaptarr.Providers;

namespace UmlautAdaptarr.Services
{
    public class SearchItemLookupService(CacheService cacheService,
                                         SonarrClient sonarrClient,
                                         ReadarrClient readarrClient,
                                         LidarrClient lidarrClient)
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
                    if (sonarrClient.SonarrOptions.Enabled)
                    {
                        fetchedItem = await sonarrClient.FetchItemByExternalIdAsync(externalId);
                    }
                    break;
                case "audio":
                    if (lidarrClient.LidarrOptions.Enabled)
                    {
                        await lidarrClient.FetchItemByExternalIdAsync(externalId);
                        fetchedItem = cacheService.GetSearchItemByExternalId(mediaType, externalId);
                    }
                    break;
                case "book":
                    if (readarrClient.ReadarrOptions.Enabled)
                    {
                        await readarrClient.FetchItemByExternalIdAsync(externalId);
                        fetchedItem = cacheService.GetSearchItemByExternalId(mediaType, externalId);
                    }
                    break;
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
                    if (sonarrClient.SonarrOptions.Enabled)
                    {
                        fetchedItem = await sonarrClient.FetchItemByTitleAsync(title);
                    }
                    break;
                case "audio":
                    break;
                case "book":
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
