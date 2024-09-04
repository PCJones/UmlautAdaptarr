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
        // TODO rename GermanTitle into Foreign or LocalTitle?
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
            if ((mediaType == "audio" || mediaType == "book") && expectedAuthor != null)
            {
                GenerateVariationsForBooksAndAudio(expectedTitle, mediaType, expectedAuthor);
            }
            else
            {
                // if mediatype is movie/tv and the Expected Title ends with a year but the german title doesn't then append the year to the german title and to aliases
                // example: https://thetvdb.com/series/385925-avatar-the-last-airbender -> german Title is without 2024
                var yearAtEndOfTitleMatch = YearAtEndOfTitleRegex().Match(expectedTitle);
                if (yearAtEndOfTitleMatch.Success)
                {
                    string year = yearAtEndOfTitleMatch.Value[1..^1];
                    if (GermanTitle != null && !GermanTitle.Contains(year))
                    {
                        GermanTitle = $"{germanTitle} {year}";
                    }

                    if (aliases != null)
                    {
                        for (int i = 0; i < aliases.Length; i++)
                        {
                            if (!aliases[i].Contains(year))
                            {
                                aliases[i] = $"{aliases[i]} {year}";
                            }
                        }
                    }
                }

                GenerateVariationsForTV(GermanTitle, mediaType, aliases);
            }
        }

        private void GenerateVariationsForTV(string? germanTitle, string mediaType, string[]? aliases)
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

                    // If title contains ":" also match for "-"
                    if (alias.Contains(':'))
                    {
                        allTitleVariations.Add(alias.Replace(":", " -"));
                    }
                }
            }

            AuthorMatchVariations = [];

            // if a german title ends with (DE) also add a search string that replaces (DE) with GERMAN
            // also add a matching title without (DE)
            if (germanTitle?.EndsWith("(DE)") ?? false)
            {
                TitleSearchVariations = [.. TitleSearchVariations,
                    ..
                    GenerateVariations(
                        germanTitle.Replace("(DE)", " GERMAN").RemoveExtraWhitespaces(),
                    mediaType)];

                allTitleVariations.AddRange(GenerateVariations(germanTitle.Replace("(DE)", "").Trim(), mediaType));

            }

            // if a german title ends with "Germany" (e.g. Good Luck Guys Germany) also add a search string that replaces Germany with GERMAN
            // (e.g. Good Luck Guys GERMAN). This is because reality shows often have different formats in different countries with the same
            // name. // also add a matching title without GERMAN
            if (germanTitle?.EndsWith("germany", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                TitleSearchVariations = [.. TitleSearchVariations,
                    ..
                    GenerateVariations(
                        (germanTitle[..^7] + "GERMAN").RemoveExtraWhitespaces(),
                    mediaType)];

                allTitleVariations.AddRange(GenerateVariations(germanTitle[..^8].Trim(), mediaType));
            }

            // If title contains ":" also match for "-"
            if (germanTitle?.Contains(':') ?? false)
            {
                allTitleVariations.Add(germanTitle.Replace(":", " -"));
            }

            TitleMatchVariations = allTitleVariations.Distinct(StringComparer.InvariantCultureIgnoreCase).ToArray();
        }

        private void GenerateVariationsForBooksAndAudio(string expectedTitle, string mediaType, string? expectedAuthor)
        {
            // e.g. Die Ärzte - best of die Ärzte
            if (expectedTitle.Contains(expectedAuthor))
            {
                var titleWithoutAuthorName = expectedTitle.Replace(expectedAuthor, string.Empty).RemoveExtraWhitespaces().Trim();

                if (titleWithoutAuthorName.Length < 2)
                {
                    // TODO log warning that this album can't be searched for automatically
                }
                TitleMatchVariations = GenerateVariations(titleWithoutAuthorName, mediaType).ToArray();
            }
            else
            {
                TitleMatchVariations = GenerateVariations(expectedTitle, mediaType).ToArray();
            }

            TitleSearchVariations = GenerateVariations($"{expectedAuthor} {expectedTitle}", mediaType).ToArray();
            AuthorMatchVariations = GenerateVariations(expectedAuthor, mediaType).ToArray();

            if (mediaType == "book")
            {
                if (expectedAuthor?.Contains(' ') ?? false)
                {
                    var nameParts = expectedAuthor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var lastName = nameParts.Last();
                    var firstNames = nameParts.Take(nameParts.Length - 1);

                    var alternativeExpectedAuthor = $"{lastName}, {string.Join(" ", firstNames)}";
                    AuthorMatchVariations = [.. AuthorMatchVariations, .. GenerateVariations(alternativeExpectedAuthor, mediaType)];
                }
            }
        }

        private IEnumerable<string> GenerateVariations(string? title, string mediaType)
        {
            if (title == null)
            {
                return [];
            }

            var cleanTitle = title.GetCleanTitle();

            if (cleanTitle?.Length == 0)
            {
                return [];
            }

            // Start with base variations including handling umlauts
            var baseVariations = new List<string>
                {
                    cleanTitle, // No change
                    cleanTitle.ReplaceGermanUmlautsWithLatinEquivalents(),
                    cleanTitle.RemoveGermanUmlautDots()
                };

            if (mediaType == "book" || mediaType == "audio")
            {
                baseVariations.Add(cleanTitle.RemoveGermanUmlauts());
            }

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

            // If a title starts with der/die/das also accept variations without it
            // Same for english the, an, a
            if (cleanTitle.StartsWith("Der ") || cleanTitle.StartsWith("Die ") || cleanTitle.StartsWith("Das ")
                || cleanTitle.StartsWith("The ") || cleanTitle.StartsWith("An "))
            {
                var cleanTitleWithoutArticle = title[3..].Trim();
                baseVariations.AddRange(GenerateVariations(cleanTitleWithoutArticle, mediaType));
            } else if (cleanTitle.StartsWith("A "))
            {
                var cleanTitleWithoutArticle = title[2..].Trim();
                baseVariations.AddRange(GenerateVariations(cleanTitleWithoutArticle, mediaType));
            }

            // Remove multiple spaces
            var cleanedVariations = baseVariations.Select(variation => variation.RemoveExtraWhitespaces());

            return cleanedVariations.Distinct();
        }

        [GeneratedRegex(@"\(\d{4}\)$")]
        private static partial Regex YearAtEndOfTitleRegex();
    }
}