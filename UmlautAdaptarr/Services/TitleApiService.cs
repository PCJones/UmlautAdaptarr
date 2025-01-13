using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using UmlautAdaptarr.Options;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public class TitleApiService(IHttpClientFactory clientFactory, ILogger<TitleApiService> logger, IOptions<GlobalOptions> options)
    {
        public GlobalOptions Options { get; } = options.Value;

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

        // TODO add caching
        public async Task<(string? germanTitle, string[]? aliases)> FetchGermanTitleAndAliasesByExternalIdAsync(string mediaType, string externalId)
        {
            try
            {
                await EnsureMinimumDelayAsync();

                var httpClient = clientFactory.CreateClient();
                var titleApiUrl = $"{Options.UmlautAdaptarrApiHost}/tvshow_german.php?tvdbid={externalId}";
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

        public async Task<Dictionary<string, (string? germanTitle, string[]? aliases)>> FetchGermanTitlesAndAliasesByExternalIdBulkAsync(IEnumerable<string> tvdbIds)
        {
            try
            {
                await EnsureMinimumDelayAsync();

                var httpClient = clientFactory.CreateClient();
                var bulkApiUrl = $"{Options.UmlautAdaptarrApiHost}/tvshow_german.php?bulk=true";
                logger.LogInformation($"TitleApiService POST {UrlUtilities.RedactApiKey(bulkApiUrl)}");

                // Prepare POST request payload
                var payload = new { tvdbIds = tvdbIds.ToArray() };
                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Send POST request
                var response = await httpClient.PostAsync(bulkApiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError($"Failed to fetch German titles via bulk API. Status Code: {response.StatusCode}");
                    return [];
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var bulkApiResponseData = JsonConvert.DeserializeObject<dynamic>(responseContent);

                if (bulkApiResponseData == null || bulkApiResponseData.status != "success")
                {
                    logger.LogError($"Parsing UmlautAdaptarr Bulk API response resulted in null or an error status.");
                    return [];
                }

                // Process response data
                var results = new Dictionary<string, (string? germanTitle, string[]? aliases)>();
                foreach (var entry in bulkApiResponseData.data)
                {
                    string tvdbId = entry.tvdbId;
                    string? germanTitle = entry.germanTitle;

                    string[]? aliases = null;
                    if (entry.aliases != null)
                    {
                        JArray aliasesArray = JArray.FromObject(entry.aliases);
                        aliases = aliasesArray.Children<JObject>()
                            .Select(alias => alias["name"].ToString())
                            .ToArray();
                    }

                    results[tvdbId] = (germanTitle, aliases);
                }

                logger.LogInformation($"Successfully fetched German titles for {results.Count} TVDB IDs via bulk API.");

                return results;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching German titles in bulk: {ex.Message}");
                return new Dictionary<string, (string? germanTitle, string[]? aliases)>();
            }
        }

        public async Task<(string? germanTitle, string? externalId, string[]? aliases)> FetchGermanTitleAndExternalIdAndAliasesByTitle(string mediaType, string title)
        {
            try
            {
                await EnsureMinimumDelayAsync();

                var httpClient = clientFactory.CreateClient();
                var tvdbCleanTitle = title.Replace("ß", "ss");
                var titleApiUrl = $"{Options.UmlautAdaptarrApiHost}/tvshow_german.php?title={tvdbCleanTitle}";
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
