using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml.Linq;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Controllers
{
    public abstract class SearchControllerBase(ProxyService proxyService, TitleMatchingService titleMatchingService) : ControllerBase
    {
        protected readonly ProxyService _proxyService = proxyService;
        protected readonly TitleMatchingService _titleMatchingService = titleMatchingService;

        protected async Task<IActionResult> BaseSearch(string options,
                                                       string domain,
                                                       IDictionary<string, string> queryParameters,
                                                       string? germanTitle = null,
                                                       string? expectedTitle = null,
                                                       bool hasGermanUmlaut = false)
        {
            try
            {
                if (!UrlUtilities.IsValidDomain(domain))
                {
                    return NotFound($"{domain} is not a valid URL.");
                }

                // Generate title variations for renaming process
                var germanTitleVariations = !string.IsNullOrEmpty(germanTitle) ? _titleMatchingService.GenerateTitleVariations(germanTitle) : new List<string>();

                // Check if "q" parameter exists for multiple search request handling
                if (hasGermanUmlaut && !string.IsNullOrEmpty(germanTitle) && !string.IsNullOrEmpty(expectedTitle) && queryParameters.ContainsKey("q"))
                {
                    // Add original search query to title variations
                    var q = queryParameters["q"];
                    if (!germanTitleVariations.Contains(q))
                    {
                        germanTitleVariations.Add(queryParameters["q"]!);
                    }

                    // Handle multiple search requests based on German title variations
                    var aggregatedResult = await AggregateSearchResults(domain, queryParameters, germanTitleVariations, expectedTitle);
                    // Rename titles in the aggregated content
                    var processedContent = ProcessContent(aggregatedResult.Content, germanTitleVariations, expectedTitle);
                    return Content(processedContent, aggregatedResult.ContentType, aggregatedResult.ContentEncoding);
                }
                else
                {
                    var singleSearchResult = await PerformSingleSearchRequest(domain, queryParameters);
                    // Rename titles in the single search content
                    var contentResult = singleSearchResult as ContentResult;
                    if (contentResult != null)
                    {
                        var processedContent = ProcessContent(contentResult.Content ?? "", germanTitleVariations, expectedTitle);
                        return Content(processedContent, contentResult.ContentType!, Encoding.UTF8);
                    }
                    return singleSearchResult;
                }
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
            var responseMessage = await _proxyService.ProxyRequestAsync(HttpContext, requestUrl);
            var content = await responseMessage.Content.ReadAsStringAsync();

            var encoding = responseMessage.Content.Headers.ContentType?.CharSet != null ?
                Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet) :
                Encoding.UTF8;
            string contentType = responseMessage.Content.Headers.ContentType?.MediaType ?? "application/xml";
            
            return Content(content, contentType, encoding);
        }


        private string ProcessContent(string content, List<string> germanTitleVariations, string? expectedTitle)
        {
            // Check if German title and expected title are provided for renaming
            if (!string.IsNullOrEmpty(expectedTitle) && germanTitleVariations.Count != 0)
            {
                // Process and rename titles in the content
                content = _titleMatchingService.RenameTitlesInContent(content, germanTitleVariations, expectedTitle);
            }
            return content;
        }

        public async Task<AggregatedSearchResult> AggregateSearchResults(string domain, IDictionary<string, string> queryParameters, List<string> germanTitleVariations, string expectedTitle)
        {
            string defaultContentType = "application/xml";
            Encoding defaultEncoding = Encoding.UTF8;
            bool encodingSet = false;

            var aggregatedResult = new AggregatedSearchResult(defaultContentType, defaultEncoding);

            foreach (var titleVariation in germanTitleVariations.Distinct())
            {
                queryParameters["q"] = titleVariation; // Replace the "q" parameter for each variation
                var requestUrl = UrlUtilities.BuildUrl(domain, queryParameters);
                var responseMessage = await _proxyService.ProxyRequestAsync(HttpContext, requestUrl);
                var content = await responseMessage.Content.ReadAsStringAsync();

                // Only update encoding from the first response
                if (!encodingSet && responseMessage.Content.Headers.ContentType?.CharSet != null)
                {
                    aggregatedResult.ContentEncoding = Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet); ;
                    aggregatedResult.ContentType = responseMessage.Content.Headers.ContentType?.MediaType ?? defaultContentType;
                    encodingSet = true;
                }

                // Process and rename titles in the content
                content = _titleMatchingService.RenameTitlesInContent(content, germanTitleVariations, expectedTitle);

                // Aggregate the items into a single document
                aggregatedResult.AggregateItems(content);
            }

            return aggregatedResult;
        }
    }

    public class SearchController(ProxyService proxyService,
                                  TitleMatchingService titleMatchingService,
                                  TitleQueryService titleQueryService) : SearchControllerBase(proxyService, titleMatchingService)
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

            string? searchKey = null;
            string? searchValue = null;

            if (queryParameters.TryGetValue("tvdbid", out string? tvdbId))
            {
                searchKey = "tvdbid";
                searchValue = tvdbId;
            }
            else if (queryParameters.TryGetValue("q", out string? title))
            {
                searchKey = "q";
                searchValue = title;
            }

            // Perform the search if a valid search key was identified
            if (searchKey != null && searchValue != null)
            {
                var (hasGermanUmlaut, germanTitle, expectedTitle) = searchKey == "tvdbid"
                    ? await titleQueryService.QueryGermanShowTitleByTVDBId(searchValue)
                    : await titleQueryService.QueryGermanShowTitleByTitle(searchValue);

                if (!string.IsNullOrEmpty(germanTitle) && !string.IsNullOrEmpty(expectedTitle))
                {
                    var initialSearchResult = await BaseSearch(options, domain, queryParameters, germanTitle, expectedTitle, hasGermanUmlaut);

                    // Additional search with german title because the automatic tvdbid association often fails at the indexer too if there are umlauts
                    if (hasGermanUmlaut && searchKey == "tvdbid")
                    {
                        // Remove identifiers for subsequent searches
                        queryParameters.Remove("tvdbid");
                        queryParameters.Remove("tvmazeid");
                        queryParameters.Remove("imdbid");

                        // Aggregate the initial search result with additional results
                        var germanTitleVariations = _titleMatchingService.GenerateTitleVariations(germanTitle);
                        var aggregatedResult = await AggregateSearchResults(domain, queryParameters, germanTitleVariations, expectedTitle);
                        // todo processedContent wie in BaseSearch


                        aggregatedResult.AggregateItems((initialSearchResult as ContentResult)?.Content ?? "");

                        return Content(aggregatedResult.Content, aggregatedResult.ContentType, aggregatedResult.ContentEncoding);
                    }
                    return initialSearchResult;
                }
            }

            return await BaseSearch(options, domain, queryParameters);
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
