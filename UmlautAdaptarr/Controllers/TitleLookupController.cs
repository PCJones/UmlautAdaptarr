using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UmlautAdaptarr.Options;
using UmlautAdaptarr.Services;

namespace UmlautAdaptarr.Controllers
{
	[ApiController]
	[Route("titlelookup/")]
	public class TitleLookupController(CacheService cacheService, IOptions<GlobalOptions> options) : ControllerBase
	{
		GlobalOptions _options = options.Value;
		[HttpGet]
		public IActionResult GetOriginalTitle([FromQuery] string changedTitle)
		{
			if (!_options.EnableChangedTitleCache)
			{
				return StatusCode(501, "Set SETTINGS__EnableChangedTitleCache to true to use this endpoint.");
			}

			if (string.IsNullOrWhiteSpace(changedTitle))
				return BadRequest("changedTitle is required.");

			var originalTitle = cacheService.GetOriginalTitleFromRenamed(changedTitle);

			return originalTitle != null
				? Ok(new { changedTitle, originalTitle })
				: NotFound($"Original title not found for '{changedTitle}'.");
		}
	}

}
