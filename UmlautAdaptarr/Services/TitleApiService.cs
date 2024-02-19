using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public class TitleApiService(IHttpClientFactory clientFactory, IConfiguration configuration, ILogger<TitleApiService> logger)
    {
        private readonly string _umlautAdaptarrApiHost = configuration["Settings:UmlautAdaptarrApiHost"]
                                                         ?? throw new ArgumentException("UmlautAdaptarrApiHost must be set in appsettings.json");
        private DateTime lastRequestTime = DateTime.MinValue;

        private async Task EnsureMinimumDelayAsync()
        {
            var sinceLastRequest = DateTime.Now - lastRequestTime;
            if (sinceLastRequest < TimeSpan.FromSeconds(1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - sinceLastRequest);
            }
            lastRequestTime = DateTime.Now;
        }

        // TODO add cache, TODO add bulk request
        public async Task<(string? germanTitle, string[]? aliases)> FetchGermanTitleAndAliasesByExternalIdAsync(string mediaType, string externalId)
        {
            try
            {
                await EnsureMinimumDelayAsync();

                var httpClient = clientFactory.CreateClient();
                var titleApiUrl = $"{_umlautAdaptarrApiHost}/tvshow_german.php?tvdbid={externalId}";
                logger.LogInformation($"TitleApiService GET {UrlUtilities.RedactApiKey(titleApiUrl)}");
                var response = await httpClient.GetStringAsync(titleApiUrl);
                var titleApiResponseData = JsonConvert.DeserializeObject<dynamic>(response);

                if (titleApiResponseData == null)
                {
                    logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response for mediaType {mediaType} with external id {externalId} resulted in null");
                    return (null, null);
                }

                if (titleApiResponseData.status == "success" && !string.IsNullOrEmpty((string)titleApiResponseData.germanTitle))
                {
                    // TODO add filter for german aliases only in API
                    // then also add if there is a "deu" alias to search for it via text
                    string[]? aliases = null;

                    if (titleApiResponseData.aliases != null)
                    {
                        // Parse the aliases as a JArray
                        JArray aliasesArray = JArray.FromObject(titleApiResponseData.aliases);

                        // Project the 'name' field from each object in the array
                        aliases = aliasesArray.Children<JObject>()
                            .Select(alias => alias["name"].ToString())
                            .ToArray();
                    }
                    return (titleApiResponseData.germanTitle, aliases);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching German title for TVDB ID {externalId}: {ex.Message}");
            }

            return (null, null);
        }

        public async Task<(string? germanTitle, string? externalId, string[]? aliases)> FetchGermanTitleAndExternalIdAndAliasesByTitle(string mediaType, string title)
        {
            try
            {
                await EnsureMinimumDelayAsync();

                var httpClient = clientFactory.CreateClient();
                var tvdbCleanTitle = title.Replace("ß", "ss");
                var titleApiUrl = $"{_umlautAdaptarrApiHost}/tvshow_german.php?title={tvdbCleanTitle}";
                logger.LogInformation($"TitleApiService GET {UrlUtilities.RedactApiKey(titleApiUrl)}");
                var titleApiResponse = await httpClient.GetStringAsync(titleApiUrl);
                var titleApiResponseData = JsonConvert.DeserializeObject<dynamic>(titleApiResponse);

                if (titleApiResponseData == null)
                {
                    logger.LogError($"Parsing UmlautAdaptarr TitleQuery API response for title {title} resulted in null");
                    return (null, null, null);
                }

                if (titleApiResponseData.status == "success" && !string.IsNullOrEmpty((string)titleApiResponseData.germanTitle))
                {
                    string[] aliases = titleApiResponseData.aliases.ToObject<string[]>();
                    return (titleApiResponseData.germanTitle, titleApiResponseData.tvdbId, aliases);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching German title for {mediaType} with title {title}: {ex.Message}");
            }

            return (null, null, null);
        }
    }
}
