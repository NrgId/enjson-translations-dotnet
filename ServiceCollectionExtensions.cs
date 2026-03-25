using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NrgId.EnJson.Translations.Config;
using NrgId.EnJson.Translations.Interfaces;
using NrgId.EnJson.Translations.Services;

namespace NrgId.EnJson.Translations;

/// <summary>
///     DI registration extensions for EnJson translations.
/// </summary>
public static class ServiceCollectionExtensions
{
    internal const string EnJsonHttpClientName = "EnJsonTranslations";

    /// <summary>
    /// Registers EnJson translations using configuration set.
    /// </summary>
    public static IServiceCollection AddEnJsonTranslations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EnJsonTranslationsOptions>()
            .Bind(configuration.GetSection(EnJsonTranslationsOptions.SectionName));

        AddServices(services);

        return services;
    }

    /// <summary>
    /// Registers EnJson translations using already instantiated options object.
    /// </summary>
    public static IServiceCollection AddEnJsonTranslations(
        this IServiceCollection services,
        EnJsonTranslationsOptions options)
    {
        services.AddSingleton(Options.Create(options));

        AddServices(services);

        return services;
    }

    /// <summary>
    /// Registers EnJson translations with programmatic configuration.
    /// </summary>
    public static IServiceCollection AddEnJsonTranslations(
        this IServiceCollection services,
        Action<EnJsonTranslationsOptions> configure)
    {
        services.AddOptions<EnJsonTranslationsOptions>()
            .Configure(configure);

        AddServices(services);

        return services;
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddHttpClient(EnJsonHttpClientName, (sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<EnJsonTranslationsOptions>>().Value;
            http.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);
        });

        services.AddSingleton<IEnJsonUsageTracker, EnJsonUsageTracker>();

        services.AddHttpClient<IEnJsonTranslationProvider, EnJsonTranslationProvider>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<EnJsonTranslationsOptions>>().Value;
            http.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);
        });
    }
}