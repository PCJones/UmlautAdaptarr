using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml.Linq;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Controllers
{
    public class CapsController(ProxyService proxyService) : ControllerBase
    {
        private readonly ProxyService _proxyService = proxyService;

        [HttpGet]
        public async Task<IActionResult> Caps([FromRoute] string options, [FromRoute] string domain, [FromQuery] string? apikey)
        {
            if (!UrlUtilities.IsValidDomain(domain))
            {
                return NotFound($"{domain} is not a valid URL.");
            }

            var requestUrl = UrlUtilities.BuildUrl(domain, "caps", apikey);

            var responseMessage = await _proxyService.ProxyRequestAsync(HttpContext, requestUrl);

            var content = await responseMessage.Content.ReadAsStringAsync();
            var encoding = responseMessage.Content.Headers.ContentType?.CharSet != null ?
                    Encoding.GetEncoding(responseMessage.Content.Headers.ContentType.CharSet) :
                    Encoding.UTF8;
            var contentType = responseMessage.Content.Headers.ContentType?.MediaType ?? "application/xml";

            return Content(content, contentType, encoding);
        }
    }
}
