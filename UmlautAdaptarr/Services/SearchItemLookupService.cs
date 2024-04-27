using UmlautAdaptarr.Models;
using UmlautAdaptarr.Providers;
using UmlautAdaptarr.Services.Factory;

namespace UmlautAdaptarr.Services
{
    public class SearchItemLookupService(CacheService cacheService,
        RrApplicationFactory rrApplicationFactory)
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

                    var sonarrInstances = rrApplicationFactory.SonarrInstances;

                    if (sonarrInstances.Any())
                    {
                        foreach (var sonarrClient in sonarrInstances)
                        {
                            fetchedItem = await sonarrClient.FetchItemByExternalIdAsync(externalId);
                        }
                    }
                    break;
                case "audio":

                    var lidarrInstances = rrApplicationFactory.LidarrInstances;

                    if (lidarrInstances.Any())
                    {
                        foreach (var lidarrClient in lidarrInstances)
                        {
                            await lidarrClient.FetchItemByExternalIdAsync(externalId);
                            fetchedItem = cacheService.GetSearchItemByExternalId(mediaType, externalId);
                        }
                    }
                    break;
                case "book":

                    var readarrInstances = rrApplicationFactory.ReadarrInstances;
                    if (readarrInstances.Any())
                    {
                        foreach (var readarrClient in readarrInstances)
                        {
                            await readarrClient.FetchItemByExternalIdAsync(externalId);
                            fetchedItem = cacheService.GetSearchItemByExternalId(mediaType, externalId);
                        }
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

                    var sonarrInstances = rrApplicationFactory.SonarrInstances;
                    foreach (var sonarrClient in sonarrInstances)
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
