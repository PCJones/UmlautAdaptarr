using Microsoft.Extensions.Configuration;
using System.Net;
using UmlautAdaptarr.Providers;
using UmlautAdaptarr.Routing;
using UmlautAdaptarr.Services;

internal class Program
{
    private static void Main(string[] args)
    {
        // TODO:
        // add option to sort by nzb age

    
       var builder = WebApplication.CreateBuilder(args);

        var configuration = builder.Configuration;

        // Add services to the container.
        builder.Services.AddHttpClient("HttpClient").ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            return handler;
        });

        builder.Services.AddMemoryCache(options =>
        {
            //options.SizeLimit = 20000;
        });


        // TODO workaround to not log api keys
        builder.Logging.AddFilter((category, level) =>
        {
            // Prevent logging of HTTP request and response if the category is HttpClient
            if (category.Contains("System.Net.Http.HttpClient") || category.Contains("Microsoft.Extensions.Http.DefaultHttpClientFactory"))
            {
                return false;
            }
            return true;
        });

        builder.Services.AddControllers();
        builder.Services.AddHostedService<ArrSyncBackgroundService>();
        builder.Services.AddSingleton<TitleApiService>();
        builder.Services.AddSingleton<SearchItemLookupService>();
        builder.Services.AddSingleton<TitleMatchingService>();
        builder.Services.AddSingleton<SonarrClient>();
        builder.Services.AddSingleton<LidarrClient>();
        builder.Services.AddSingleton<ReadarrClient>();
        builder.Services.AddSingleton<CacheService>();
        builder.Services.AddSingleton<ProxyService>();

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllerRoute(name: "caps",
                pattern: "{options}/{*domain}",
                defaults: new { controller = "Caps", action = "Caps" },
                constraints: new { t = new TRouteConstraint("caps") });

        app.MapControllerRoute(name: "movie-search",
                pattern: "{options}/{*domain}",
                defaults: new { controller = "Search", action = "MovieSearch" },
                constraints: new { t = new TRouteConstraint("movie") });

        app.MapControllerRoute(name: "tv-search",
                pattern: "{options}/{*domain}",
                defaults: new { controller = "Search", action = "TVSearch" },
                constraints: new { t = new TRouteConstraint("tvsearch") });

        app.MapControllerRoute(name: "music-search",
                pattern: "{options}/{*domain}",
                defaults: new { controller = "Search", action = "MusicSearch" },
                constraints: new { t = new TRouteConstraint("music") });

        app.MapControllerRoute(name: "book-search",
               pattern: "{options}/{*domain}",
               defaults: new { controller = "Search", action = "BookSearch" },
               constraints: new { t = new TRouteConstraint("book") });

        app.MapControllerRoute(name: "generic-search",
                pattern: "{options}/{*domain}",
                defaults: new { controller = "Search", action = "GenericSearch" },
                constraints: new { t = new TRouteConstraint("search") });

        app.Run();
    }
}