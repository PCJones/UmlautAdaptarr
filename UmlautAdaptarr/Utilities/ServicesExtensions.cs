using System.Linq.Expressions;
using UmlautAdaptarr.Interfaces;
using UmlautAdaptarr.Options;
using UmlautAdaptarr.Options.ArrOptions.InstanceOptions;
using UmlautAdaptarr.Providers;
using UmlautAdaptarr.Services;

namespace UmlautAdaptarr.Utilities;

/// <summary>
///     Extension methods for configuring services related to ARR Applications
/// </summary>
public static class ServicesExtensions
{
    /// <summary>
    ///     Adds a service with specified options and service to the service collection.
    /// </summary>
    /// <typeparam name="TOptions">The options type for the service.</typeparam>
    /// <typeparam name="TService">The service type for the service.</typeparam>
    /// <typeparam name="TInterface">The Interface of the service type</typeparam>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to configure the service collection.</param>
    /// <param name="sectionName">The name of the configuration section containing service options.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder" />.</returns>
    private static WebApplicationBuilder AddServicesWithOptions<TOptions, TService, TInterface>(
        this WebApplicationBuilder builder, string sectionName)
        where TOptions : class, new()
        where TService : class, TInterface
        where TInterface : class
    {
        if (builder.Services == null) throw new ArgumentNullException(nameof(builder), "Service collection is null.");


        var singleInstance = builder.Configuration.GetSection(sectionName).Get<TOptions>();

        var singleHost = (string?)typeof(TOptions).GetProperty("Host")?.GetValue(singleInstance, null);

        // If we have no Single Instance , we try to parse for a Array
        var optionsArray = singleHost == null
            ? builder.Configuration.GetSection(sectionName).Get<TOptions[]>()
            :
            [
                singleInstance
            ];

        if (optionsArray == null || !optionsArray.Any())
            throw new InvalidOperationException(
                $"{typeof(TService).Name} options could not be loaded from Configuration or ENV Variable.");

        foreach (var option in optionsArray)
        {
            var instanceState = (bool)(typeof(TOptions).GetProperty("Enabled")?.GetValue(option, null) ?? false);

            // We only want to create instances that are enabled in the Configs
            if (instanceState)
            {
                // User can give the Instance a readable Name otherwise we use the Host Property
                var instanceName = (string)(typeof(TOptions).GetProperty("Name")?.GetValue(option, null) ??
                                            (string)typeof(TOptions).GetProperty("Host")?.GetValue(option, null)!);

                // Dark Magic , we don't know the Property's of TOptions , and we won't cast them for each Options
                // Todo eventuell schönere Lösung finden
                var paraexpression = Expression.Parameter(Type.GetType(option.GetType().FullName), "x");

                foreach (var prop in option.GetType().GetProperties())
                {
                    var val = Expression.Constant(prop.GetValue(option));
                    var memberexpression = Expression.PropertyOrField(paraexpression, prop.Name);

                    if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(bool))
                    {
                        var assign = Expression.Assign(memberexpression, Expression.Convert(val, prop.PropertyType));
                        var exp = Expression.Lambda<Action<TOptions>>(assign, paraexpression);
                        builder.Services.Configure(instanceName, exp.Compile());
                    }
                    else
                    {
                        Console.WriteLine(prop.PropertyType + "No Support");
                    }
                }


                builder.Services.AllowResolvingKeyedServicesAsDictionary();
                builder.Services.AddKeyedSingleton<TInterface, TService>(instanceName);
            }
        }

        return builder;
    }

    /// <summary>
    ///     Adds a service with specified options and service to the service collection.
    /// </summary>
    /// <typeparam name="TOptions">The options type for the service.</typeparam>
    /// <typeparam name="TService">The service type for the service.</typeparam>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to configure the service collection.</param>
    /// <param name="sectionName">The name of the configuration section containing service options.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder" />.</returns>
    private static WebApplicationBuilder AddServiceWithOptions<TOptions, TService>(this WebApplicationBuilder builder,
        string sectionName)
        where TOptions : class
        where TService : class
    {
        if (builder.Services == null) throw new ArgumentNullException(nameof(builder), "Service collection is null.");

        var options = builder.Configuration.GetSection(sectionName).Get<TOptions>();
        if (options == null)
            throw new InvalidOperationException(
                $"{typeof(TService).Name} options could not be loaded from Configuration or ENV Variable.");

        builder.Services.Configure<TOptions>(builder.Configuration.GetSection(sectionName));
        builder.Services.AddSingleton<TService>();

        return builder;
    }

    /// <summary>
    ///     Adds support for Sonarr with default options and client.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to configure the service collection.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder" />.</returns>
    public static WebApplicationBuilder AddSonarrSupport(this WebApplicationBuilder builder)
    {
        //  builder.Serviceses.AddSingleton<IOptionsMonitoSonarrInstanceOptionsns>, OptionsMonitoSonarrInstanceOptionsns>>();
        return builder.AddServicesWithOptions<SonarrInstanceOptions, SonarrClient, IArrApplication>("Sonarr");
    }

    /// <summary>
    ///     Adds support for Lidarr with default options and client.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to configure the service collection.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder" />.</returns>
    public static WebApplicationBuilder AddLidarrSupport(this WebApplicationBuilder builder)
    {
        return builder.AddServicesWithOptions<LidarrInstanceOptions, LidarrClient, IArrApplication>("Lidarr");
    }

    /// <summary>
    ///     Adds support for Readarr with default options and client.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to configure the service collection.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder" />.</returns>
    public static WebApplicationBuilder AddReadarrSupport(this WebApplicationBuilder builder)
    {
        return builder.AddServicesWithOptions<ReadarrInstanceOptions, ReadarrClient, IArrApplication>("Readarr");
    }

    /// <summary>
    ///     Adds a title lookup service to the service collection.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to configure the service collection.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder" />.</returns>
    public static WebApplicationBuilder AddTitleLookupService(this WebApplicationBuilder builder)
    {
        return builder.AddServiceWithOptions<GlobalOptions, TitleApiService>("Settings");
    }

    /// <summary>
    ///     Adds a proxy request service to the service collection.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to configure the service collection.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder" />.</returns>
    public static WebApplicationBuilder AddProxyRequestService(this WebApplicationBuilder builder)
    {
        return builder.AddServiceWithOptions<GlobalOptions, ProxyRequestService>("Settings");
    }
}