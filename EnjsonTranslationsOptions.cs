namespace NrgId.EnJson.Translations
{
    /// <summary>
    /// Configuration options for Enjson translations.
    /// </summary>
    public class EnjsonTranslationsOptions
    {
        /// <summary>
        /// Configuration section name for Enjson translations.
        /// </summary>
        public const string SectionName = "EnjsonTranslations";

        /// <summary>
        /// Base API URL for the Enjson service.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.enjson.com";

        /// <summary>
        /// Enjson Project ID.
        /// </summary>
        public string ProjectId { get; set; } = "";

        /// <summary>
        /// Cache duration in minutes.
        /// </summary>
        public int CacheMinutes { get; set; } = 5;

        /// <summary>
        /// HTTP timeout in seconds.
        /// </summary>
        public int HttpTimeoutSeconds { get; set; } = 20;

        /// <summary>
        /// Default locale used when none is provided.
        /// </summary>
        public string DefaultLocale { get; set; } = "en";

        /// <summary>
        /// Namespace depth used when parsing keys like "emails.user.created".
        /// </summary>
        public int NamespaceDepth { get; set; } = 1;

        /// <summary>
        /// Optional local fallback file path (e.g. Resources/en.json).
        /// </summary>
        public string? LocalFallbackPath { get; set; }

        /// <summary>
        /// Enables usage tracking to update last-used timestamps.
        /// </summary>
        public bool EnableUsageTracking { get; set; } = true;

        /// <summary>
        /// Usage reporting interval in minutes.
        /// </summary>
        public int UsageReportIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Max number of keys per usage reporting batch.
        /// </summary>
        public int UsageReportBatchSize { get; set; } = 200;
    }
}
