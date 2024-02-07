using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public class TitleQueryService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TitleQueryService> _logger;
        private readonly string _sonarrHost;
        private readonly string _sonarrApiKey;
        private readonly string _umlautAdaptarrApiHost;

        public TitleQueryService(IMemoryCache memoryCache, ILogger<TitleQueryService> logger, IConfiguration configuration, IHttpClientFactory clientFactory)
        {
            _httpClient = clientFactory.CreateClient("HttpClient") ?? throw new ArgumentNullException();
            _cache = memoryCache;
            _logger = logger;

            _sonarrHost = configuration.GetValue<string>("SONARR_HOST");
            _sonarrApiKey = configuration.GetValue<string>("SONARR_API_KEY");
            _umlautAdaptarrApiHost = configuration["Settings:UmlautAdaptarrApiHost"] ?? throw new ArgumentException("UmlautAdaptarrApiHost must be set in appsettings.json");

        }

        public async Task<(bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle)> QueryGermanShowTitleByTVDBId(string tvdbId)
        {
            var cacheKey = $"show_{tvdbId}";
            if (_cache.TryGetValue(cacheKey, out (bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle) cachedResult))
            {
                return cachedResult;
            }

            var sonarrUrl = $"{_sonarrHost}/api/v3/series?tvdbId={tvdbId}&includeSeasonImages=false&apikey={_sonarrApiKey}";
            var response = await _httpClient.GetStringAsync(sonarrUrl);
            var shows = JsonConvert.DeserializeObject<dynamic>(response);

            if (shows == null)
            {
                _logger.LogError($"Parsing Sonarr API response for TVDB ID {tvdbId} resulted in null");
                return (false, null, string.Empty);
            } else if (shows.Count == 0)
            {
                _logger.LogWarning($"No results found for TVDB ID {tvdbId}");
                return (false, null, string.Empty);
            }

            var expectedTitle = (string)shows[0].title;
            if (expectedTitle == null)
            {
                _logger.LogError($"Sonarr Title for TVDB ID {tvdbId} is null");
                return (false, null, string.Empty);
            }

            string? germanTitle = null;
            var hasGermanTitle = false;
            var originalLanguage = (string)shows[0].originalLanguage.name;

            if (originalLanguage != "German")
            {
                var apiUrl = $"{_umlautAdaptarrApiHost}/tvshow_german.php?tvdbid={tvdbId}";
                var apiResponse = await _httpClient.GetStringAsync(apiUrl);
                var responseData = JsonConvert.DeserializeObject<dynamic>(apiResponse);

                if (responseData == null)
                {
                    _logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response for TVDB ID {tvdbId} resulted in null");
                    return (false, null, string.Empty);
                }

                if (responseData.status == "success" && !string.IsNullOrEmpty((string)responseData.germanTitle))
                {
                    germanTitle = responseData.germanTitle;
                    hasGermanTitle = true;
                }
            }
            else
            {
                germanTitle = expectedTitle;
                hasGermanTitle = true;
            }

            var hasGermanUmlaut = germanTitle?.HasGermanUmlauts() ?? false;

            var result = (hasGermanUmlaut, germanTitle, expectedTitle);
            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = hasGermanTitle ? TimeSpan.FromDays(30) : TimeSpan.FromDays(7)
            });

            return result;
        }

        public async Task<(bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle)> QueryGermanShowTitleByTitle(string title)
        {
            // TVDB doesn't use ß
            var tvdbCleanTitle = title.Replace("ß", "ss");

            var cacheKey = $"show_{tvdbCleanTitle}";
            if (_cache.TryGetValue(cacheKey, out (bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle) cachedResult))
            {
                return cachedResult;
            }

            var apiUrl = $"{_umlautAdaptarrApiHost}/tvshow_german.php?title={tvdbCleanTitle}";
            var apiResponse = await _httpClient.GetStringAsync(apiUrl);
            var responseData = JsonConvert.DeserializeObject<dynamic>(apiResponse);

            if (responseData == null)
            {
                _logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response for title {title} resulted in null");
                return (false, null, string.Empty);
            }

            if (responseData.status == "success" && !string.IsNullOrEmpty((string)responseData.germanTitle))
            {
                var tvdbId = (string)responseData.tvdbId;
                if (tvdbId == null)
                {
                    _logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response tvdbId {responseData} resulted in null");
                    return (false, null, string.Empty);
                }

                var sonarrUrl = $"{_sonarrHost}/api/v3/series?tvdbId={tvdbId}&includeSeasonImages=false&apikey={_sonarrApiKey}";
                var response = await _httpClient.GetStringAsync(sonarrUrl);
                var shows = JsonConvert.DeserializeObject<dynamic>(response);

                if (shows == null)
                {
                    _logger.LogError($"Parsing Sonarr API response for TVDB ID {tvdbId} resulted in null");
                    return (false, null, string.Empty);
                }
                else if (shows.Count == 0)
                {
                    _logger.LogWarning($"No results found for TVDB ID {tvdbId}");
                    return (false, null, string.Empty);
                }

                var expectedTitle = (string)shows[0].title;
                if (expectedTitle == null)
                {
                    _logger.LogError($"Sonarr Title for TVDB ID {tvdbId} is null");
                    return (false, null, string.Empty);
                }

                string germanTitle ;
                bool hasGermanTitle;
                var originalLanguage = (string)shows[0].originalLanguage.name;

                if (originalLanguage != "German")
                {
                    germanTitle = responseData.germanTitle;
                    hasGermanTitle = true;
                }
                else
                {
                    germanTitle = expectedTitle;
                    hasGermanTitle = true;
                }

                var hasGermanUmlaut = germanTitle?.HasGermanUmlauts() ?? false;

                var result = (hasGermanUmlaut, germanTitle, expectedTitle);
                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    SlidingExpiration = hasGermanTitle ? TimeSpan.FromDays(30) : TimeSpan.FromDays(7)
                });

                return result;
            }
            else
            {
                _logger.LogWarning($"UmlautAdaptarr TitleQuery { apiUrl } didn't succeed.");
                return (false, null, string.Empty);
            }
        }

    }
}
