using System.Net;
using Serilog;
using Serilog.Filters;
using UmlautAdaptarr.Options;
using UmlautAdaptarr.Routing;
using UmlautAdaptarr.Services;
using UmlautAdaptarr.Services.Factory;
using UmlautAdaptarr.Utilities;

internal class Program
{
    private static void Main(string[] args)
    {
        Helper.ShowLogo();
        Helper.ShowInformation();
        // TODO:
        // add option to sort by nzb age
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        // TODO workaround to not log api keys
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .Filter.ByExcluding(Matching.FromSource("System.Net.Http.HttpClient"))
            .Filter.ByExcluding(Matching.FromSource("Microsoft.Extensions.Http.DefaultHttpClientFactory"))
            //.Enrich.With(new ApiKeyMaskingEnricher("appsettings.json")) // TODO - Not working currently
            .CreateLogger();


        builder.Services.AddSerilog();

        // Add services to the container.
        builder.Services.AddHttpClient("HttpClient").ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate |
                                         DecompressionMethods.Brotli
            };

            return handler;
        });

        builder.Services.AddMemoryCache(options =>
        {
            // TODO cache size limit? option?
            //options.SizeLimit = 20000;
        });

        builder.Services.AllowResolvingKeyedServicesAsDictionary();
        builder.Services.AddControllers();
        builder.AddTitleLookupService();
        builder.Services.AddSingleton<SearchItemLookupService>();
        builder.Services.AddSingleton<TitleMatchingService>();
        builder.AddSonarrSupport();
        builder.AddLidarrSupport();
        builder.AddReadarrSupport();
        builder.Services.AddSingleton<CacheService>();
        builder.Services.AddSingleton<ProxyRequestService>();
        builder.Services.AddSingleton<ArrApplicationFactory>();
        builder.Services.AddHostedService<ArrSyncBackgroundService>();
        builder.Services.AddSingleton<IHostedService, HttpProxyService>();

        var app = builder.Build();

        GlobalStaticLogger.Initialize(app.Services.GetService<ILoggerFactory>()!);
        app.UseHttpsRedirection();
        app.UseAuthorization();

        app.MapControllerRoute("caps",
            "{options}/{*domain}",
            new { controller = "Caps", action = "Caps" },
            new { t = new TRouteConstraint("caps") });

        app.MapControllerRoute("movie-search",
            "{options}/{*domain}",
            new { controller = "Search", action = "MovieSearch" },
            new { t = new TRouteConstraint("movie") });

        app.MapControllerRoute("tv-search",
            "{options}/{*domain}",
            new { controller = "Search", action = "TVSearch" },
            new { t = new TRouteConstraint("tvsearch") });

        app.MapControllerRoute("music-search",
            "{options}/{*domain}",
            new { controller = "Search", action = "MusicSearch" },
            new { t = new TRouteConstraint("music") });

        app.MapControllerRoute("book-search",
            "{options}/{*domain}",
            new { controller = "Search", action = "BookSearch" },
            new { t = new TRouteConstraint("book") });

        app.MapControllerRoute("generic-search",
            "{options}/{*domain}",
            new { controller = "Search", action = "GenericSearch" },
            new { t = new TRouteConstraint("search") });
        app.Run();
    }
}