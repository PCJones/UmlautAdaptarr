namespace UmlautAdaptarr.Services
{
    public class ProxyService(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        private readonly HttpClient _httpClient = clientFactory.CreateClient("HttpClient") ?? throw new ArgumentNullException();
        private readonly string _userAgent = configuration["Settings:UserAgent"] ?? throw new ArgumentException("UserAgent must be set in appsettings.json");
        // TODO: Add cache!

        public async Task<HttpResponseMessage> ProxyRequestAsync(HttpContext context, string targetUri)
        {
            var requestMessage = new HttpRequestMessage();
            var requestMethod = context.Request.Method;

            if (!HttpMethods.IsGet(requestMethod))
            {
                throw new ArgumentException("Only GET requests are supported", nameof(requestMethod));
            }

            // Copy the request headers
            foreach (var header in context.Request.Headers)
            {
                if (header.Key == "User-Agent" && _userAgent.Length != 0)
                {
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, $"{header.Value} + {_userAgent}");
                }
                else if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            requestMessage.RequestUri = new Uri(targetUri);
            requestMessage.Method = HttpMethod.Get;

            //var responseMessage = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            try
            {
                var responseMessage = _httpClient.Send(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

                // TODO: Handle 503 etc
                responseMessage.EnsureSuccessStatusCode();

                // Modify the response content if necessary
                /*var content = await responseMessage.Content.ReadAsStringAsync();
                content = ReplaceCharacters(content);
                responseMessage.Content = new StringContent(content);*/

                return responseMessage;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        private string ReplaceCharacters(string input)
        {
            return input.Replace("Ä", "AE");
        }
    }
}
