using UmlautAdaptarr.Options;
using UmlautAdaptarr.Options.ArrOptions;
using UmlautAdaptarr.Providers;
using UmlautAdaptarr.Services;

namespace UmlautAdaptarr.Utilities
{
    /// <summary>
    /// Extension methods for configuring services related to ARR Applications
    /// </summary>
    public static class ServicesExtensions
    {

        /// <summary>
        /// Adds a service with specified options and service to the service collection.
        /// </summary>
        /// <typeparam name="TOptions">The options type for the service.</typeparam>
        /// <typeparam name="TService">The service type for the service.</typeparam>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure the service collection.</param>
        /// <param name="sectionName">The name of the configuration section containing service options.</param>
        /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
        private static WebApplicationBuilder AddServiceWithOptions<TOptions, TService>(this WebApplicationBuilder builder, string sectionName)
            where TOptions : class
            where TService : class
        {
            if (builder.Services == null)
            {
                throw new ArgumentNullException(nameof(builder), "Service collection is null.");
            }

            var options = builder.Configuration.GetSection(sectionName).Get<TOptions>();
            if (options == null)
            {
                throw new InvalidOperationException($"{typeof(TService).Name} options could not be loaded from Configuration or ENV Variable.");
            }

            builder.Services.Configure<TOptions>(builder.Configuration.GetSection(sectionName));
            builder.Services.AddSingleton<TService>();
            return builder;
        }

        /// <summary>
        /// Adds support for Sonarr with default options and client.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure the service collection.</param>
        /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder AddSonarrSupport(this WebApplicationBuilder builder)
        {
            return builder.AddServiceWithOptions<SonarrInstanceOptions, SonarrClient>("Sonarr");
        }

        /// <summary>
        /// Adds support for Lidarr with default options and client.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure the service collection.</param>
        /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder AddLidarrSupport(this WebApplicationBuilder builder)
        {
            return builder.AddServiceWithOptions<LidarrInstanceOptions, LidarrClient>("Lidarr");
        }

        /// <summary>
        /// Adds support for Readarr with default options and client.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure the service collection.</param>
        /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder AddReadarrSupport(this WebApplicationBuilder builder)
        {
            return builder.AddServiceWithOptions<ReadarrInstanceOptions, ReadarrClient>("Readarr");
        }

        /// <summary>
        /// Adds a title lookup service to the service collection.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure the service collection.</param>
        /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder AddTitleLookupService(this WebApplicationBuilder builder)
        {
            return builder.AddServiceWithOptions<GlobalOptions, TitleApiService>("Settings");
        }

        /// <summary>
        /// Adds a proxy service to the service collection.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure the service collection.</param>
        /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
        public static WebApplicationBuilder AddProxyService(this WebApplicationBuilder builder)
        {
            return builder.AddServiceWithOptions<GlobalOptions, ProxyService>("Settings");
        }
    }
}
