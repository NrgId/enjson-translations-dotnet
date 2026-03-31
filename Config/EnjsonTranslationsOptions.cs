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
	///     If true, sets it for network requests, and expects nested fallback files
	/// </summary>
	public bool Nested { get; set; } = true;

	/// <summary>
	/// Fallback configuration.
	/// </summary>
	public FallbackSection Fallback { get; set; } = new();

	/// <summary>
	/// Fallback configuration options.
	/// </summary>
	public record FallbackSection
	{
		/// <summary>
		/// Default locale used if translations not found in provided language.
		/// </summary>
		public string Language { get; set; } = "en";

		/// <summary>
		/// <para>
		/// If not set, local files would only be accessed if enjson API is not available.
		/// </para>
		/// <para>
		///	If set, enjson API response will be merged with this language's local file, and then with fallback language's local file.
		/// </para>
		/// </summary>
		public bool AlwaysMerge { get; set; } = false;

		/// <summary>
		/// <para>
		/// Local fallback file paths (e.g. Resources/en.json). Keys are languages, values are paths to .json files. Optional.
		/// </para>
		/// </summary>
		/// <example>
		/// <code>
		/// "LocalFallbackPaths": {
		///   "en": "Resources/en.json",
		///   "fr": "Resources/fr.json"
		/// },
		/// </code>
		/// </example>
		public Dictionary<string, string> LocalPaths { get; set; } = [];
	}

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
