using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Xml.Linq;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Controllers
{
    public abstract class SearchControllerBase(ProxyService proxyService, TitleMatchingService titleMatchingService) : ControllerBase
    {
        private readonly bool TODO_FORCE_TEXT_SEARCH_ORIGINAL_TITLE = false;
        private readonly bool TODO_FORCE_TEXT_SEARCH_GERMAN_TITLE = false;
        protected async Task<IActionResult> BaseSearch(string options,
                                                       string domain,
                                                       IDictionary<string, string> queryParameters,
                                                       SearchItem? searchItem = null)
        {
            try
            {
                if (!UrlUtilities.IsValidDomain(domain))
                {
                    return NotFound($"{domain} is not a valid URL.");
                }

                var initialSearchResult = await PerformSingleSearchRequest(domain, queryParameters) as ContentResult;
                if (initialSearchResult == null)
                {
                    return null;
                }

                string inititalProcessedContent = string.Empty;
                // Rename titles in the single search content
                if (!string.IsNullOrEmpty(initialSearchResult?.Content))
                {
                    inititalProcessedContent = ProcessContent(initialSearchResult.Content, searchItem?.TitleMatchVariations, searchItem?.ExpectedTitle);
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

                    string searchQuery = string.Empty;
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
                    var aggregatedResult = await AggregateSearchResults(domain, queryParameters, titleSearchVariations, searchItem.TitleMatchVariations, expectedTitle);
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
            var responseMessage = await proxyService.ProxyRequestAsync(HttpContext, requestUrl);
            var content = await responseMessage.Content.ReadAsStringAsync();

            var encoding = responseMessage.Content.Headers.ContentType?.CharSet != null ?
                Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet) :
                Encoding.UTF8;
            string contentType = responseMessage.Content.Headers.ContentType?.MediaType ?? "application/xml";
            
            return Content(content, contentType, encoding);
        }


        private string ProcessContent(string content, string[]? titleMatchVariations = null, string? expectedTitle = null)
        {
            return titleMatchingService.RenameTitlesInContent(content, titleMatchVariations, expectedTitle);
        }

        public async Task<AggregatedSearchResult> AggregateSearchResults(
            string domain,
            IDictionary<string, string> queryParameters,
            IEnumerable<string> titleSearchVariations,
            string[] titleMatchVariations,
            string expectedTitle)
        {
            string defaultContentType = "application/xml";
            Encoding defaultEncoding = Encoding.UTF8;
            bool encodingSet = false;

            var aggregatedResult = new AggregatedSearchResult(defaultContentType, defaultEncoding);

            foreach (var titleVariation in titleSearchVariations)
            {
                queryParameters["q"] = titleVariation; // Replace the "q" parameter for each variation
                var requestUrl = UrlUtilities.BuildUrl(domain, queryParameters);
                var responseMessage = await proxyService.ProxyRequestAsync(HttpContext, requestUrl);
                var content = await responseMessage.Content.ReadAsStringAsync();

                // Only update encoding from the first response
                if (!encodingSet && responseMessage.Content.Headers.ContentType?.CharSet != null)
                {
                    aggregatedResult.ContentEncoding = Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet);
                    aggregatedResult.ContentType = responseMessage.Content.Headers.ContentType?.MediaType ?? defaultContentType;
                    encodingSet = true;
                }

                // Process and rename titles in the content
                content = ProcessContent(content, titleMatchVariations, expectedTitle);

                // Aggregate the items into a single document
                aggregatedResult.AggregateItems(content);
            }

            return aggregatedResult;
        }
    }

    public class SearchController(ProxyService proxyService,
                                  TitleMatchingService titleMatchingService,
                                  SearchItemLookupService searchItemLookupService) : SearchControllerBase(proxyService, titleMatchingService)
    {
        [HttpGet]
        public async Task<IActionResult> MovieSearch([FromRoute] string options, [FromRoute] string domain)
        {
            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));
            return await BaseSearch(options, domain, queryParameters);
        }

        [HttpGet]
        public async Task<IActionResult> GenericSearch([FromRoute] string options, [FromRoute] string domain)
        {
            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));
            return await BaseSearch(options, domain, queryParameters);
        }

        [HttpGet]
        public async Task<IActionResult> BookSearch([FromRoute] string options, [FromRoute] string domain)
        {
            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));
            return await BaseSearch(options, domain, queryParameters);
        }

        [HttpGet]
        public async Task<IActionResult> TVSearch([FromRoute] string options, [FromRoute] string domain)
        {
            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));

            SearchItem? searchItem = null;
            string mediaType = "tv";

            if (queryParameters.TryGetValue("tvdbid", out string? tvdbId) && !string.IsNullOrEmpty(tvdbId))
            {
                searchItem = await searchItemLookupService.GetOrFetchSearchItemByExternalId(mediaType, tvdbId);
            }
            else if (queryParameters.TryGetValue("q", out string? title) && !string.IsNullOrEmpty(title))
            {
                searchItem = await searchItemLookupService.GetOrFetchSearchItemByTitle(mediaType, title);
            }

            return await BaseSearch(options, domain, queryParameters, searchItem);
        }

        [HttpGet]
        public async Task<IActionResult> MusicSearch([FromRoute] string options, [FromRoute] string domain)
        {
            var queryParameters = HttpContext.Request.Query.ToDictionary(
                 q => q.Key,
                 q => string.Join(",", q.Value));
            return await BaseSearch(options, domain, queryParameters);
        }
    }
}
