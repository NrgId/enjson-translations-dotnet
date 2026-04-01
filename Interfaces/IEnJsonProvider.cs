using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NrgId.EnJson.Translations.Services.Results;

namespace NrgId.EnJson.Translations.Interfaces;

/// <summary>
/// Provides API functionality from enjson.com.
/// </summary>
public interface IEnJsonProvider
{
	/// <summary>
	/// Gets a list of <see cref="EnJsonLanguage"/>s added to the project
	/// </summary>
	/// <param name="includeInactive">If set, inactive languages are returned also</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>If list of languages.</returns>
	Task<List<EnJsonLanguage>?> GetLanguagesAsync(
		bool includeInactive = false,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Gets a translation for the specified language and key.
	/// If translation is not found in the remote copy, looks for a match in fallback file.
	/// Reports last used.
	/// </summary>
	/// <param name="language">What language to fetch translation for</param>
	/// <param name="key"> Key to translate</param>
	/// <param name="customGroup">If provided, will fetch translations with this customGroup</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <example>
	/// <code>
	/// GetTranslationAsync("en-GB", "login.form.email.placeholder");
	/// </code>
	/// </example>
	/// <returns>If found, string - the translation. If not found - <c>null</c>.</returns>
	Task<string?> GetTranslationAsync(
		string language,
		string key,
		string? customGroup = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Gets all translations for this language.
	/// Doesn't report last used.
	/// </summary>
	/// <param name="language">What language to fetch translations for</param>
	/// <param name="namespace">If provided, will fetch only this section of the translations. Only top-level namespaces supported as of now.</param>
	/// <param name="customGroup">If provided, will fetch translations with this customGroup</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns><c>null</c> if API is unavailable/misconfigured AND fallback file(s) were not found</returns>
	Task<JsonObject?> GetTranslationsAsync(
		string language,
		string? @namespace,
		string? customGroup = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Posts last use statistics. Doesn't use batching or it's settings, just makes a direct request.
	/// Intended to be used manually if relying on <see cref="GetTranslationsAsync"/>.
	/// </summary>
	/// <param name="translationKeys">Set of translation full translation keys to mark as used</param>
	/// <param name="customGroup">If provided, will mark translations in this customGroup</param>
	Task PostLastUsedAsync(IEnumerable<string> translationKeys, string? customGroup);
}
