using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Options.ArrOptions.InstanceOptions;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Providers;

public class SonarrClient : ArrClientBase
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<SonarrClient> _logger;

    private readonly string _mediaType = "tv";
    private readonly TitleApiService _titleService;


    public SonarrClient([ServiceKey] string instanceName,
        IHttpClientFactory clientFactory,
        TitleApiService titleService,
        IOptionsMonitor<SonarrInstanceOptions> options,
        ILogger<SonarrClient> logger)
    {
        _clientFactory = clientFactory;
        _titleService = titleService;
        _logger = logger;

        InstanceName = instanceName;
        Options = options.Get(InstanceName);
        _logger.LogInformation($"Init SonarrClient ({InstanceName})");
    }

    public SonarrInstanceOptions Options { get; init; }

    public override async Task<IEnumerable<SearchItem>> FetchAllItemsAsync()
    {
        var httpClient = _clientFactory.CreateClient();
        var items = new List<SearchItem>();

        try
        {
            var sonarrUrl = $"{Options.Host}/api/v3/series?includeSeasonImages=false&apikey={Options.ApiKey}";
            _logger.LogInformation($"Fetching all items from Sonarr: {UrlUtilities.RedactApiKey(sonarrUrl)}");
            var response = await httpClient.GetStringAsync(sonarrUrl);
            var shows = JsonConvert.DeserializeObject<List<dynamic>>(response);

            if (shows != null)
            {
                _logger.LogInformation($"Successfully fetched {shows.Count} items from Sonarr ({InstanceName}).");
                foreach (var show in shows)
                {
                    var tvdbId = (string)show.tvdbId;
                    if (tvdbId == null)
                    {
                        _logger.LogWarning($"Sonarr ({InstanceName}) Show {show.id} doesn't have a tvdbId.");
                        continue;
                    }

                    var (germanTitle, aliases) =
                        await _titleService.FetchGermanTitleAndAliasesByExternalIdAsync(_mediaType, tvdbId);
                    var searchItem = new SearchItem
                    (
                        (int)show.id,
                        tvdbId,
                        (string)show.title,
                        (string)show.title,
                        germanTitle,
                        aliases: aliases,
                        mediaType: _mediaType
                    );

                    items.Add(searchItem);
                }
            }

            _logger.LogInformation($"Finished fetching all items from Sonarr ({InstanceName})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching all shows from Sonarr ({InstanceName}) : {ex.Message}");
        }

        return items;
    }

    public override async Task<SearchItem?> FetchItemByExternalIdAsync(string externalId)
    {
        var httpClient = _clientFactory.CreateClient();

        try
        {
            var sonarrUrl =
                $"{Options.Host}/api/v3/series?tvdbId={externalId}&includeSeasonImages=false&apikey={Options.ApiKey}";
            _logger.LogInformation(
                $"Fetching item by external ID from Sonarr ({InstanceName}): {UrlUtilities.RedactApiKey(sonarrUrl)}");
            var response = await httpClient.GetStringAsync(sonarrUrl);
            var shows = JsonConvert.DeserializeObject<dynamic>(response);
            var show = shows?[0];

            if (show != null)
            {
                var tvdbId = (string)show.tvdbId;
                if (tvdbId == null)
                {
                    _logger.LogWarning($"Sonarr ({InstanceName}) Show {show.id} doesn't have a tvdbId.");
                    return null;
                }

                var (germanTitle, aliases) =
                    await _titleService.FetchGermanTitleAndAliasesByExternalIdAsync(_mediaType, tvdbId);

                var searchItem = new SearchItem
                (
                    (int)show.id,
                    tvdbId,
                    (string)show.title,
                    (string)show.title,
                    germanTitle,
                    aliases: aliases,
                    mediaType: _mediaType
                );

                _logger.LogInformation($"Successfully fetched show {searchItem.Title} from Sonarr ({InstanceName}).");
                return searchItem;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching single show from Sonarr ({InstanceName}): {ex.Message}");
        }

        return null;
    }

    public override async Task<SearchItem?> FetchItemByTitleAsync(string title)
    {
        var httpClient = _clientFactory.CreateClient();

        try
        {
            var (germanTitle, tvdbId, aliases) =
                await _titleService.FetchGermanTitleAndExternalIdAndAliasesByTitle(_mediaType, title);

            if (tvdbId == null) return null;

            var sonarrUrl =
                $"{Options.Host}/api/v3/series?tvdbId={tvdbId}&includeSeasonImages=false&apikey={Options.ApiKey}";
            var sonarrApiResponse = await httpClient.GetStringAsync(sonarrUrl);
            var shows = JsonConvert.DeserializeObject<dynamic>(sonarrApiResponse);

            if (shows == null)
            {
                _logger.LogError($"Parsing Sonarr ({InstanceName}) API response for TVDB ID {tvdbId} resulted in null");
                return null;
            }

            if (shows.Count == 0)
            {
                _logger.LogWarning($"No results found for TVDB ID {tvdbId}");
                return null;
            }

            var expectedTitle = (string)shows[0].title;
            if (expectedTitle == null)
            {
                _logger.LogError($"Sonarr ({InstanceName}) : Title for TVDB ID {tvdbId} is null");
                return null;
            }

            var searchItem = new SearchItem
            (
                (int)shows[0].id,
                tvdbId,
                (string)shows[0].title,
                (string)shows[0].title,
                germanTitle,
                aliases: aliases,
                mediaType: _mediaType
            );

            _logger.LogInformation($"Successfully fetched show {searchItem.Title} from Sonarr ({InstanceName}).");
            return searchItem;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching single show from Sonarr ({InstanceName}) : {ex.Message}");
        }

        return null;
    }
}