using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NrgId.EnJson.Translations
{
    /// <summary>
    ///     DI registration extensions for EnJson translations.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        internal const string EnJsonHttpClientName = "EnJsonTranslations";

        /// <summary>
        ///     Registers EnJson translations with memory cache and HTTP client support.
        /// </summary>
        public static IServiceCollection AddEnJsonTranslations(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddMemoryCache();

            services.AddOptions<EnJsonTranslationsOptions>()
                .Bind(configuration.GetSection(EnJsonTranslationsOptions.SectionName));

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

            return services;
        }

        /// <summary>
        ///     Registers EnJson translations with programmatic configuration.
        /// </summary>
        public static IServiceCollection AddEnJsonTranslations(
            this IServiceCollection services,
            Action<EnJsonTranslationsOptions> configure)
        {
            services.AddMemoryCache();

            services.AddOptions<EnJsonTranslationsOptions>()
                .Configure(configure);

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

            return services;
        }
    }
}