using Microsoft.Extensions.Logging.Abstractions;
using System.Text.RegularExpressions;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Models
{
    public partial class SearchItem
    {
        public int ArrId { get; set; }
        public string ExternalId { get; set; }
        public string Title { get; set; }
        public bool HasUmlaut => Title?.HasUmlauts() ?? false;
        public string ExpectedTitle { get; set; }
        public string? ExpectedAuthor { get; set; }
        public string? GermanTitle { get; set; }
        public string[] TitleSearchVariations { get; set; }
        public string[] TitleMatchVariations { get; set; }
        public string[] AuthorMatchVariations { get; set; }
        public string MediaType { get; set; }
        // TODO public MediaType instead of string

        public SearchItem(
            int arrId,
            string externalId,
            string title,
            string expectedTitle,
            string? germanTitle,
            string mediaType,
            string[]? aliases,
            string? expectedAuthor = null)
        {
            ArrId = arrId;
            ExternalId = externalId;
            Title = title;
            ExpectedTitle = expectedTitle;
            ExpectedAuthor = expectedAuthor;
            GermanTitle = germanTitle;
            MediaType = mediaType;
            if (mediaType == "audio" && expectedAuthor != null)
            {
                // e.g. Die Ärzte - best of die Ärzte
                if (expectedTitle.Contains(expectedAuthor))
                {
                    var titleWithoutAuthorName = expectedTitle.Replace(expectedAuthor, string.Empty).RemoveExtraWhitespaces().Trim();
                    TitleMatchVariations = GenerateVariations(titleWithoutAuthorName, mediaType).ToArray();
                }
                else
                {
                    TitleMatchVariations = GenerateVariations(expectedTitle, mediaType).ToArray();
                }
                TitleSearchVariations = GenerateVariations($"{expectedAuthor} {expectedTitle}", mediaType).ToArray();
                AuthorMatchVariations = GenerateVariations(expectedAuthor, mediaType).ToArray();
            }
            else
            {
                TitleSearchVariations = GenerateVariations(germanTitle, mediaType).ToArray();

                var allTitleVariations = new List<string>(TitleSearchVariations);

                // If aliases are not null, generate variations for each and add them to the list
                // TODO (not necessarily here) only use deu and eng alias
                if (aliases != null)
                {
                    foreach (var alias in aliases)
                    {
                        allTitleVariations.AddRange(GenerateVariations(alias, mediaType));
                    }
                }

                AuthorMatchVariations = [];

                // if a german title ends with (DE) also add a search string that replaces (DE) with GERMAN
                // also add a matching title without (DE)
                if (germanTitle?.EndsWith("(DE)") ?? false)
                {
                    TitleSearchVariations = [.. TitleSearchVariations, .. 
                        GenerateVariations(
                            germanTitle.Replace("(DE)", " GERMAN").RemoveExtraWhitespaces(),
                        mediaType)];

                    allTitleVariations.AddRange(GenerateVariations(germanTitle.Replace("(DE)", "").Trim(), mediaType));

                }

                TitleMatchVariations = allTitleVariations.Distinct().ToArray();
            }
        }

        private IEnumerable<string> GenerateVariations(string? germanTitle, string mediaType)
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

            // If a german title starts with der/die/das also accept variations without it
            if (mediaType != "audio" && cleanTitle.StartsWith("Der") || cleanTitle.StartsWith("Die") || cleanTitle.StartsWith("Das"))
            {
                var cleanTitleWithoutArticle = germanTitle[3..].Trim();
                baseVariations.AddRange(GenerateVariations(cleanTitleWithoutArticle, mediaType));
            }        

            // Remove multiple spaces
            var cleanedVariations = baseVariations.Select(variation => variation.RemoveExtraWhitespaces());

            return cleanedVariations.Distinct();
        }
    }
}