using System;
using System.Net;
using UmlautAdaptarr.Options;

namespace UmlautAdaptarr.Utilities
{
    /// <summary>
    /// Extension methods for configuring proxies.
    /// </summary>
    public static class ProxyExtension
    {
        /// <summary>
        /// Logger instance for logging proxy configurations.
        /// </summary>
        public static ILogger Logger = GlobalStaticLogger.Logger;

        /// <summary>
        /// Configures the proxy settings for the provided HttpClientHandler instance.
        /// </summary>
        /// <param name="handler">The HttpClientHandler instance to configure.</param>
        /// <param name="proxyOptions">Proxy options to be used for configuration.</param>
        /// <returns>The configured HttpClientHandler instance.</returns>
        public static HttpClientHandler ConfigureProxy(this HttpClientHandler handler, Proxy? proxyOptions)
        {
            try
            {
                if (proxyOptions != null && proxyOptions.Enabled)
                {
                    Logger.LogInformation("Use Proxy {0}", proxyOptions.Address);
                    handler.UseProxy = true;
                    handler.Proxy = new WebProxy(proxyOptions.Address, true);

                    if (!string.IsNullOrEmpty(proxyOptions.Username) && !string.IsNullOrEmpty(proxyOptions.Password))
                    {
                        Logger.LogInformation("Use Proxy Credentials from User {0}", proxyOptions.Username);
                        handler.DefaultProxyCredentials =
                            new NetworkCredential(proxyOptions.Username, proxyOptions.Password);
                    }
                }
                else
                {
                    Logger.LogDebug("No Proxy was setup");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred while configuring proxy, no Proxy will be used!");
            }

            return handler;
        }
    }
}