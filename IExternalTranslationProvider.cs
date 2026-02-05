using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NrgId.EnJson.Translations
{
    /// <summary>
    /// Provides translations from enjson.com.
    /// </summary>
    public interface IExternalTranslationProvider
    {
        /// <summary>
        /// Gets a translation and result metadata.
        /// </summary>
        Task<EnjsonTranslationResult> GetTranslationResultAsync(
            string key,
            string locale,
            string? customGroup = null,
            string? cacheNamespace = null,
            CancellationToken ct = default
        );

        /// <summary>
        /// Gets a translation for the specified locale and key.
        /// </summary>
        Task<string?> GetTranslationAsync(
            string key,
            string locale,
            string? customGroup = null,
            string? cacheNamespace = null,
            CancellationToken ct = default
        );
    }
}
