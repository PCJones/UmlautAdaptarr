using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using UmlautAdaptarr.Options;
using UmlautAdaptarr.Utilities;

namespace UmlautAdaptarr.Services
{
    public class ProxyRequestService
    {
        private readonly HttpClient _httpClient;
        private readonly string _userAgent;
        private readonly ILogger<ProxyRequestService> _logger;
        private readonly IMemoryCache _cache;
        private readonly GlobalOptions _options;
        private static readonly ConcurrentDictionary<string, DateTimeOffset> _lastRequestTimes = new();
        private static readonly TimeSpan MINIMUM_DELAY_FOR_SAME_HOST = new(0, 0, 0, 1);

        public ProxyRequestService(IHttpClientFactory clientFactory, ILogger<ProxyRequestService> logger, IMemoryCache cache, IOptions<GlobalOptions> options)
        {
            _options = options.Value;
            _httpClient = clientFactory.CreateClient("HttpClient") ?? throw new ArgumentNullException(nameof(clientFactory));
            _userAgent =  _options.UserAgent ?? throw new ArgumentException("UserAgent must be set in appsettings.json");
            _logger = logger;
            _cache = cache;
        }

        private static async Task EnsureMinimumDelayAsync(string targetUri)
        {
            var host = new Uri(targetUri).Host;
            if (_lastRequestTimes.TryGetValue(host, out var lastRequestTime))
            {
                var timeSinceLastRequest = DateTimeOffset.Now - lastRequestTime;
                if (timeSinceLastRequest < MINIMUM_DELAY_FOR_SAME_HOST)
                {
                    await Task.Delay(MINIMUM_DELAY_FOR_SAME_HOST - timeSinceLastRequest);
                }
            }
            _lastRequestTimes[host] = DateTimeOffset.Now;
        }

        public async Task<HttpResponseMessage> ProxyRequestAsync(HttpContext context, string targetUri)
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                throw new ArgumentException("Only GET requests are supported", context.Request.Method);
            }

            // Check cache
            if (_cache.TryGetValue(targetUri, out HttpResponseMessage cachedResponse))
            {
                _logger.LogInformation($"Returning cached response for {UrlUtilities.RedactApiKey(targetUri)}");
                return cachedResponse!;
            }

            await EnsureMinimumDelayAsync(targetUri);
            var targetHost = new Uri(targetUri).Host;

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
                _logger.LogInformation($"ProxyRequestService GET {UrlUtilities.RedactApiKey(targetUri)}");
                var responseMessage = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

                if (responseMessage.IsSuccessStatusCode)
                {
                    _cache.Set(targetUri, responseMessage, TimeSpan.FromMinutes(_options.IndexerRequestsCacheDurationInMinutes));
                }

                return responseMessage;
            }
            catch (Exception ex)
            {
                var upstreamMessage = IsTimeoutOrReset(ex)
                    ? $"{targetHost} timed out or reset the connection. This is an error with your indexer or network, not with UmlautAdaptarr."
                    : $"{targetHost} request failed. This is an error with your indexer or network, not with UmlautAdaptarr.";

                _logger.LogError(ex, "{UpstreamMessage} Target: {TargetUri} Error: {ErrorMessage}",
                    upstreamMessage,
                    UrlUtilities.RedactApiKey(targetUri),
                    ex.Message);

                var errorResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"An error occurred while processing your request: {ex.Message}")
                };
                return errorResponse;
            }
        }

        private static bool IsTimeoutOrReset(Exception ex)
        {
            if (ex is TaskCanceledException)
            {
                return true;
            }

            var current = ex;
            while (current != null)
            {
                if (current is System.Net.Sockets.SocketException socketEx)
                {
                    return socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut
                        || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset;
                }
                current = current.InnerException;
            }

            return false;
        }
    }
}
