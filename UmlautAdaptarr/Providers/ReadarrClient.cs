using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Options.ArrOptions.InstanceOptions;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Providers;

public class ReadarrClient : ArrClientBase
{
    private readonly IMemoryCache _cache;
    private readonly CacheService _cacheService;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<ReadarrClient> _logger;
    private readonly string _mediaType = "book";

    public ReadarrClient([ServiceKey] string instanceName, IHttpClientFactory clientFactory,
        CacheService cacheService,
        IMemoryCache cache,
        IOptionsMonitor<ReadarrInstanceOptions> options,
        ILogger<ReadarrClient> logger)
    {
        _clientFactory = clientFactory;
        _cacheService = cacheService;
        _cache = cache;
        _logger = logger;
        InstanceName = instanceName;
        Options = options.Get(InstanceName);
        _logger.LogInformation($"Init ReadarrClient ({InstanceName})");
    }

    public ReadarrInstanceOptions Options { get; init; }

    public override async Task<IEnumerable<SearchItem>> FetchAllItemsAsync()
    {
        var httpClient = _clientFactory.CreateClient();
        var items = new List<SearchItem>();

        try
        {
            var readarrAuthorUrl = $"{Options.Host}/api/v1/author?apikey={Options.ApiKey}";
            _logger.LogInformation(
                $"Fetching all authors from Readarr ({InstanceName}) : {UrlUtilities.RedactApiKey(readarrAuthorUrl)}");
            var authorApiResponse = await httpClient.GetStringAsync(readarrAuthorUrl);
            var authors = JsonConvert.DeserializeObject<List<dynamic>>(authorApiResponse);

            if (authors == null)
            {
                _logger.LogError($"Readarr ({InstanceName}) authors API request resulted in null");
                return items;
            }

            _logger.LogInformation($"Successfully fetched {authors.Count} authors from Readarr ({InstanceName}).");
            foreach (var author in authors)
            {
                var authorId = (int)author.id;

                var readarrBookUrl = $"{Options.Host}/api/v1/book?authorId={authorId}&apikey={Options.ApiKey}";

                // TODO add caching here
                _logger.LogInformation(
                    $"Fetching all books from authorId {authorId} from Readarr: {UrlUtilities.RedactApiKey(readarrBookUrl)}");
                var bookApiResponse = await httpClient.GetStringAsync(readarrBookUrl);
                var books = JsonConvert.DeserializeObject<List<dynamic>>(bookApiResponse);

                if (books == null)
                {
                    _logger.LogWarning(
                        $"Readarr ({InstanceName}) book API request for authorId {authorId} resulted in null");
                    continue;
                }

                _logger.LogInformation(
                    $"Successfully fetched {books.Count} books for authorId {authorId} from Readarr ({InstanceName}) .");

                // Cache books for 3 minutes
                _cache.Set(readarrBookUrl, books, TimeSpan.FromMinutes(3));

                foreach (var book in books)
                {
                    var authorName = (string)author.authorName;
                    var bookTitle = GetSearchBookTitle((string)book.title, authorName);

                    var expectedTitle = $"{bookTitle} {authorName}";

                    string[]? aliases = null;

                    // Abuse externalId to set the search string Readarr uses
                    // TODO use own method or rename
                    var externalId = expectedTitle.GetReadarrTitleForExternalId();

                    var searchItem = new SearchItem
                    (
                        authorId,
                        externalId,
                        bookTitle,
                        bookTitle,
                        null,
                        aliases: aliases,
                        mediaType: _mediaType,
                        expectedAuthor: authorName
                    );

                    items.Add(searchItem);
                }
            }

            _logger.LogInformation($"Finished fetching all items from Readarr ({InstanceName})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching all authors from Readarr ({InstanceName}): {ex.Message}");
        }

        return items;
    }

    // Logic based on https://github.com/Readarr/Readarr/blob/develop/src/NzbDrone.Core/Parser/Parser.cs#L541
    public static string GetSearchBookTitle(string bookTitle, string authorName)
    {
        // Remove author prefix from title if present, e.g., "Tom Clancy: Ghost Protocol"
        if (!string.IsNullOrEmpty(authorName) && bookTitle.StartsWith($"{authorName}:"))
            bookTitle = bookTitle[(authorName.Length + 1)..].Trim();

        // Remove subtitles or additional info enclosed in parentheses or following a colon, if any
        var firstParenthesisIndex = bookTitle.IndexOf('(');
        var firstColonIndex = bookTitle.IndexOf(':');

        if (firstParenthesisIndex > -1)
        {
            var endParenthesisIndex = bookTitle.IndexOf(')', firstParenthesisIndex);
            if (endParenthesisIndex > -1 && bookTitle
                    .Substring(firstParenthesisIndex + 1, endParenthesisIndex - firstParenthesisIndex - 1)
                    .Contains(' ')) bookTitle = bookTitle[..firstParenthesisIndex].Trim();
        }
        else if (firstColonIndex > -1)
        {
            bookTitle = bookTitle[..firstColonIndex].Trim();
        }

        return bookTitle;
    }


    public override async Task<SearchItem?> FetchItemByExternalIdAsync(string externalId)
    {
        try
        {
            // For now we have to fetch all items every time
            // TODO if possible look at the author in search query and only update for author
            var searchItems = await FetchAllItemsAsync();
            foreach (var searchItem in searchItems ?? [])
                try
                {
                    _cacheService.CacheSearchItem(searchItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"An error occurred while caching search item with ID {searchItem.ArrId}.");
                }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching single author from Readarr ({InstanceName}) : {ex.Message}");
        }

        return null;
    }

    public override async Task<SearchItem?> FetchItemByTitleAsync(string title)
    {
        try
        {
            // this should never be called at the moment
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching single author from Readarr ({InstanceName}) : {ex.Message}");
        }

        return null;
    }
}