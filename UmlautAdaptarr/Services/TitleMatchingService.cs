using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UmlautAdaptarr.Models;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public partial class TitleMatchingService(CacheService cacheService, ILogger<TitleMatchingService> logger)
    {
        public string RenameTitlesInContent(string content, SearchItem? searchItem)
        {
            var xDoc = XDocument.Parse(content);

            bool useCacheService = searchItem == null;

            foreach (var item in xDoc.Descendants("item"))
            {
                var titleElement = item.Element("title");
                if (titleElement != null)
                {
                    var originalTitle = titleElement.Value;
                    var cleanTitleSeperatedBySpace = ReplaceSeperatorsWithSpace(originalTitle.RemoveAccentButKeepGermanUmlauts());

                    var categoryElement = item.Element("category");
                    var category = categoryElement?.Value;
                    var mediaType = GetMediaTypeFromCategory(category);

                    if (mediaType == null)
                    {
                        continue;
                    }

                    if (useCacheService)
                    {
                        // Use CacheService to find a matching SearchItem by title
                        searchItem = cacheService.SearchItemByTitle(mediaType, cleanTitleSeperatedBySpace);
                    }

                    if (searchItem == null)
                    {
                        // Skip processing this item if no matching SearchItem is found
                        continue;
                    }

                    switch (mediaType)
                    {
                        case "tv":
                            FindAndReplaceForMoviesAndTV(logger, searchItem, titleElement, originalTitle, cleanTitleSeperatedBySpace!);
                            break;
                        case "movie":
                            FindAndReplaceForMoviesAndTV(logger, searchItem, titleElement, originalTitle, cleanTitleSeperatedBySpace!);
                            break;
                        case "audio":
                            FindAndReplaceForBooksAndAudio(searchItem, titleElement, originalTitle!);
                            break;
                        case "book":
                            FindAndReplaceForBooksAndAudio(searchItem, titleElement, originalTitle!);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            return xDoc.ToString();
        }

        public void FindAndReplaceForBooksAndAudio(SearchItem searchItem, XElement? titleElement, string originalTitle)
        {
            var authorMatch = FindBestMatch(searchItem.AuthorMatchVariations, originalTitle.NormalizeForComparison(), originalTitle);
            var (foundMatch, bestStart, bestEndInOriginal) = FindBestMatch(searchItem.TitleMatchVariations, originalTitle.NormalizeForComparison(), originalTitle);

            if (authorMatch.foundMatch && foundMatch)
            {
                int matchEndPositionInOriginal = Math.Max(authorMatch.bestEndInOriginal, bestEndInOriginal);

                // Check and adjust for immediate following delimiter
                char[] delimiters = [' ', '-', '_', '.'];
                if (matchEndPositionInOriginal < originalTitle.Length && delimiters.Contains(originalTitle[matchEndPositionInOriginal]))
                {
                    matchEndPositionInOriginal++; // Skip the delimiter if it's immediately after the match
                }

                // Ensure we trim any leading delimiters from the suffix
                string suffix = originalTitle[matchEndPositionInOriginal..].TrimStart([' ', '-', '_', '.']).Trim();

                // Concatenate the expected title with the remaining suffix
                var updatedTitle = $"{searchItem.ExpectedAuthor} - {searchItem.ExpectedTitle}";
                if (suffix.Length >= 3)
                {
                    updatedTitle += $"-[{suffix}]";
                }

                // Update the title element
                titleElement.Value = updatedTitle;
                logger.LogInformation($"TitleMatchingService - Title changed: '{originalTitle}' to '{updatedTitle}'");
            }
            else
            {
                logger.LogDebug($"TitleMatchingService - No satisfactory fuzzy match found for both author and title for {originalTitle}.");
            }
        }


        private static (bool foundMatch, int bestStart, int bestEndInOriginal) FindBestMatch(string[] variations, string normalizedOriginal, string originalTitle)
        {
            bool found = false;
            int bestStart = int.MaxValue;
            int bestEndInOriginal = -1;

            foreach (var variation in variations)
            {
                var normalizedVariation = variation.NormalizeForComparison();
                int startNormalized = normalizedOriginal.IndexOf(normalizedVariation);

                if (startNormalized >= 0)
                {
                    found = true;
                    // Map the start position from the normalized string back to the original string
                    int startOriginal = MapNormalizedIndexToOriginal(normalizedOriginal, originalTitle, startNormalized);
                    int endOriginal = MapNormalizedIndexToOriginal(normalizedOriginal, originalTitle, startNormalized + normalizedVariation.Length);

                    bestStart = Math.Min(bestStart, startOriginal);
                    bestEndInOriginal = Math.Max(bestEndInOriginal, endOriginal);
                }
            }

            if (!found) return (false, 0, 0);
            return (found, bestStart, bestEndInOriginal);
        }

        // Maps an index from the normalized string back to a corresponding index in the original string
        private static int MapNormalizedIndexToOriginal(string normalizedOriginal, string originalTitle, int normalizedIndex)
        {
            // Count non-special characters up to the given index in the normalized string
            int nonSpecialCharCount = 0;
            for (int i = 0; i < normalizedIndex && i < normalizedOriginal.Length; i++)
            {
                if (char.IsLetterOrDigit(normalizedOriginal[i]))
                {
                    nonSpecialCharCount++;
                }
            }

            // Count non-special characters in the original title to find the corresponding index
            int originalIndex = 0;
            for (int i = 0; i < originalTitle.Length; i++)
            {
                if (char.IsLetterOrDigit(originalTitle[i]))
                {
                    if (--nonSpecialCharCount < 0)
                    {
                        break;
                    }
                }
                originalIndex = i;
            }

            return originalIndex;
        }

        // This method replaces the first variation that starts at the beginning of the release title
        private static void FindAndReplaceForMoviesAndTV(ILogger<TitleMatchingService> logger, SearchItem searchItem, XElement? titleElement, string originalTitle, string normalizedOriginalTitle)
        {
            var titleMatchVariations = searchItem.TitleMatchVariations;
            var expectedTitle = searchItem.ExpectedTitle;
            var variationsOrderedByLength = titleMatchVariations!.OrderByDescending(variation => variation.Length);

            // Attempt to find a variation that matches the start of the original title
            foreach (var variation in variationsOrderedByLength)
            {
                // Skip variations that are already the expectedTitle
                if (variation == expectedTitle)
                {
                    continue;
                }

                // Variation is already normalized at creation
                var variationMatchPattern = "^" + Regex.Escape(variation).Replace("\\ ", "[._ ]");

                // Check if the originalTitle starts with the variation (ignoring case and separators)
                if (Regex.IsMatch(normalizedOriginalTitle, variationMatchPattern, RegexOptions.IgnoreCase))
                {
                    var originalTitleMatchPattern = "^" + Regex.Escape(variation).Replace("\\ ", "[._ ]");

                    // Find the first separator used in the original title for consistent replacement
                    var separator = FindFirstSeparator(originalTitle);
                    // Reconstruct the expected title using the original separator
                    var newTitlePrefix = expectedTitle!.Replace(" ", separator.ToString());

                    // Extract the suffix from the original title starting right after the matched variation length
                    var variationLength = variation.Length;
                    var suffix = originalTitle[Math.Min(variationLength, originalTitle.Length)..];

                    // Workaround for the rare case of e.g. "Frieren: Beyond Journey's End" that also has the alias "Frieren"
                    if (expectedTitle!.StartsWith(variation, StringComparison.OrdinalIgnoreCase))
                    {
                        // See if we already matched the whole title by checking if S01E01 pattern is coming next to avoid false positives
                        // - that won't help with movies but with tv shows
                        var seasonMatchingPattern = $"^{separator}S\\d{{1,2}}E\\d{{1,2}}";
                        if (!Regex.IsMatch(suffix, seasonMatchingPattern))
                        {
                            logger.LogWarning($"TitleMatchingService - Didn't rename: '{originalTitle}' because the expected title '{expectedTitle}' starts with the variation '{variation}'");
                            continue;
                        }
                    }

                    // Clean up any leading separator from the suffix
                    suffix = Regex.Replace(suffix, "^ +", "");

                    // TODO add this when radarr is implemented
                    // FixBadReleaseNaming

                    // Construct the new title with the original suffix
                    var newTitle = newTitlePrefix + (string.IsNullOrEmpty(suffix) ? "" : suffix.StartsWith(separator) ? suffix : $"{separator}{suffix}");

                    // Update the title element's value with the new title
                    //titleElement.Value = newTitle + $"({originalTitle.Substring(0, variationLength)})";
                    titleElement.Value = newTitle;

                    logger.LogInformation($"TitleMatchingService - Title changed: '{originalTitle}' to '{newTitle}'");
                    break;
                }
            }
        }

        private static readonly string[] MissingGermanTagReleaseGroups = ["tvr"];
        private static readonly string[] HEVCInsteadOfx265TagReleaseGroups = ["eisbaer"];
        private static readonly string[] WrongTagsReleaseGroups = ["eisbaer"];
        private static string FixBadReleaseNaming(string title, string seperator, ILogger<TitleMatchingService> logger)
        {
            var releaseGroup = GetReleaseGroup(title);
            if (MissingGermanTagReleaseGroups.Contains(releaseGroup))
            {
                // Check if "german" is not in the title, ignoring case
                if (!Regex.IsMatch(title, "german", RegexOptions.IgnoreCase))
                {
                    logger.LogInformation($"FixBadReleaseNaming - found missing GERMAN tag for {title}");
                    // TODO not finished
                    // Insert "GERMAN" after the newTitlePrefix
                    //newTitlePrefix += separator + "GERMAN";
                }
            }

            if (HEVCInsteadOfx265TagReleaseGroups.Contains(releaseGroup))
            {
                if (!title.Contains("REMUX", StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.LogInformation($"FixBadReleaseNaming - found HEVC instead of x265 for {title}");
                    title = title.Replace("HEVC", "x265");
                }
            }

            if (WrongTagsReleaseGroups.Contains(releaseGroup))
            {
                if (title.Contains($"{seperator}RM{seperator}"))
                {
                    logger.LogInformation($"FixBadReleaseNaming - found bad Tag RM instead of REMASTERED for {title}");
                    title = title.Replace($"{seperator}RM{seperator}", $"{seperator}REMASTERED{seperator}");
                }
            }

            return "";
        }

        private static string? GetReleaseGroup(string title)
        {
            return title.Contains('-') ? title[(title.LastIndexOf('-') + 1)..].Trim() : null;
        }

        private static string ReplaceSeperatorsWithSpace(string title)
        {
            // Replace all known separators with space for normalization
            return WordSeperationCharRegex().Replace(title, " ".ToString());
        }

        private static char FindFirstSeparator(string title)
        {
            var match = WordSeperationCharRegex().Match(title);
            return match.Success ? match.Value.First() : ' ';
        }

        private static string ReconstructTitleWithSeparator(string title, char separator)
        {
            if (separator != ' ')
            {
                return title;
            }
            
            return title.Replace(' ', separator);
        }

        public string? GetMediaTypeFromCategory(string? category)
        {
            if (category == null)
            {
                return null;
            }

            if (category == "7000" || category.StartsWith("EBook", StringComparison.OrdinalIgnoreCase) || category.StartsWith("Book", StringComparison.OrdinalIgnoreCase))
            {
                return "book";
            }
            else if (category == "2000" || category.StartsWith("Movies", StringComparison.OrdinalIgnoreCase))
            {
                return "movies";
            }
            else if (category == "5000" || category.StartsWith("TV", StringComparison.OrdinalIgnoreCase))
            {
                return "tv";
            }
            else if (category == "3030" || category.Contains("Audiobook", StringComparison.OrdinalIgnoreCase))
            {
                return "book";
            }
            else if (category == "3000" || category.StartsWith("Audio"))
            {
                return "audio";
            }

            return null;
        }


        [GeneratedRegex("[._ ]")]
            private static partial Regex WordSeperationCharRegex();

    }
}
