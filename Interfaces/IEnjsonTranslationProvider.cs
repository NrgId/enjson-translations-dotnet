using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NrgId.EnJson.Translations.Interfaces;

/// <summary>
///     Provides translations from enjson.com.
/// </summary>
public interface IEnJsonTranslationProvider
{
    /// <summary>
    ///     Gets a translation for the specified locale and key asynchronously.
    /// </summary>
    Task<string?> GetTranslationAsync(
        string key,
        string locale,
        string? customGroup = null,
        string? cacheNamespace = null,
        Dictionary<string, string>? format = null,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Gets a translation for the specified locale and key synchronously.
    /// </summary>
    string? GetTranslation(
        string key,
        string locale,
        string? customGroup = null,
        string? cacheNamespace = null,
        Dictionary<string, string>? format = null
    );
}