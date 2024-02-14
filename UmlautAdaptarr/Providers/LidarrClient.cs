using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Providers
{
    public class LidarrClient(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        CacheService cacheService,
        IMemoryCache cache,
        ILogger<LidarrClient> logger) : ArrClientBase()
    {
        private readonly string _lidarrHost = configuration.GetValue<string>("LIDARR_HOST") ?? throw new ArgumentException("LIDARR_HOST environment variable must be set");
        private readonly string _lidarrApiKey = configuration.GetValue<string>("LIDARR_API_KEY") ?? throw new ArgumentException("LIDARR_API_KEY environment variable must be set");
        private readonly string _mediaType = "audio";

        public override async Task<IEnumerable<SearchItem>> FetchAllItemsAsync()
        {
            var httpClient = clientFactory.CreateClient();
            var items = new List<SearchItem>();

            try
            {
                var lidarrArtistsUrl = $"{_lidarrHost}/api/v1/artist?apikey={_lidarrApiKey}";
                logger.LogInformation($"Fetching all artists from Lidarr: {UrlUtilities.RedactApiKey(lidarrArtistsUrl)}");
                var artistsApiResponse = await httpClient.GetStringAsync(lidarrArtistsUrl);
                var artists = JsonConvert.DeserializeObject<List<dynamic>>(artistsApiResponse);

                if (artists == null)
                {
                    logger.LogError($"Lidarr artists API request resulted in null");
                    return items;
                }
                logger.LogInformation($"Successfully fetched {artists.Count} artists from Lidarr.");
                foreach (var artist in artists)
                {
                    var artistId = (int)artist.id;

                    var lidarrAlbumUrl = $"{_lidarrHost}/api/v1/album?artistId={artistId}&apikey={_lidarrApiKey}";

                    // Disable cache for now as it can result in problems when adding new albums that aren't displayed on the artists page initially
                    //if (cache.TryGetValue(lidarrAlbumUrl, out List<dynamic>? albums))
                    //{
                    //    logger.LogInformation($"Using cached albums for {UrlUtilities.RedactApiKey(lidarrAlbumUrl)}");
                    //}
                    //else
                    //{
                    logger.LogInformation($"Fetching all albums from artistId {artistId} from Lidarr: {UrlUtilities.RedactApiKey(lidarrArtistsUrl)}");
                    var albumApiResponse = await httpClient.GetStringAsync(lidarrAlbumUrl);
                    var albums = JsonConvert.DeserializeObject<List<dynamic>>(albumApiResponse);
                    //}

                    if (albums == null)
                    {
                        logger.LogWarning($"Lidarr album API request for artistId {artistId} resulted in null");
                        continue;
                    }

                    logger.LogInformation($"Successfully fetched {albums.Count} albums for artistId {artistId} from Lidarr.");

                    // Cache albums for 3 minutes
                    cache.Set(lidarrAlbumUrl, albums, TimeSpan.FromMinutes(3));

                    foreach (var album in albums)
                    {
                        var artistName = (string)album.artist.artistName;
                        var albumTitle = (string)album.title;

                        var expectedTitle = $"{artistName} {albumTitle}";

                        string[]? aliases = null;

                        // Abuse externalId to set the search string Lidarr uses
                        var externalId = expectedTitle.GetLidarrTitleForExternalId();

                        var searchItem = new SearchItem
                        (
                            arrId: artistId,
                            externalId: externalId,
                            title: albumTitle,
                            expectedTitle: albumTitle,
                            germanTitle: null,
                            aliases: aliases,
                            mediaType: _mediaType,
                            expectedAuthor: artistName
                        );

                        items.Add(searchItem);
                    }
                }

                logger.LogInformation($"Finished fetching all items from Lidarr");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching all artists from Lidarr: {ex.Message}");
            }

            return items;
        }

        public override async Task<SearchItem?> FetchItemByExternalIdAsync(string externalId)
        {
            try
            {
                // For now we have to fetch all items every time
                var searchItems = await FetchAllItemsAsync();
                foreach (var searchItem in searchItems ?? [])
                {
                    try
                    {
                        cacheService.CacheSearchItem(searchItem);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"An error occurred while caching search item with ID {searchItem.ArrId}.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching single artist from Lidarr: {ex.Message}");
            }

            return null;
        }

        public override async Task<SearchItem?> FetchItemByTitleAsync(string title)
        {
            try
            {
               // this should never be called at the moment
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching single artist from Lidarr: {ex.Message}");
            }

            return null;
        }
    }
}
