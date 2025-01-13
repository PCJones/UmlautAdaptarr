using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Xml.Linq;
using UmlautAdaptarr.Options;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Controllers
{
    public class CapsController(ProxyRequestService proxyRequestService, IOptions<GlobalOptions> options, ILogger<CapsController> logger) : ControllerBase
    {
        private readonly ProxyRequestService _proxyRequestService = proxyRequestService;
        private readonly GlobalOptions _options = options.Value;
        private readonly ILogger<CapsController> _logger = logger;


        [HttpGet]
        public async Task<IActionResult> Caps([FromRoute] string apiKey, [FromRoute] string domain, [FromQuery] string? apikey)
        {
            if (_options.ApiKey != null && !apiKey.Equals(apiKey))
            {
                _logger.LogWarning("Invalid or missing API key for request.");
                return Unauthorized("Unauthorized: Invalid or missing API key.");
            }

            if (!domain.StartsWith("localhost") && !UrlUtilities.IsValidDomain(domain))
            {
                return NotFound($"{domain} is not a valid URL.");
            }

            var requestUrl = UrlUtilities.BuildUrl(domain, "caps", apikey);

            var responseMessage = await _proxyRequestService.ProxyRequestAsync(HttpContext, requestUrl);

            var content = await responseMessage.Content.ReadAsStringAsync();
            var encoding = responseMessage.Content.Headers.ContentType?.CharSet != null ?
                    Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet) :
                    Encoding.UTF8;
            var contentType = responseMessage.Content.Headers.ContentType?.MediaType ?? "application/xml";

            return Content(content, contentType, encoding);
        }
    }
}
