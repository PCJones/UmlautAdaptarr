using System.Text.RegularExpressions;
using System.Xml.Linq;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public partial class TitleMatchingService(CacheService cacheService, ILogger<TitleMatchingService> logger)
    {
        public string RenameTitlesInContent(string content, string[]? titleMatchVariations, string? expectedTitle)
        {
            var xDoc = XDocument.Parse(content);

            // If expectedTitle and titleMatchVariations are provided use them, if not use the CacheService to find matches.
            bool useCacheService = string.IsNullOrEmpty(expectedTitle) || titleMatchVariations?.Length == 0;

            foreach (var item in xDoc.Descendants("item"))
            {
                var titleElement = item.Element("title");
                if (titleElement != null)
                {
                    var originalTitle = titleElement.Value;
                    var normalizedOriginalTitle = NormalizeTitle(originalTitle);

                    if (useCacheService)
                    {
                        var categoryElement = item.Element("category");
                        var category = categoryElement?.Value;
                        var mediaType = GetMediaTypeFromCategory(category);
                        if (mediaType == null)
                        {
                            continue;
                        }

                        // Use CacheService to find a matching SearchItem by title
                        var searchItem = cacheService.SearchItemByTitle(mediaType, normalizedOriginalTitle);
                        if (searchItem != null)
                        {
                            // If a SearchItem is found, use its ExpectedTitle and titleMatchVariations for renaming
                            expectedTitle = searchItem.ExpectedTitle;
                            titleMatchVariations = searchItem.TitleMatchVariations;
                        }
                        else
                        {
                            // Skip processing this item if no matching SearchItem is found
                            continue;
                        }
                    }

                    // Attempt to find a variation that matches the start of the original title
                    foreach (var variation in titleMatchVariations!)
                    {
                        // Skip variations that are already the expectedTitle
                        if (variation == expectedTitle)
                        {
                            continue;
                        }

                        // Variation is already normalized at creation
                        var pattern = "^" + Regex.Escape(variation).Replace("\\ ", "[._ ]");

                        // Check if the originalTitle starts with the variation (ignoring case and separators)
                        if (Regex.IsMatch(normalizedOriginalTitle, pattern, RegexOptions.IgnoreCase))
                        {
                            // Find the first separator used in the original title for consistent replacement
                            var separator = FindFirstSeparator(originalTitle);
                            // Reconstruct the expected title using the original separator
                            var newTitlePrefix = expectedTitle!.Replace(" ", separator.ToString());

                            // Extract the suffix from the original title starting right after the matched variation length
                            var variationLength = variation.Length;
                            var suffix = originalTitle[Math.Min(variationLength, originalTitle.Length)..];

                            // Clean up any leading separators from the suffix
                            suffix = Regex.Replace(suffix, "^[._ ]+", "");

                            // TODO EVALUTE! definitely make this optional - this adds GERMAN to the title is the title is german to make sure it's recognized as german
                            // can lead to problems with shows such as "dark" that have international dubs
                            /*
                            // Check if "german" is not in the original title, ignoring case
                            if (!Regex.IsMatch(originalTitle, "german", RegexOptions.IgnoreCase))
                            {
                                // Insert "GERMAN" after the newTitlePrefix
                                newTitlePrefix += separator + "GERMAN";
                            }
                            */

                            // Construct the new title with the original suffix
                            var newTitle = newTitlePrefix + (string.IsNullOrEmpty(suffix) ? "" : separator + suffix);

                            // Update the title element's value with the new title
                            //titleElement.Value = newTitle + $"({originalTitle.Substring(0, variationLength)})";
                            titleElement.Value = newTitle;

                            logger.LogInformation($"TitleMatchingService - Title changed: '{originalTitle}' to '{newTitle}'");
                            break; // Break after the first successful match and modification
                        }
                    }
                }
            }

            return xDoc.ToString();
        }


        private static string NormalizeTitle(string title)
        {
            title = title.RemoveAccentButKeepGermanUmlauts();
            // Replace all known separators with space for normalization
            return WordSeperationCharRegex().Replace(title, " ".ToString());
        }

        private static char FindFirstSeparator(string title)
        {
            var match = WordSeperationCharRegex().Match(title);
            return match.Success ? match.Value.First() : ' '; // Default to space if no separator found
        }

        private static string ReconstructTitleWithSeparator(string title, char separator)
        {
            // Replace spaces with the original separator found in the title
            return title.Replace(' ', separator);
        }

        public string? GetMediaTypeFromCategory(string? category)
        {
            if (category == null)
            {
                return null;
            }

            if (category.StartsWith("EBook", StringComparison.OrdinalIgnoreCase) || category.StartsWith("Book", StringComparison.OrdinalIgnoreCase))
            {
                return "book";
            }
            else if (category.StartsWith("Movies", StringComparison.OrdinalIgnoreCase))
            {
                return "movies";
            }
            else if (category.StartsWith("TV", StringComparison.OrdinalIgnoreCase))
            {
                return "tv";
            }
            else if (category.Contains("Audiobook", StringComparison.OrdinalIgnoreCase))
            {
                return "book";
            }

            return null;
        }


        [GeneratedRegex("[._ ]")]
            private static partial Regex WordSeperationCharRegex();
    }
}
