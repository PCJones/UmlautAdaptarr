using System.Net;
using UmlautAdaptarr.Routing;
using UmlautAdaptarr.Services;

internal class Program
{
    private static void Main(string[] args)
    {
        // TODO:
        // add option to sort by nzb age

        // TODO
        // add delay between requests

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
            options.SizeLimit = 500;
        });
        builder.Services.AddSingleton<ILogger, Logger<TitleQueryService>>();

        builder.Services.AddControllers();
        builder.Services.AddScoped<TitleQueryService>();
        builder.Services.AddScoped<ProxyService>();
        builder.Services.AddScoped<TitleMatchingService>();

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