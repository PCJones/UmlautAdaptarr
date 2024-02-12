using System.Text.RegularExpressions;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Models
{
    public partial class SearchItem
    {
        public int ArrId { get; set; }
        public string ExternalId { get; set; }
        public string Title { get; set; }
        public bool HasGermanUmlaut => Title?.HasGermanUmlauts() ?? false;
        public string ExpectedTitle { get; set; }
        public string? GermanTitle { get; set; }
        public string[] TitleSearchVariations { get; set; }
        public string[] TitleMatchVariations { get; set; }
        public string MediaType { get; set; }
        // TODO public MediaType instead of string

        public SearchItem(int arrId, string externalId, string title, string expectedTitle, string? germanTitle, string mediaType, string[]? aliases)
        {
            ArrId = arrId;
            ExternalId = externalId;
            Title = title;
            ExpectedTitle = expectedTitle;
            GermanTitle = germanTitle;
            TitleSearchVariations = GenerateTitleVariations(germanTitle).ToArray();
            MediaType = mediaType;

            var allTitleVariations = new List<string>(TitleSearchVariations);

            // If aliases are not null, generate variations for each and add them to the list
            // TODO (not necessarily here) only use deu and eng alias
            if (aliases != null)
            {
                foreach (var alias in aliases)
                {
                    allTitleVariations.AddRange(GenerateTitleVariations(alias));
                }
            }

            TitleMatchVariations = allTitleVariations.Distinct().ToArray();
        }

        private IEnumerable<string> GenerateTitleVariations(string? germanTitle)
        {
            if (germanTitle == null)
            {
                return [];
            }
            var cleanTitle = germanTitle.RemoveAccentButKeepGermanUmlauts().GetCleanTitle();

            // Start with base variations including handling umlauts
            var baseVariations = new List<string>
                {
                    cleanTitle, // No change
                    cleanTitle.ReplaceGermanUmlautsWithLatinEquivalents(),
                    cleanTitle.RemoveGermanUmlautDots()
                };

            // TODO: determine if this is really needed
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

            // Remove multiple spaces
            var cleanedVariations = baseVariations.Select(variation => MultipleWhitespaceRegex().Replace(variation, " "));

            return cleanedVariations.Distinct();
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex MultipleWhitespaceRegex();
    }
}