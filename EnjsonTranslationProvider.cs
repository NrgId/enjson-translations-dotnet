using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace NrgId.EnJson.Translations
{
    /// <summary>
    /// Enjson implementation of <see cref="IExternalTranslationProvider"/>.
    /// </summary>
    public class EnjsonTranslationProvider : IExternalTranslationProvider
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly EnjsonTranslationsOptions _options;
        private readonly IEnjsonUsageTracker _usageTracker;

        /// <summary>
        /// Creates a new translation provider.
        /// </summary>
        public EnjsonTranslationProvider(
            HttpClient http,
            IMemoryCache cache,
            IOptions<EnjsonTranslationsOptions> options,
            IEnjsonUsageTracker usageTracker)
        {
            _http = http;
            _cache = cache;
            _options = options.Value;
            _usageTracker = usageTracker;

            if (!string.IsNullOrWhiteSpace(_options.LocalFallbackPath) && !System.IO.File.Exists(_options.LocalFallbackPath))
                throw new ArgumentException("enjson_fallback_not_found", nameof(_options.LocalFallbackPath));
        }

        /// <inheritdoc />
        public async Task<EnjsonTranslationResult> GetTranslationResultAsync(
            string key,
            string locale,
            string? customGroup = null,
            string? cacheNamespace = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(locale))
                throw new ArgumentException("enjson_missing_locale", nameof(locale));

            if (string.IsNullOrWhiteSpace(key))
                return new EnjsonTranslationResult(string.Empty, null, false);

            if (string.IsNullOrWhiteSpace(_options.ProjectId))
                throw new ArgumentException("enjson_missing_project_id", nameof(_options.ProjectId));

            var hasInlineNamespace = TrySplitKey(key, _options.NamespaceDepth, out var parsedNamespace, out var parsedKey);
            if (!string.IsNullOrWhiteSpace(cacheNamespace))
            {
                if (!hasInlineNamespace)
                    throw new ArgumentException("enjson_key_missing_namespace", nameof(key));

                if (!string.Equals(parsedNamespace, cacheNamespace, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("enjson_namespace_mismatch", nameof(key));
            }

            var effectiveNamespace = cacheNamespace ?? (hasInlineNamespace ? parsedNamespace : null);

            var fullKeyForTracking = key;

            _usageTracker.Track(fullKeyForTracking);

            IReadOnlyDictionary<string, string> dict;
            try
            {
                dict = await GetTranslationsAsync(locale, effectiveNamespace, customGroup, ct).ConfigureAwait(false);
            }
            catch
            {
                if (TryGetLocalFallbackValue(fullKeyForTracking, out var localValue))
                    return new EnjsonTranslationResult(fullKeyForTracking, localValue, true);

                return new EnjsonTranslationResult(fullKeyForTracking, null, false, "enjson_request_failed");
            }

            if (dict.TryGetValue(fullKeyForTracking, out var value) && !string.IsNullOrWhiteSpace(value))
                return new EnjsonTranslationResult(fullKeyForTracking, value, true);

            if (TryGetLocalFallbackValue(fullKeyForTracking, out var fallbackValue))
                return new EnjsonTranslationResult(fullKeyForTracking, fallbackValue, true);

            return new EnjsonTranslationResult(fullKeyForTracking, null, false);
        }

        /// <inheritdoc />
        public async Task<string?> GetTranslationAsync(
            string key,
            string locale,
            string? customGroup = null,
            string? cacheNamespace = null,
            CancellationToken ct = default)
        {
            var result = await GetTranslationResultAsync(key, locale, customGroup, cacheNamespace, ct).ConfigureAwait(false);
            return result.Found ? result.Value : result.Key;
        }

        private async Task<IReadOnlyDictionary<string, string>> GetTranslationsAsync(
            string locale,
            string? @namespace,
            string? customGroup,
            CancellationToken ct)
        {
            var namespaceKey = string.IsNullOrWhiteSpace(@namespace) ? "root" : @namespace;
            var customGroupKey = string.IsNullOrWhiteSpace(customGroup) ? "global" : customGroup;
            var cacheKey = $"enjson:{_options.ProjectId}:{locale}:{namespaceKey}:{customGroupKey}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached != null)
                return cached;

            var urlBuilder = new StringBuilder();
            urlBuilder.Append(_options.BaseUrl.TrimEnd('/'))
                .Append("/integration/")
                .Append(_options.ProjectId)
                .Append("/translations?language=")
                .Append(Uri.EscapeDataString(locale))
                .Append("&fallbackLanguage=en");
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                urlBuilder.Append("&namespace=").Append(Uri.EscapeDataString(@namespace));
            }
            if (!string.IsNullOrWhiteSpace(customGroup))
            {
                urlBuilder.Append("&customGroup=").Append(Uri.EscapeDataString(customGroup));
            }
            var url = urlBuilder.ToString();

            var dict = await _http.GetFromJsonAsync<Dictionary<string, string>>(url, ct)
                       ?? new Dictionary<string, string>();

            IReadOnlyDictionary<string, string> ro = dict;

            _cache.Set(cacheKey, ro, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes)
            });

            return ro;
        }

        private static bool TrySplitKey(string key, int namespaceDepth, out string? @namespace, out string lookupKey)
        {
            @namespace = null;
            lookupKey = key;

            if (namespaceDepth <= 0)
                return false;

            var parts = key.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
                return false;

            var depth = Math.Min(namespaceDepth, parts.Length - 1);
            @namespace = string.Join(".", parts, 0, depth);
            lookupKey = string.Join(".", parts, depth, parts.Length - depth);
            if (string.IsNullOrWhiteSpace(lookupKey))
            {
                lookupKey = key;
                @namespace = null;
                return false;
            }

            return true;
        }

        private bool TryGetLocalFallbackValue(string key, out string? value)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(_options.LocalFallbackPath))
                return false;

            var cacheKey = $"enjson:fallback:{_options.LocalFallbackPath}";
            if (!_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) || cached == null)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(_options.LocalFallbackPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                               ?? new Dictionary<string, string>();
                    cached = dict;
                    _cache.Set(cacheKey, cached, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes)
                    });
                }
                catch
                {
                    return false;
                }
            }

            return cached.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
        }
    }

}
