using System.Text.RegularExpressions;
using System.Web;

namespace UmlautAdaptarr.Utilities
{
    public partial class UrlUtilities
    {
        [GeneratedRegex(@"^(?!http:\/\/)([a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)+.*)$")]
        private static partial Regex UrlMatchingRegex();
        public static bool IsValidDomain(string domain)
        {
            // RegEx für eine einfache URL-Validierung ohne http:// und ohne abschließenden Schrägstrich
            // Erlaubt optionale Subdomains, Domainnamen und TLDs, aber keine Pfade oder Protokolle
            var regex = UrlMatchingRegex();
            return regex.IsMatch(domain) && !domain.EndsWith("/");
        }

        public static string BuildUrl(string domain, IDictionary<string, string> queryParameters)
        {
            var uriBuilder = new UriBuilder("https", domain);

            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var param in queryParameters)
            {
                query[param.Key] = param.Value;
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }

        public static string BuildUrl(string domain, string tParameter, string? apiKey = null)
        {
            var queryParameters = new Dictionary<string, string>() { { "t", tParameter } };

            if (!string.IsNullOrEmpty(apiKey))
            {
                queryParameters["apikey"] = apiKey;
            }

            return BuildUrl(domain, queryParameters);
        }

        public static string RedactApiKey(string targetUri)
        {
            var apiKeyPattern = @"(apikey=)[^&]*";

            var redactedUri = Regex.Replace(targetUri, apiKeyPattern, "$1[REDACTED]");

            return redactedUri;
        }
    }
}
