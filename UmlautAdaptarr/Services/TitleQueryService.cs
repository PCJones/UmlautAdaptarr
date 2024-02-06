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

        public TitleQueryService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<TitleQueryService> logger)
        {
            _httpClient = httpClient;
            _cache = memoryCache;
            _logger = logger;

            _sonarrHost = Environment.GetEnvironmentVariable("SONARR_HOST");
            _sonarrApiKey = Environment.GetEnvironmentVariable("SONARR_API_KEY");
        }

        public async Task<(bool hasGermanUmlaut, string? GermanTitle, string ExpectedTitle)> QueryShow(string tvdbId)
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

            var expectedTitle = shows[0].title as string;
            if (expectedTitle == null)
            {
                _logger.LogError($"Sonarr Title for TVDB ID {tvdbId} is null");
                return (false, null, string.Empty);
            }

            string? germanTitle = null;
            var hasGermanTitle = false;

            if ((string)shows[0].originalLanguage.name != "German")
            {
                var thetvdbUrl = $"https://umlautadaptarr.pcjones.de/get_german_title.php?tvdbid={tvdbId}";
                var tvdbResponse = await _httpClient.GetStringAsync(thetvdbUrl);
                var tvdbData = JsonConvert.DeserializeObject<dynamic>(tvdbResponse);

                if (tvdbData == null)
                {
                    _logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response for TVDB ID {tvdbId} resulted in null");
                    return (false, null, string.Empty);
                }

                if (tvdbData.status == "success")
                {
                    germanTitle = tvdbData.germanTitle;
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
                SlidingExpiration = hasGermanTitle ? TimeSpan.FromDays(30) : TimeSpan.FromDays(7)
            });

            return result;
        }

    }
}
