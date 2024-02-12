using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public class ProxyService
    {
        private readonly HttpClient _httpClient;
        private readonly string _userAgent;
        private readonly ILogger<ProxyService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<string, DateTimeOffset> _lastRequestTimes = new();

        public ProxyService(IHttpClientFactory clientFactory, IConfiguration configuration, ILogger<ProxyService> logger, IMemoryCache cache)
        {
            _httpClient = clientFactory.CreateClient("HttpClient") ?? throw new ArgumentNullException(nameof(clientFactory));
            _userAgent = configuration["Settings:UserAgent"] ?? throw new ArgumentException("UserAgent must be set in appsettings.json");
            _logger = logger;
            _cache = cache;
        }

        public async Task<HttpResponseMessage> ProxyRequestAsync(HttpContext context, string targetUri)
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                throw new ArgumentException("Only GET requests are supported", context.Request.Method);
            }

            // Throttling mechanism
            var host = new Uri(targetUri).Host;
            if (_lastRequestTimes.TryGetValue(host, out var lastRequestTime))
            {
                var timeSinceLastRequest = DateTimeOffset.Now - lastRequestTime;
                if (timeSinceLastRequest < TimeSpan.FromSeconds(3))
                {
                    await Task.Delay(TimeSpan.FromSeconds(3) - timeSinceLastRequest);
                }
            }
            _lastRequestTimes[host] = DateTimeOffset.Now;

            // Check cache
            if (_cache.TryGetValue(targetUri, out HttpResponseMessage cachedResponse))
            {
                _logger.LogInformation($"Returning cached response for {UrlUtilities.RedactApiKey(targetUri)}");
                return cachedResponse!;
            }

            var requestMessage = new HttpRequestMessage
            {
                RequestUri = new Uri(targetUri),
                Method = HttpMethod.Get,
            };

            // Copy request headers
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

            try
            {
                _logger.LogInformation($"ProxyService GET {UrlUtilities.RedactApiKey(targetUri)}");
                var responseMessage = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

                if (responseMessage.IsSuccessStatusCode)
                {
                    _cache.Set(targetUri, responseMessage, TimeSpan.FromMinutes(5));
                }

                return responseMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error proxying request: {UrlUtilities.RedactApiKey(targetUri)}. Error: {ex.Message}");

                // Create a response message indicating an internal server error
                var errorResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"An error occurred while processing your request: {ex.Message}")
                };
                return errorResponse;
            }
        }
    }
}
