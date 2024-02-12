using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Providers;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public class TitleQueryServiceLegacy(
        IMemoryCache memoryCache,
        ILogger<TitleQueryServiceLegacy> logger,
        IConfiguration configuration,
        IHttpClientFactory clientFactory,
        SonarrClient sonarrClient)
    {
        private readonly HttpClient _httpClient = clientFactory.CreateClient("HttpClient") ?? throw new ArgumentNullException();
        private readonly string _sonarrHost = configuration.GetValue<string>("SONARR_HOST") ?? throw new ArgumentException("SONARR_HOST environment variable must be set");
        private readonly string _sonarrApiKey = configuration.GetValue<string>("SONARR_API_KEY") ?? throw new ArgumentException("SONARR_API_KEY environment variable must be set");
        private readonly string _umlautAdaptarrApiHost = configuration["Settings:UmlautAdaptarrApiHost"] ?? throw new ArgumentException("UmlautAdaptarrApiHost must be set in appsettings.json");

        /*public async Task<(bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle)> QueryGermanShowTitleByTVDBId(string tvdbId)
        {
            var sonarrCacheKey = $"SearchItem_Sonarr_{tvdbId}";

            if (memoryCache.TryGetValue(sonarrCacheKey, out SearchItem? cachedItem))
            {
                return (cachedItem?.HasGermanUmlaut ?? false, cachedItem?.GermanTitle, cachedItem?.ExpectedTitle ?? string.Empty);
            }
            else
            {
                var sonarrUrl = $"{_sonarrHost}/api/v3/series?tvdbId={tvdbId}&includeSeasonImages=false&apikey={_sonarrApiKey}";
                var sonarrApiResponse = await _httpClient.GetStringAsync(sonarrUrl);
                var shows = JsonConvert.DeserializeObject<dynamic>(sonarrApiResponse);

                if (shows == null)
                {
                    logger.LogError($"Parsing Sonarr API response for TVDB ID {tvdbId} resulted in null");
                    return (false, null, string.Empty);
                }
                else if (shows.Count == 0)
                {
                    logger.LogWarning($"No results found for TVDB ID {tvdbId}");
                    return (false, null, string.Empty);
                }

                var expectedTitle = (string)shows[0].title;
                if (expectedTitle == null)
                {
                    logger.LogError($"Sonarr Title for TVDB ID {tvdbId} is null");
                    return (false, null, string.Empty);
                }

                string? germanTitle = null;
                var hasGermanTitle = false;

                var titleApiUrl = $"{_umlautAdaptarrApiHost}/tvshow_german.php?tvdbid={tvdbId}";
                var titleApiResponse = await _httpClient.GetStringAsync(titleApiUrl);
                var titleApiResponseData = JsonConvert.DeserializeObject<dynamic>(titleApiResponse);

                if (titleApiResponseData == null)
                {
                    logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response for TVDB ID {tvdbId} resulted in null");
                    return (false, null, string.Empty);
                }

                if (titleApiResponseData.status == "success" && !string.IsNullOrEmpty((string)titleApiResponseData.germanTitle))
                {
                    germanTitle = titleApiResponseData.germanTitle;
                    hasGermanTitle = true;
                }

                var hasGermanUmlaut = germanTitle?.HasGermanUmlauts() ?? false;

                var result = (hasGermanUmlaut, germanTitle, expectedTitle);
                memoryCache.Set(showCacheKey, result, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    SlidingExpiration = hasGermanTitle ? TimeSpan.FromDays(30) : TimeSpan.FromDays(7)
                });

                return result;
            }
        }*/

        // This method is being used if the *arrs do a search with the "q" parameter (text search)
        public async Task<(bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle)> QueryGermanShowTitleByTitle(string title)
        {
            // TVDB doesn't use ß - TODO: Determine if this is true
            var tvdbCleanTitle = title.Replace("ß", "ss");

            var cacheKey = $"show_{tvdbCleanTitle}";
            if (memoryCache.TryGetValue(cacheKey, out (bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle) cachedResult))
            {
                return cachedResult;
            }

            var titleApiUrl = $"{_umlautAdaptarrApiHost}/tvshow_german.php?title={tvdbCleanTitle}";
            var titleApiResponse = await _httpClient.GetStringAsync(titleApiUrl);
            var titleApiResponseData = JsonConvert.DeserializeObject<dynamic>(titleApiResponse);

            if (titleApiResponseData == null)
            {
                logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response for title {title} resulted in null");
                return (false, null, string.Empty);
            }

            if (titleApiResponseData.status == "success" && !string.IsNullOrEmpty((string)titleApiResponseData.germanTitle))
            {
                var tvdbId = (string)titleApiResponseData.tvdbId;
                if (tvdbId == null)
                {
                    logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response tvdbId {titleApiResponseData} resulted in null");
                    return (false, null, string.Empty);
                }

                var sonarrUrl = $"{_sonarrHost}/api/v3/series?tvdbId={tvdbId}&includeSeasonImages=false&apikey={_sonarrApiKey}";
                var sonarrApiResponse = await _httpClient.GetStringAsync(sonarrUrl);
                var shows = JsonConvert.DeserializeObject<dynamic>(sonarrApiResponse);

                if (shows == null)
                {
                    logger.LogError($"Parsing Sonarr API response for TVDB ID {tvdbId} resulted in null");
                    return (false, null, string.Empty);
                }
                else if (shows.Count == 0)
                {
                    logger.LogWarning($"No results found for TVDB ID {tvdbId}");
                    return (false, null, string.Empty);
                }

                var expectedTitle = (string)shows[0].title;
                if (expectedTitle == null)
                {
                    logger.LogError($"Sonarr Title for TVDB ID {tvdbId} is null");
                    return (false, null, string.Empty);
                }

                string germanTitle ;
                bool hasGermanTitle;

                germanTitle = titleApiResponseData.germanTitle;
                hasGermanTitle = true;

                var hasGermanUmlaut = germanTitle?.HasGermanUmlauts() ?? false;

                var result = (hasGermanUmlaut, germanTitle, expectedTitle);
                memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    SlidingExpiration = hasGermanTitle ? TimeSpan.FromDays(30) : TimeSpan.FromDays(7)
                });

                return result;
            }
            else
            {
                logger.LogWarning($"UmlautAdaptarr TitleQuery {titleApiUrl} didn't succeed.");
                return (false, null, string.Empty);
            }
        }
    }
}
