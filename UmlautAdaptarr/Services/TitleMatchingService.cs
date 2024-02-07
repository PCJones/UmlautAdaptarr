using System.Text.RegularExpressions;
using System.Xml.Linq;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public partial class TitleMatchingService
    {
        public List<string> GenerateTitleVariations(string germanTitle)
        {
            var cleanTitle = germanTitle.RemoveAccentButKeepGermanUmlauts();
            
            // Start with base variations including handling umlauts
            var baseVariations = new List<string>
                {
                    cleanTitle, // No change
                    cleanTitle.ReplaceGermanUmlautsWithLatinEquivalents(),
                    cleanTitle.RemoveGermanUmlautDots()
                };

            // Additional variations to accommodate titles with "-"
            if (cleanTitle.Contains('-'))
            {
                var withoutDash = cleanTitle.Replace("-", "");
                var withSpaceInsteadOfDash = cleanTitle.Replace("-", " ");

                // Add variations of the title without dash and with space instead of dash
                baseVariations.AddRange(new List<string>
                {
                    withoutDash,
                    withSpaceInsteadOfDash,
                    withoutDash.ReplaceGermanUmlautsWithLatinEquivalents(),
                    withoutDash.RemoveGermanUmlautDots(),
                    withSpaceInsteadOfDash.ReplaceGermanUmlautsWithLatinEquivalents(),
                    withSpaceInsteadOfDash.RemoveGermanUmlautDots()
                });
            }

            return baseVariations.Distinct().ToList();
        }


        public string RenameTitlesInContent(string content, List<string> germanTitleVariations, string expectedTitle)
        {
            var xDoc = XDocument.Parse(content);

            foreach (var item in xDoc.Descendants("item"))
            {
                var titleElement = item.Element("title");
                if (titleElement != null)
                {
                    var originalTitle = titleElement.Value;
                    var normalizedOriginalTitle = NormalizeTitle(originalTitle);

                    // Attempt to find a variation that matches the start of the original title
                    foreach (var variation in germanTitleVariations)
                    {
                        // Variation is already normalized at creation
                        var pattern = "^" + Regex.Escape(variation).Replace("\\ ", "[._ ]");

                        // Check if the originalTitle starts with the variation (ignoring case and separators)
                        if (Regex.IsMatch(normalizedOriginalTitle, pattern, RegexOptions.IgnoreCase))
                        {
                            // Find the first separator used in the original title for consistent replacement
                            var separator = FindFirstSeparator(originalTitle);
                            // Reconstruct the expected title using the original separator
                            var newTitlePrefix = expectedTitle.Replace(" ", separator.ToString());

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
                            titleElement.Value = newTitle + $"({originalTitle.Substring(0, variationLength)})";
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
            // Replace all known separators with a consistent one for normalization
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


        [GeneratedRegex("[._ ]")]
            private static partial Regex WordSeperationCharRegex();
    }
}
