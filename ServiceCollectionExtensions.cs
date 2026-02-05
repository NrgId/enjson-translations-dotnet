using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NrgId.EnJson.Translations
{
    /// <summary>
    /// DI registration extensions for Enjson translations.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        internal const string EnjsonHttpClientName = "EnjsonTranslations";

        /// <summary>
        /// Registers Enjson translations with memory cache and HTTP client support.
        /// </summary>
        public static IServiceCollection AddEnjsonTranslations(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddMemoryCache();

            services.AddOptions<EnjsonTranslationsOptions>()
                .Bind(configuration.GetSection(EnjsonTranslationsOptions.SectionName));

            services.AddHttpClient(EnjsonHttpClientName, (sp, http) =>
            {
                var opt = sp.GetRequiredService<IOptions<EnjsonTranslationsOptions>>().Value;
                http.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);
            });

            services.AddSingleton<IEnjsonUsageTracker, EnjsonUsageTracker>();

            services.AddHttpClient<IExternalTranslationProvider, EnjsonTranslationProvider>((sp, http) =>
            {
                var opt = sp.GetRequiredService<IOptions<EnjsonTranslationsOptions>>().Value;
                http.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);
            });

            return services;
        }

        /// <summary>
        /// Registers Enjson translations with programmatic configuration.
        /// </summary>
        public static IServiceCollection AddEnjsonTranslations(
            this IServiceCollection services,
            Action<EnjsonTranslationsOptions> configure)
        {
            services.AddMemoryCache();

            services.AddOptions<EnjsonTranslationsOptions>()
                .Configure(configure);

            services.AddHttpClient(EnjsonHttpClientName, (sp, http) =>
            {
                var opt = sp.GetRequiredService<IOptions<EnjsonTranslationsOptions>>().Value;
                http.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);
            });

            services.AddSingleton<IEnjsonUsageTracker, EnjsonUsageTracker>();

            services.AddHttpClient<IExternalTranslationProvider, EnjsonTranslationProvider>((sp, http) =>
            {
                var opt = sp.GetRequiredService<IOptions<EnjsonTranslationsOptions>>().Value;
                http.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);
            });

            return services;
        }
    }
}
