using System.Net;

namespace UmlautAdaptarr.Services
{
    public class HttpProxyService : IHostedService
    {
        private HttpListener _listener;
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<HttpProxyService> _logger;
        private const int PROXY_PORT = 5006; // TODO move to appsettings.json

        public HttpProxyService(IHttpClientFactory clientFactory, ILogger<HttpProxyService> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        }

        private async Task HandleRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await ProcessRequest(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error handling request: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var originalUri = new Uri(request.RawUrl);
                var modifiedUri = "http://localhost:5005/_/" + originalUri.Host + originalUri.PathAndQuery; // TODO read port from appsettings?

                // Act as a proxy and forward the modified request to internal endpoints
                using var client = _clientFactory.CreateClient();
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, modifiedUri);
                var result = await client.SendAsync(httpRequestMessage);

                if (result.IsSuccessStatusCode)
                {
                    var responseData = await result.Content.ReadAsByteArrayAsync();
                    response.ContentLength64 = responseData.Length;
                    await response.OutputStream.WriteAsync(responseData);
                }
                else
                {
                    response.StatusCode = (int)result.StatusCode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"HTTP Proxy error: {ex.Message}");
                response.StatusCode = 500;
            }
            finally
            {
                response.OutputStream.Close();
            }
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{PROXY_PORT}/");
            _listener.Start();
            Task.Run(HandleRequests, cancellationToken);
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _listener.Stop();
            _listener.Close();
            return Task.CompletedTask;
        }
    }
}
