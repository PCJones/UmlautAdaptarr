using Microsoft.AspNetCore.Mvc;
using System.Text;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Controllers
{
    public abstract class SearchControllerBase(ProxyService proxyService) : ControllerBase
    {
        protected readonly ProxyService _proxyService = proxyService;

        protected async Task<IActionResult> BaseSearch(string options, string domain, IDictionary<string, string> queryParameters)
        {
            if (!UrlUtilities.IsValidDomain(domain))
            {
                return NotFound($"{domain} is not a valid URL.");
            }

            var requestUrl = UrlUtilities.BuildUrl(domain, queryParameters);

            var responseMessage = await _proxyService.ProxyRequestAsync(HttpContext, requestUrl);

            var content = await responseMessage.Content.ReadAsStringAsync();
            var encoding = responseMessage.Content.Headers.ContentType?.CharSet != null ?
                Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet) :
                Encoding.UTF8;
            var contentType = responseMessage.Content.Headers.ContentType?.MediaType ?? "application/xml";

            return Content(content, contentType, encoding);
        }
    }

    public class SearchController(ProxyService proxyService, TitleQueryService titleQueryService) : SearchControllerBase(proxyService)
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


            if (queryParameters.TryGetValue("tvdbid", out string tvdbId))
            {
                var (HasGermanUmlaut, GermanTitle, ExpectedTitle) = await titleQueryService.QueryShow(tvdbId);

                if (GermanTitle == null && ExpectedTitle == null)
                {
                    return NotFound($"Show with TVDB ID {tvdbId} not found.");
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
