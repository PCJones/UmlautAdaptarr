﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Options;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Controllers
{
    public abstract class SearchControllerBase(ProxyRequestService proxyRequestService, TitleMatchingService titleMatchingService, IOptions<GlobalOptions> options, ILogger<SearchControllerBase> logger) : ControllerBase
    {
        // TODO evaluate if this should be set to true by default
        private readonly bool TODO_FORCE_TEXT_SEARCH_ORIGINAL_TITLE = true;
        private readonly bool TODO_FORCE_TEXT_SEARCH_GERMAN_TITLE = false;
        protected async Task<IActionResult> BaseSearch(string apiKey,
                                                       string domain,
                                                       IDictionary<string, string> queryParameters,
                                                       SearchItem? searchItem = null)
        {
            try
            {
                if (!AssureApiKey(apiKey))
                {
                    return Unauthorized("Unauthorized: Invalid or missing API key.");
                }

                if (!UrlUtilities.IsValidDomain(domain))
                {
                    return NotFound($"{domain} is not a valid URL.");
                }

                ContentResult? initialSearchResult = await PerformSingleSearchRequest(domain, queryParameters) as ContentResult;
                if (initialSearchResult == null)
                {
                    return null;
                }

                string inititalProcessedContent = string.Empty;
                // Rename titles in the single search content
                if (!string.IsNullOrEmpty(initialSearchResult?.Content))
                {
                    inititalProcessedContent = ProcessContent(initialSearchResult.Content, searchItem);
                }

                var additionalTextSearch = searchItem != null
                                           && !string.IsNullOrEmpty(searchItem.ExpectedTitle)
                                           && (TODO_FORCE_TEXT_SEARCH_GERMAN_TITLE || TODO_FORCE_TEXT_SEARCH_ORIGINAL_TITLE || 
                                           // TODO check if this is a good idea
                                           (searchItem.TitleSearchVariations.Length > 0 && !(searchItem.TitleSearchVariations.Length == 1 && searchItem.TitleSearchVariations[0] == searchItem.ExpectedTitle)));

                if (additionalTextSearch)
                {
                    // Aggregate the initial search result with additional results
                    // Remove identifiers for subsequent searches
                    // TODO rework this
                    queryParameters.Remove("tvdbid");
                    queryParameters.Remove("tvmazeid");
                    queryParameters.Remove("imdbid");

                    var titleSearchVariations = new List<string>(searchItem?.TitleSearchVariations);

                    var searchQuery = string.Empty;
                    if (queryParameters.TryGetValue("q", out string? q))
                    {
                        searchQuery = q ?? string.Empty;
                        // Add original search query to title variations
                        if (!titleSearchVariations.Remove(searchQuery))
                        {
                            titleSearchVariations.Add(searchQuery);
                        }
                    }

                    var expectedTitle = searchItem.ExpectedTitle;

                    if (TODO_FORCE_TEXT_SEARCH_ORIGINAL_TITLE)
                    {
                        if (expectedTitle != searchQuery && !titleSearchVariations.Contains(expectedTitle))
                        {
                            titleSearchVariations.Add(expectedTitle);
                        }
                    }

                    // Handle multiple search requests based on German title variations
                    var aggregatedResult = await AggregateSearchResults(domain, queryParameters, titleSearchVariations, searchItem);
                    aggregatedResult.AggregateItems(inititalProcessedContent);

                    return Content(aggregatedResult.Content, aggregatedResult.ContentType, aggregatedResult.ContentEncoding);
                }

                initialSearchResult!.Content = inititalProcessedContent;
                return initialSearchResult;
            }
            catch (Exception ex)
            {
                // TODO error logging
                Console.WriteLine(ex.ToString());
                return null;
            }
        }


        private async Task<IActionResult> PerformSingleSearchRequest(string domain, IDictionary<string, string> queryParameters)
        {
            var requestUrl = UrlUtilities.BuildUrl(domain, queryParameters);
            var responseMessage = await proxyRequestService.ProxyRequestAsync(HttpContext, requestUrl);
            var content = await responseMessage.Content.ReadAsStringAsync();

            var encoding = responseMessage.Content.Headers.ContentType?.CharSet != null ?
                Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet) :
                Encoding.UTF8;
            string contentType = responseMessage.Content.Headers.ContentType?.MediaType ?? "application/xml";
            
            return Content(content, contentType, encoding);
        }


        private string ProcessContent(string content, SearchItem? searchItem)
        {
            try
            {
                return titleMatchingService.RenameTitlesInContent(content, searchItem);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error at ProcessContent: {ex.Message}{Environment.NewLine}Content:{Environment.NewLine}{content}");
            }
            return null;
        }

        public async Task<AggregatedSearchResult> AggregateSearchResults(
            string domain,
            IDictionary<string, string> queryParameters,
            IEnumerable<string> titleSearchVariations,
            SearchItem? searchItem
            )
        {
            string defaultContentType = "application/xml";
            Encoding defaultEncoding = Encoding.UTF8;
            bool encodingSet = false;

            var aggregatedResult = new AggregatedSearchResult(defaultContentType, defaultEncoding);

            foreach (var titleVariation in titleSearchVariations)
            {
                queryParameters["q"] = titleVariation; // Replace the "q" parameter for each variation
                var requestUrl = UrlUtilities.BuildUrl(domain, queryParameters);
                var responseMessage = await proxyRequestService.ProxyRequestAsync(HttpContext, requestUrl);
                var content = await responseMessage.Content.ReadAsStringAsync();

                // Only update encoding from the first response
                if (!encodingSet && responseMessage.Content.Headers.ContentType?.CharSet != null)
                {
                    aggregatedResult.ContentEncoding = Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet);
                    aggregatedResult.ContentType = responseMessage.Content.Headers.ContentType?.MediaType ?? defaultContentType;
                    encodingSet = true;
                }

                // Process and rename titles in the content
                content = ProcessContent(content, searchItem);

                // Aggregate the items into a single document
                aggregatedResult.AggregateItems(content);
            }

            return aggregatedResult;
        }

        internal bool AssureApiKey(string apiKey)
        {
            if (!string.IsNullOrEmpty(options.Value.ApiKey) && !apiKey.Equals(options.Value.ApiKey))
            {
                logger.LogWarning("Invalid or missing API key for request.");
                return false;
            }
            return true;
        }
    }

    public class SearchController(ProxyRequestService proxyRequestService,
                                  TitleMatchingService titleMatchingService,
                                  SearchItemLookupService searchItemLookupService,
                                  IOptions<GlobalOptions> options,
                                  ILogger<SearchControllerBase> logger) : SearchControllerBase(proxyRequestService, titleMatchingService, options, logger)
    {
        public readonly string[] LIDARR_CATEGORY_IDS = ["3000", "3010", "3020", "3040", "3050"];
        public readonly string[] READARR_CATEGORY_IDS = ["3030", "3130", "7000", "7010", "7020", "7030", "7100", "7110", "7120", "7130"];

        [HttpGet]
        public async Task<IActionResult> MovieSearch([FromRoute] string apiKey, [FromRoute] string domain)
        {
            if (!AssureApiKey(apiKey))
            {
                return Unauthorized("Unauthorized: Invalid or missing API key.");
            }

            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));
            return await BaseSearch(apiKey, domain, queryParameters);
        }

        [HttpGet]
        public async Task<IActionResult> GenericSearch([FromRoute] string apiKey, [FromRoute] string domain)
        {
            if (!AssureApiKey(apiKey))
            {
                return Unauthorized("Unauthorized: Invalid or missing API key.");
            }

            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));

            SearchItem? searchItem = null;

            if (queryParameters.TryGetValue("q", out string? title) && !string.IsNullOrEmpty(title))
            {
                if (queryParameters.TryGetValue("cat", out string? categories) && !string.IsNullOrEmpty(categories))
                {
                    // look for (audio-)book
                    if (categories.Split(',').Any(category => READARR_CATEGORY_IDS.Contains(category)))
                    {
                        var mediaType = "book";
                        searchItem = await searchItemLookupService.GetOrFetchSearchItemByExternalId(mediaType, title.GetReadarrTitleForExternalId());
                    }

                    // look for audio (lidarr)
                    if (searchItem == null && categories.Split(',').Any(category => LIDARR_CATEGORY_IDS.Contains(category)))
                    {
                        var mediaType = "audio";
                        searchItem = await searchItemLookupService.GetOrFetchSearchItemByExternalId(mediaType, title.GetLidarrTitleForExternalId());
                    }
                }
            }

            return await BaseSearch(apiKey, domain, queryParameters, searchItem);
        }

        [HttpGet]
        public async Task<IActionResult> BookSearch([FromRoute] string apiKey, [FromRoute] string domain)
        {
            if (!AssureApiKey(apiKey))
            {
                return Unauthorized("Unauthorized: Invalid or missing API key.");
            }

            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));
            return await BaseSearch(apiKey, domain, queryParameters);
        }

        [HttpGet]
        public async Task<IActionResult> TVSearch([FromRoute] string apiKey, [FromRoute] string domain)
        {
            if (!AssureApiKey(apiKey))
            {
               return Unauthorized("Unauthorized: Invalid or missing API key.");
            }

            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));

            SearchItem? searchItem = null;
            var mediaType = "tv";

            if (queryParameters.TryGetValue("tvdbid", out string? tvdbId) && !string.IsNullOrEmpty(tvdbId))
            {
                searchItem = await searchItemLookupService.GetOrFetchSearchItemByExternalId(mediaType, tvdbId);
            }
            else if (queryParameters.TryGetValue("q", out string? title) && !string.IsNullOrEmpty(title))
            {
                searchItem = await searchItemLookupService.GetOrFetchSearchItemByTitle(mediaType, title);
            }

            return await BaseSearch(apiKey, domain, queryParameters, searchItem);
        }

        [HttpGet]
        public async Task<IActionResult> MusicSearch([FromRoute] string apiKey, [FromRoute] string domain)
        {
            if (!AssureApiKey(apiKey))
            {
                return Unauthorized("Unauthorized: Invalid or missing API key.");
            }

            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));
            return await BaseSearch(apiKey, domain, queryParameters);
        }
    }
}
