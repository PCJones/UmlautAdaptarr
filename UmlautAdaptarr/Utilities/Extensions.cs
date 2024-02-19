using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UmlautAdaptarr.Utilities
{
    public static partial class Extensions
    {
        public static string GetQuery(this HttpContext context, string key)
        {
            return context.Request.Query[key].FirstOrDefault() ?? string.Empty;
        }
        public static string RemoveAccent(this string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);

                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }


        public static string RemoveAccentButKeepGermanUmlauts(this string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);

                if (unicodeCategory != UnicodeCategory.NonSpacingMark || c == '\u0308')
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        public static string GetLidarrTitleForExternalId(this string text)
        {
            text = text.RemoveGermanUmlautDots()
                       .Replace("-", "")
                       .GetCleanTitle()
                       .ToLower();

            // Lidarr removes the, an and a at the beginning
            return TitlePrefixRegex()
                .Replace(text, "")
                .RemoveExtraWhitespaces()
                .Trim();
        }

        public static string GetReadarrTitleForExternalId(this string text)
        {
            text = text.ToLower();

            // Readarr removes "the" at the beginning
            if (text.StartsWith("the "))
            {
                text = text[4..];
            }

            return text.RemoveGermanUmlautDots()
                       .Replace(".", " ")
                       .Replace("-", " ")
                       .Replace(":", " ")
                       .GetCleanTitle();
        }

        public static string GetCleanTitle(this string text)
        {
            return text
                .Replace(".", " ")
                .Replace(":", " ")
                .RemoveAccentButKeepGermanUmlauts()
                .RemoveSpecialCharacters(removeUmlauts: false)
                .RemoveExtraWhitespaces()
                .Trim();
        }

        public static string NormalizeForComparison(this string text)
        {
            // TODO see if we can replace RemoveGermanUmlautDots() with RemoveSpecialCharacters(removeUmlauts: false);
            return text.RemoveGermanUmlautDots().RemoveAccent().RemoveSpecialCharacters().Replace(" ", "").Trim().ToLower();
        }

        public static string RemoveSpecialCharacters(this string text, bool removeUmlauts = true)
        {
            if (removeUmlauts)
            {
                return NoSpecialCharactersExceptHypenRegex().Replace(text, "");
            }
            else
            {
                return NoSpecialCharactersExceptHyphenAndUmlautsRegex().Replace(text, "");
            }
        }


        public static string ReplaceGermanUmlautsWithLatinEquivalents(this string text)
        {
            return text
                .Replace("Ö", "Oe")
                .Replace("Ä", "Ae")
                .Replace("Ü", "Ue")
                .Replace("ö", "oe")
                .Replace("ä", "ae")
                .Replace("ü", "ue")
                .Replace("ß", "ss");
        }

        public static string RemoveGermanUmlautDots(this string text)
        {
            return text
                .Replace("ö", "o")
                .Replace("ü", "u")
                .Replace("ä", "a")
                .Replace("Ö", "O")
                .Replace("Ü", "U")
                .Replace("Ä", "A")
                .Replace("ß", "ss");
        }

        public static string RemoveExtraWhitespaces(this string text)
        {
            return MultipleWhitespaceRegex().Replace(text, " ");
        }

        public static bool HasUmlauts(this string text)
        {
            if (text == null) return false;
            var umlauts = new[] { 'ö', 'ä', 'ü', 'Ä', 'Ü', 'Ö', 'ß' };
            return umlauts.Any(text.Contains);
        }

        [GeneratedRegex("[^a-zA-Z0-9 -]+", RegexOptions.Compiled)]
        private static partial Regex NoSpecialCharactersExceptHypenRegex();

        [GeneratedRegex("[^a-zA-Z0-9 -öäüßÖÄÜß]+", RegexOptions.Compiled)]
        private static partial Regex NoSpecialCharactersExceptHyphenAndUmlautsRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex MultipleWhitespaceRegex();

        [GeneratedRegex(@"\b(the|an|a)\b", RegexOptions.IgnoreCase, "de-DE")]
        private static partial Regex TitlePrefixRegex();
    }
}
