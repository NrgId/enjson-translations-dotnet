using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NrgId.EnJson.Translations.Services.Results;

namespace NrgId.EnJson.Translations.Interfaces;

/// <summary>
///     Provides translations from enjson.com.
/// </summary>
public interface IEnJsonTranslationProvider
{
    /// <summary>
    /// Gets a list of <see cref="EnJsonLanguage"/>s added to the project
    /// </summary>
    Task<List<EnJsonLanguage>?> GetLanguagesAsync(bool includeInactive, CancellationToken cancellationToken);
    
    /// <summary>
    ///     Gets a translation for the specified locale and key asynchronously.
    /// </summary>
    Task<string?> GetTranslationAsync(
        string locale,
        string key,
        string? customGroup = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Gets all translations for the namespace, merged with local fallbacks if available,
    /// but doesn't report usage. For now, only top-level namespace is supported.
    /// </summary>
    Task<JsonObject?> GetNamespaceAsync(
        string locale, 
        string @namespace, 
        string? customGroup,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Posts last use statistics. Doesn't use batching or it's settings, just makes a direct request.
    /// Intended to be used manually if relying on <see cref="GetNamespaceAsync"/>.
    /// </summary>
    Task PostLastUsedAsync(IEnumerable<string> translationKeys, string? customGroup);
}