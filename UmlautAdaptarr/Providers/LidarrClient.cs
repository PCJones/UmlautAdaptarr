using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Options.ArrOptions.InstanceOptions;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Providers;

public class LidarrClient : ArrClientBase
{
    private readonly IMemoryCache _cache;
    private readonly CacheService _cacheService;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<LidarrClient> _logger;
    private readonly string _mediaType = "audio";

    public LidarrClient([ServiceKey] string instanceName,
        IHttpClientFactory clientFactory,
        CacheService cacheService,
        IMemoryCache cache, IOptionsMonitor<LidarrInstanceOptions> options,
        ILogger<LidarrClient> logger)
    {
        _clientFactory = clientFactory;
        _cacheService = cacheService;
        _cache = cache;
        _logger = logger;
        InstanceName = instanceName;
        Options = options.Get(InstanceName);
        _logger.LogInformation($"Init Lidarr ({InstanceName})");
    }

    public LidarrInstanceOptions Options { get; init; }


    public override async Task<IEnumerable<SearchItem>> FetchAllItemsAsync()
    {
        var httpClient = _clientFactory.CreateClient();
        var items = new List<SearchItem>();

        try
        {
            var lidarrArtistsUrl = $"{Options.Host}/api/v1/artist?apikey={Options.ApiKey}";
            _logger.LogInformation(
                $"Fetching all artists from Lidarr ({InstanceName}) : {UrlUtilities.RedactApiKey(lidarrArtistsUrl)}");
            var artistsApiResponse = await httpClient.GetStringAsync(lidarrArtistsUrl);
            var artists = JsonConvert.DeserializeObject<List<dynamic>>(artistsApiResponse);

            if (artists == null)
            {
                _logger.LogError($"Lidarr ({InstanceName}) artists API request resulted in null");
                return items;
            }

            _logger.LogInformation($"Successfully fetched {artists.Count} artists from Lidarr ({InstanceName}).");
            foreach (var artist in artists)
            {
                var artistId = (int)artist.id;

                var lidarrAlbumUrl = $"{Options.Host}/api/v1/album?artistId={artistId}&apikey={Options.ApiKey}";

                // TODO add caching here
                // Disable cache for now as it can result in problems when adding new albums that aren't displayed on the artists page initially
                //if (cache.TryGetValue(lidarrAlbumUrl, out List<dynamic>? albums))
                //{
                //    logger.LogInformation($"Using cached albums for {UrlUtilities.RedactApiKey(lidarrAlbumUrl)}");
                //}
                //else
                //{
                _logger.LogInformation(
                    $"Fetching all albums from artistId {artistId} from Lidarr ({InstanceName}) : {UrlUtilities.RedactApiKey(lidarrAlbumUrl)}");
                var albumApiResponse = await httpClient.GetStringAsync(lidarrAlbumUrl);
                var albums = JsonConvert.DeserializeObject<List<dynamic>>(albumApiResponse);
                //}

                if (albums == null)
                {
                    _logger.LogWarning(
                        $"Lidarr ({InstanceName}) album API request for artistId {artistId} resulted in null");
                    continue;
                }

                _logger.LogInformation(
                    $"Successfully fetched {albums.Count} albums for artistId {artistId} from Lidarr ({InstanceName}).");

                // Cache albums for 3 minutes
                _cache.Set(lidarrAlbumUrl, albums, TimeSpan.FromMinutes(3));

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
                        artistId,
                        externalId,
                        albumTitle,
                        albumTitle,
                        null,
                        aliases: aliases,
                        mediaType: _mediaType,
                        expectedAuthor: artistName
                    );

                    items.Add(searchItem);
                }
            }

            _logger.LogInformation($"Finished fetching all items from Lidarr ({InstanceName})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching all artists from Lidarr ({InstanceName}) : {ex.Message}");
        }

        return items;
    }

    public override async Task<SearchItem?> FetchItemByExternalIdAsync(string externalId)
    {
        try
        {
            // For now we have to fetch all items every time
            // TODO if possible look at the author in search query and only update for author
            var searchItems = await FetchAllItemsAsync();
            foreach (var searchItem in searchItems ?? [])
                try
                {
                    _cacheService.CacheSearchItem(searchItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"An error occurred while caching search item with ID {searchItem.ArrId} in Lidarr ({InstanceName}).");
                }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching single artist from Lidarr ({InstanceName}) : {ex.Message}");
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
            _logger.LogError($"Error fetching single artist from Lidarr ({InstanceName}): {ex.Message}");
        }

        return null;
    }
}