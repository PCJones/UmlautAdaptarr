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
        TitleApiService titleService,
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
                    logger.LogInformation($"Fetching all albums from artistId {artistId} from Lidarr: {UrlUtilities.RedactApiKey(lidarrArtistsUrl)}");
                    var albumApiResponse = await httpClient.GetStringAsync(lidarrAlbumUrl);
                    var albums = JsonConvert.DeserializeObject<List<dynamic>>(albumApiResponse);

                    if (albums == null)
                    {
                        logger.LogWarning($"Lidarr album API request for artistId {artistId} resulted in null");
                        continue;
                    }

                    logger.LogInformation($"Successfully fetched {albums.Count} albums for artistId {artistId} from Lidarr.");

                    foreach (var album in albums)
                    {
                        var artistName = (string)album.artist.artistName;
                        var albumTitle = (string)album.title;

                        var expectedTitle = $"{artistName} {albumTitle}";

                        string[]? aliases = null;

                        // Abuse externalId to set the search string Lidarr uses
                        var externalId = expectedTitle.RemoveGermanUmlautDots().RemoveAccent().RemoveSpecialCharacters().RemoveExtraWhitespaces().ToLower();

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
            var httpClient = clientFactory.CreateClient();

            try
            {
                var lidarrUrl = $"{_lidarrHost}/api/v1/series?mbId={externalId}&includeSeasonImages=false&apikey={_lidarrApiKey}";
                logger.LogInformation($"Fetching item by external ID from Lidarr: {UrlUtilities.RedactApiKey(lidarrUrl)}");
                var response = await httpClient.GetStringAsync(lidarrUrl);
                var artists = JsonConvert.DeserializeObject<dynamic>(response);
                var artist = artists?[0];

                if (artist != null)
                {
                    var mbId = (string)artist.mbId;
                    if (mbId == null)
                    {
                        logger.LogWarning($"Lidarr Artist {artist.id} doesn't have a mbId.");
                        return null;
                    }
                    (var germanTitle, var aliases) = await titleService.FetchGermanTitleAndAliasesByExternalIdAsync(_mediaType, mbId);

                    throw new NotImplementedException();

                    var searchItem = new SearchItem
                    (
                        arrId: (int)artist.id,
                        externalId: mbId,
                        title: (string)artist.title,
                        expectedTitle: (string)artist.title,
                        germanTitle: germanTitle,
                        aliases: aliases,
                        mediaType: _mediaType,
                        expectedAuthor: "TODO"
                    ); ;

                    logger.LogInformation($"Successfully fetched artist {searchItem} from Lidarr.");
                    return searchItem;
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
            var httpClient = clientFactory.CreateClient();

            try
            {
                (string? germanTitle, string? mbId, string[]? aliases) = await titleService.FetchGermanTitleAndExternalIdAndAliasesByTitle(_mediaType, title);

                if (mbId == null)
                {
                    return null;
                }

                var lidarrUrl = $"{_lidarrHost}/api/v1/series?mbId={mbId}&includeSeasonImages=false&apikey={_lidarrApiKey}";
                var lidarrApiResponse = await httpClient.GetStringAsync(lidarrUrl);
                var artists = JsonConvert.DeserializeObject<dynamic>(lidarrApiResponse);

                if (artists == null)
                {
                    logger.LogError($"Parsing Lidarr API response for MB ID {mbId} resulted in null");
                    return null;
                }
                else if (artists.Count == 0)
                {
                    logger.LogWarning($"No results found for MB ID {mbId}");
                    return null;
                }

                var expectedTitle = (string)artists[0].title;
                if (expectedTitle == null)
                {
                    logger.LogError($"Lidarr Title for MB ID {mbId} is null");
                    return null;
                }

                throw new NotImplementedException();
                var searchItem = new SearchItem
                (
                    arrId: (int)artists[0].id,
                    externalId: mbId,
                    title: (string)artists[0].title,
                    expectedTitle: (string)artists[0].title,
                    germanTitle: germanTitle,
                    aliases: aliases,
                    mediaType: _mediaType,
                    expectedAuthor: "TODO"
                );

                logger.LogInformation($"Successfully fetched artist {searchItem} from Lidarr.");
                return searchItem;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching single artist from Lidarr: {ex.Message}");
            }

            return null;
        }
    }
}
