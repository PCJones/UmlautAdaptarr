using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Options.ArrOptions;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Providers
{
    public class ReadarrClient(
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        CacheService cacheService,
        IMemoryCache cache,
        IOptions<ReadarrInstanceOptions> options,
        ILogger<ReadarrClient> logger) : ArrClientBase()
    {

        public ReadarrInstanceOptions ReadarrOptions { get; } = options.Value;
        private readonly string _mediaType = "book";

        public override async Task<IEnumerable<SearchItem>> FetchAllItemsAsync()
        {
            var httpClient = clientFactory.CreateClient();
            var items = new List<SearchItem>();

            try
            {
                var readarrAuthorUrl = $"{ReadarrOptions.Host}/api/v1/author?apikey={ReadarrOptions.ApiKey}";
                logger.LogInformation($"Fetching all authors from Readarr: {UrlUtilities.RedactApiKey(readarrAuthorUrl)}");
                var authorApiResponse = await httpClient.GetStringAsync(readarrAuthorUrl);
                var authors = JsonConvert.DeserializeObject<List<dynamic>>(authorApiResponse);

                if (authors == null)
                {
                    logger.LogError($"Readarr authors API request resulted in null");
                    return items;
                }
                logger.LogInformation($"Successfully fetched {authors.Count} authors from Readarr.");
                foreach (var author in authors)
                {
                    var authorId = (int)author.id;

                    var readarrBookUrl = $"{ReadarrOptions.Host}/api/v1/book?authorId={authorId}&apikey={ReadarrOptions.ApiKey}";

                    // TODO add caching here
                    logger.LogInformation($"Fetching all books from authorId {authorId} from Readarr: {UrlUtilities.RedactApiKey(readarrBookUrl)}");
                    var bookApiResponse = await httpClient.GetStringAsync(readarrBookUrl);
                    var books = JsonConvert.DeserializeObject<List<dynamic>>(bookApiResponse);

                    if (books == null)
                    {
                        logger.LogWarning($"Readarr book API request for authorId {authorId} resulted in null");
                        continue;
                    }

                    logger.LogInformation($"Successfully fetched {books.Count} books for authorId {authorId} from Readarr.");

                    // Cache books for 3 minutes
                    cache.Set(readarrBookUrl, books, TimeSpan.FromMinutes(3));

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
                            arrId: authorId,
                            externalId: externalId,
                            title: bookTitle,
                            expectedTitle: bookTitle,
                            germanTitle: null,
                            aliases: aliases,
                            mediaType: _mediaType,
                            expectedAuthor: authorName
                        );

                        items.Add(searchItem);
                    }
                }

                logger.LogInformation($"Finished fetching all items from Readarr");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching all authors from Readarr: {ex.Message}");
            }

            return items;
        }

        // Logic based on https://github.com/Readarr/Readarr/blob/develop/src/NzbDrone.Core/Parser/Parser.cs#L541
        public static string GetSearchBookTitle(string bookTitle, string authorName)
        {
            // Remove author prefix from title if present, e.g., "Tom Clancy: Ghost Protocol"
            if (!string.IsNullOrEmpty(authorName) && bookTitle.StartsWith($"{authorName}:"))
            {
                bookTitle = bookTitle[(authorName.Length + 1)..].Trim();
            }

            // Remove subtitles or additional info enclosed in parentheses or following a colon, if any
            int firstParenthesisIndex = bookTitle.IndexOf('(');
            int firstColonIndex = bookTitle.IndexOf(':');

            if (firstParenthesisIndex > -1)
            {
                int endParenthesisIndex = bookTitle.IndexOf(')', firstParenthesisIndex);
                if (endParenthesisIndex > -1 && bookTitle.Substring(firstParenthesisIndex + 1, endParenthesisIndex - firstParenthesisIndex - 1).Contains(' '))
                {
                    bookTitle = bookTitle[..firstParenthesisIndex].Trim();
                }
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
                {
                    try
                    {
                        cacheService.CacheSearchItem(searchItem);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"An error occurred while caching search item with ID {searchItem.ArrId}.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error fetching single author from Readarr: {ex.Message}");
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
                logger.LogError($"Error fetching single author from Readarr: {ex.Message}");
            }

            return null;
        }
    }
}
