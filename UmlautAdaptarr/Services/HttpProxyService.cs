using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UmlautAdaptarr.Options;

namespace UmlautAdaptarr.Services
{
    public class HttpProxyService : IHostedService
    {
        private TcpListener _listener;
        private readonly ILogger<HttpProxyService> _logger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly GlobalOptions _options;
        private readonly HashSet<string> _knownHosts = [];
        private readonly object _hostsLock = new();
        private readonly IConfiguration _configuration;
        private static readonly string[] newLineSeparator = ["\r\n"];

        public HttpProxyService(ILogger<HttpProxyService> logger, IHttpClientFactory clientFactory, IConfiguration configuration, IOptions<GlobalOptions> options)
        {
            _options = options.Value;
            _logger = logger;
            _configuration = configuration;
            _clientFactory = clientFactory;
            _knownHosts.Add("prowlarr.servarr.com");
        }

        private async Task HandleRequests(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var clientSocket = await _listener.AcceptSocketAsync();
                _ = Task.Run(() => ProcessRequest(clientSocket), stoppingToken);
            }
        }

        private async Task ProcessRequest(Socket clientSocket)
        {
            using var clientStream = new NetworkStream(clientSocket, ownsSocket: true);
            var buffer = new byte[8192];
            var bytesRead = await clientStream.ReadAsync(buffer);
            var requestString = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                var headers = ParseHeaders(buffer, bytesRead);

                if (!headers.TryGetValue("Proxy-Authorization", out var proxyAuthorizationHeader) ||
                    !ValidateApiKey(proxyAuthorizationHeader))
                {
                    var isFirstRequest = !headers.ContainsKey("Proxy-Authorization");
                    if (!isFirstRequest)
                    {
                        _logger.LogWarning("Unauthorized access attempt.");
                    }
                    await clientStream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"Proxy\"\r\n\r\n"));
                    clientSocket.Close();
                    return;
                }
            }

            if (requestString.StartsWith("CONNECT"))
            {
                // Handle HTTPS CONNECT request
                await HandleHttpsConnect(requestString, clientStream, clientSocket);
            }
            else
            {
                // Handle HTTP request
                await HandleHttp(requestString, clientStream, clientSocket, buffer, bytesRead);
            }
        }
        
        private bool ValidateApiKey(string proxyAuthorizationHeader)
        {
            // Expect the header to be in the format: "Basic <base64encodedApiKey>"
            if (proxyAuthorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encodedKey = proxyAuthorizationHeader["Basic ".Length..].Trim();
                var decodedKey = Encoding.ASCII.GetString(Convert.FromBase64String(encodedKey));
                var password = decodedKey.Split(':')[^1];
                return password == _options.ApiKey;
            }
            return false;
        }

        private async Task HandleHttpsConnect(string requestString, NetworkStream clientStream, Socket clientSocket)
        {
            var (host, port) = ParseTargetInfo(requestString);

            // Prowlarr will send grab requests via https which cannot be changed
            if (!_knownHosts.Contains(host))
            {
                _logger.LogWarning($"IMPORTANT! {Environment.NewLine} Indexer {host} needs to be set to http:// instead of https:// {Environment.NewLine}" +
                    $"UmlautAdaptarr will not work for {host}!");
            }
            using var targetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await targetSocket.ConnectAsync(host, port);
                await clientStream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"));
                using var targetStream = new NetworkStream(targetSocket, ownsSocket: true);
                await RelayTraffic(clientStream, targetStream);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to connect to target: {ex.Message}");
                clientSocket.Close();
            }
        }

        private async Task HandleHttp(string requestString, NetworkStream clientStream, Socket clientSocket, byte[] buffer, int bytesRead)
        {
            try
            {
                var headers = ParseHeaders(buffer, bytesRead);
                string userAgent = headers.FirstOrDefault(h => h.Key == "User-Agent").Value;
                var uri = new Uri(requestString.Split(' ')[1]);

                // Add to known hosts if not already present
                lock (_hostsLock)
                {
                    if (!_knownHosts.Contains(uri.Host))
                    {
                        _knownHosts.Add(uri.Host);
                    }
                }

                var url = _configuration["Kestrel:Endpoints:Http:Url"];
                var port = new Uri(url).Port;

                var apiKey = _options.ApiKey == null ? "_" : _options.ApiKey;

                var modifiedUri = $"http://localhost:{port}/{apiKey}/{uri.Host}{uri.PathAndQuery}";
                using var client = _clientFactory.CreateClient();
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, modifiedUri);
                httpRequestMessage.Headers.Add("User-Agent", userAgent);
                var result = await client.SendAsync(httpRequestMessage);

                if (result.IsSuccessStatusCode)
                {
                    var responseData = await result.Content.ReadAsByteArrayAsync();
                    await clientStream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {responseData.Length}\r\n\r\n"));
                    await clientStream.WriteAsync(responseData);
                }
                else
                {
                    await clientStream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {result.StatusCode}\r\n\r\n"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"HTTP Proxy error: {ex.Message}");
                await clientStream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 500 Internal Server Error\r\n\r\n"));
            }
            finally
            {
                clientSocket.Close();
            }
        }

        private Dictionary<string, string> ParseHeaders(byte[] buffer, int length)
        {
            var headers = new Dictionary<string, string>();
            var headerString = Encoding.ASCII.GetString(buffer, 0, length);
            var lines = headerString.Split(newLineSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Skip(1)) // Skip the request line
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = line[..colonIndex].Trim();
                    var value = line[(colonIndex + 1)..].Trim();
                    headers[key] = value;
                }
            }
            return headers;
        }

        private static (string host, int port) ParseTargetInfo(string requestLine)
        {
            var parts = requestLine.Split(' ')[1].Split(':');
            return (parts[0], int.Parse(parts[1]));
        }

        private async Task RelayTraffic(NetworkStream clientStream, NetworkStream targetStream)
        {
            var clientToTargetTask = RelayStream(clientStream, targetStream);
            var targetToClientTask = RelayStream(targetStream, clientStream);
            await Task.WhenAll(clientToTargetTask, targetToClientTask);
        }

        private static async Task RelayStream(NetworkStream input, NetworkStream output)
        {
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                await output.FlushAsync();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _listener = new TcpListener(IPAddress.Any, _options.ProxyPort);
            _listener.Start();
            Task.Run(() => HandleRequests(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _listener.Stop();
            return Task.CompletedTask;
        }
    }
}
