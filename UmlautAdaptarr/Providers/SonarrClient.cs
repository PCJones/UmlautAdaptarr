using Newtonsoft.Json;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Providers
{
    public class SonarrClient(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        TitleApiService titleService,
        ILogger<SonarrClient> logger) : ArrClientBase()
    {
        private readonly string _sonarrHost = configuration.GetValue<string>("SONARR_HOST") ?? throw new ArgumentException("SONARR_HOST environment variable must be set");
        private readonly string _sonarrApiKey = configuration.GetValue<string>("SONARR_API_KEY") ?? throw new ArgumentException("SONARR_API_KEY environment variable must be set");
        private readonly string _mediaType = "tv";

        public override async Task<IEnumerable<SearchItem>> FetchAllItemsAsync()
        {
            var httpClient = clientFactory.CreateClient();
            var items = new List<SearchItem>();

            try
            {

                var sonarrUrl = $"{_sonarrHost}/api/v3/series?includeSeasonImages=false&apikey={_sonarrApiKey}";
                logger.LogInformation($"Fetching all items from Sonarr: {UrlUtilities.RedactApiKey(sonarrUrl)}");
                var response = await httpClient.GetStringAsync(sonarrUrl);
                var shows = JsonConvert.DeserializeObject<List<dynamic>>(response);

                if (shows != null)
                {
                    logger.LogInformation($"Successfully fetched {shows.Count} items from Sonarr.");
                    foreach (var show in shows)
                    {
                        var tvdbId = (string)show.tvdbId;
                        if (tvdbId == null)
                        {
                            logger.LogWarning($"Sonarr Show {show.id} doesn't have a tvdbId.");
                            continue;
                        }
                        (var germanTitle, var aliases) = await titleService.FetchGermanTitleAndAliasesByExternalIdAsync(_mediaType, tvdbId);
                        var searchItem = new SearchItem
                        (
                            arrId: (int)show.id,
                            externalId: tvdbId,
                            title: (string)show.title,
                            expectedTitle: (string)show.title,
                            germanTitle: germanTitle,
                            aliases: aliases,
                            mediaType: _mediaType
                        );

                        items.Add(searchItem);
                    }
                }

                logger.LogInformation($"Finished fetching all items from Sonarr");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching all shows from Sonarr: {ex.Message}");
            }

            return items;
        }

        public override async Task<SearchItem?> FetchItemByExternalIdAsync(string externalId)
        {
            var httpClient = clientFactory.CreateClient();

            try
            {
                var sonarrUrl = $"{_sonarrHost}/api/v3/series?tvdbId={externalId}&includeSeasonImages=false&apikey={_sonarrApiKey}";
                logger.LogInformation($"Fetching item by external ID from Sonarr: {UrlUtilities.RedactApiKey(sonarrUrl)}");
                var response = await httpClient.GetStringAsync(sonarrUrl);
                var shows = JsonConvert.DeserializeObject<dynamic>(response);
                var show = shows?[0];

                if (show != null)
                {
                    var tvdbId = (string)show.tvdbId;
                    if (tvdbId == null)
                    {
                        logger.LogWarning($"Sonarr Show {show.id} doesn't have a tvdbId.");
                        return null;
                    }
                    (var germanTitle, var aliases) = await titleService.FetchGermanTitleAndAliasesByExternalIdAsync(_mediaType, tvdbId);
                    
                    var searchItem = new SearchItem
                    (
                        arrId: (int)show.id,
                        externalId: tvdbId,
                        title: (string)show.title,
                        expectedTitle: (string)show.title,
                        germanTitle: germanTitle,
                        aliases: aliases,
                        mediaType: _mediaType
                    );

                    logger.LogInformation($"Successfully fetched show {searchItem} from Sonarr.");
                    return searchItem;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching single show from Sonarr: {ex.Message}");
            }

            return null;
        }

        public override async Task<SearchItem?> FetchItemByTitleAsync(string title)
        {
            var httpClient = clientFactory.CreateClient();

            try
            {
                (string? germanTitle, string? tvdbId, string[]? aliases) = await titleService.FetchGermanTitleAndExternalIdAndAliasesByTitle(_mediaType, title);

                if (tvdbId == null)
                {
                    return null;
                }

                var sonarrUrl = $"{_sonarrHost}/api/v3/series?tvdbId={tvdbId}&includeSeasonImages=false&apikey={_sonarrApiKey}";
                var sonarrApiResponse = await httpClient.GetStringAsync(sonarrUrl);
                var shows = JsonConvert.DeserializeObject<dynamic>(sonarrApiResponse);

                if (shows == null)
                {
                    logger.LogError($"Parsing Sonarr API response for TVDB ID {tvdbId} resulted in null");
                    return null;
                }
                else if (shows.Count == 0)
                {
                    logger.LogWarning($"No results found for TVDB ID {tvdbId}");
                    return null;
                }

                var expectedTitle = (string)shows[0].title;
                if (expectedTitle == null)
                {
                    logger.LogError($"Sonarr Title for TVDB ID {tvdbId} is null");
                    return null;
                }

                var searchItem = new SearchItem
                (
                    arrId: (int)shows[0].id,
                    externalId: tvdbId,
                    title: (string)shows[0].title,
                    expectedTitle: (string)shows[0].title,
                    germanTitle: germanTitle,
                    aliases: aliases,
                    mediaType: _mediaType
                );

                logger.LogInformation($"Successfully fetched show {searchItem} from Sonarr.");
                return searchItem;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching single show from Sonarr: {ex.Message}");
            }

            return null;
        }
    }
}
