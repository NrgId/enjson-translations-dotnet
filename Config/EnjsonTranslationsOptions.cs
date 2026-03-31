using System.Collections.Generic;

namespace NrgId.EnJson.Translations.Config;

/// <summary>
///     Configuration options for EnJson translations.
/// </summary>
public record EnJsonTranslationsOptions
{
	/// <summary>
	///     Configuration section name for EnJson translations.
	/// </summary>
	public const string SectionName = "EnJsonTranslations";

	/// <summary>
	///     Base API URL for the EnJson service.
	/// </summary>
	public string BaseUrl { get; set; } = "https://api.EnJson.com";

	/// <summary>
	///     EnJson Project ID.
	/// </summary>
	public string ProjectId { get; set; } = "";

	/// <summary>
	///     API key for EnJson secured APIs.
	/// </summary>
	public string ApiKey { get; set; } = "";

	/// <summary>
	///     Cache duration in minutes.
	/// </summary>
	public int CacheMinutes { get; set; } = 5;

	/// <summary>
	///     HTTP timeout in seconds.
	/// </summary>
	public int HttpTimeoutSeconds { get; set; } = 20;

	/// <summary>
	///     Default locale used when none is provided.
	/// </summary>
	public string FallBackLanguage { get; set; } = "en";

	/// <summary>
	///     If true, sets it for network requests, and expects nested fallback files
	/// </summary>
	public bool Nested { get; set; } = true;

	/// <summary>
	/// <para>
	/// Local fallback file paths (e.g. Resources/en.json). Optional.
	/// </para>
	/// <para>
	/// Keys are languages, values are paths to .json files.
	/// </para>
	/// <example>
	/// <code>
	/// "LocalFallbackPaths": {
	///   "en": "Resources/en.json",
	///   "fr": "Resources/fr.json"
	/// },
	/// </code>
	/// </example>
	/// </summary>
	public Dictionary<string, string> LocalFallbackPaths { get; set; } = [];

	/// <summary>
	/// Usage tracking configuration.
	/// </summary>
	public UsageTrackingSection UsageTracking { get; set; } = new();

	/// <summary>
	/// Usage tracking configuration options
	/// </summary>
	public record UsageTrackingSection
	{
		/// <summary>
		///     Enables usage tracking to update last-used timestamps.
		/// </summary>
		public bool Enabled { get; set; } = true;

		/// <summary>
		///     Usage reporting interval in minutes.
		/// </summary>
		public int ReportIntervalMinutes { get; set; } = 5;

		/// <summary>
		///     Max number of keys per usage reporting batch.
		/// </summary>
		public int ReportBatchSize { get; set; } = 200;
	}
}
