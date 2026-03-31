namespace NrgId.EnJson.Translations.Services.Results;

/// <example>
/// <code>
/// {
///  "id": 110,
///  "isMain": true,
///  "code": "en",
///  "googleCode": null,
///  "internationalName": "English",
///  "nativeName": "English",
///  "isActive": true
/// }
/// </code>
/// </example>
public record EnJsonLanguage
{
	/// <summary>
	/// Internal id
	/// </summary>
	public required int Id { get; init; }

	/// <summary>
	/// Only one language can be main
	/// </summary>
	public required bool IsMain { get; init; }

	/// <summary>
	/// Language code assigned at creation.
	/// </summary>
	/// <example>
	///	"fr"
	/// </example>
	public required string Code { get; init; }

	/// <summary>
	/// Code for compatability with Google Translate API's.
	/// Optional.
	/// </summary>
	/// <example>
	///	"fr"
	/// </example>
	public string? GoogleCode { get; init; }

	/// <summary>
	/// Internationally understandable language name. Generally, in English.
	/// </summary>
	/// <example>
	///	"French"
	/// </example>
	public required string InternationalName { get; init; }

	/// <summary>
	/// Name of the language in that language. Prefer these when showing UI language options.
	/// </summary>
	/// <example>
	///	"Français"
	/// </example>
	public required string NativeName { get; init; }

	/// <summary>
	/// Will always be true, unless fetching inactive languages with `includeInactive`
	/// </summary>
	public required bool IsActive { get; init; }
}
