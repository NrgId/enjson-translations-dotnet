using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NrgId.EnJson.Translations.Config;
using NrgId.EnJson.Translations.Core;
using NrgId.EnJson.Translations.Interfaces;
using NrgId.EnJson.Translations.Services.Results;

namespace NrgId.EnJson.Translations.Services;

/// <summary>
///     EnJson implementation of <see cref="IEnJsonTranslationProvider" />.
/// </summary>
public class EnJsonTranslationProvider : IEnJsonTranslationProvider
{
    private readonly IMemoryCache _cache;
    private readonly HttpClient _http;
    private readonly EnJsonTranslationsOptions _options;
    private readonly IEnJsonUsageTracker _usageTracker;
    private readonly IEnJsonErrorListener _errorListener;

    /// <summary>
    ///     Creates a new translation provider.
    /// </summary>
    public EnJsonTranslationProvider(
        HttpClient http, 
        IMemoryCache cache,
        IOptions<EnJsonTranslationsOptions> options, 
        IEnJsonUsageTracker usageTracker,
        IEnJsonErrorListener errorListener
    )
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _usageTracker = usageTracker;
        _errorListener = errorListener;

        foreach (var fallbackPath in _options.LocalFallbackPaths)
            if (!string.IsNullOrWhiteSpace(fallbackPath.Value) && !File.Exists(fallbackPath.Value))
                throw new ArgumentException(ErrorMessages.EnJsonFallbackNotFound, nameof(_options.LocalFallbackPaths));

        if (!string.IsNullOrEmpty(_options.ApiKey))
            _http.DefaultRequestHeaders.Add("apiKey", _options.ApiKey);
    }

    /// <inheritdoc />
    public string? GetTranslation(string key, string locale, string? customGroup = null,
        string? cacheNamespace = null)
    {
        var result = GetTranslationResultAsync(key, locale, customGroup, cacheNamespace)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        return result.Found ? result.Value : result.Key;
    }

    /// <inheritdoc />
    public async Task<string?> GetTranslationAsync(string key, string locale, string? customGroup = null,
        string? cacheNamespace = null, CancellationToken ct = default)
    {
        var result = await GetTranslationResultAsync(key, locale, customGroup, cacheNamespace, ct)
            .ConfigureAwait(false);
        return result.Found ? result.Value : result.Key;
    }

    /// <inheritdoc cref="GetTranslationAsync" />
    private async Task<EnJsonTranslationResult> GetTranslationResultAsync(string key, string locale,
        string? customGroup = null, string? cacheNamespace = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException(ErrorMessages.EnJsonMissingLocale, nameof(locale));

        if (string.IsNullOrWhiteSpace(key))
            return new EnJsonTranslationResult(string.Empty, null, false);

        if (string.IsNullOrWhiteSpace(_options.ProjectId))
            throw new ArgumentException(ErrorMessages.EnJsonMissingProjectId, nameof(_options.ProjectId));

        var hasInlineNamespace =
            TrySplitKey(key, _options.NamespaceDepth, out var parsedNamespace, out _);

        if (!string.IsNullOrWhiteSpace(cacheNamespace))
        {
            if (!hasInlineNamespace)
                throw new ArgumentException(ErrorMessages.EnJsonKeyMissingNamespace, nameof(key));

            if (!string.Equals(parsedNamespace, cacheNamespace, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(ErrorMessages.EnJsonNamespaceMismatch, nameof(key));
        }

        var effectiveNamespace = cacheNamespace ?? (hasInlineNamespace ? parsedNamespace : null);

        var fullKeyForTracking = key;

        _usageTracker.Track(fullKeyForTracking);

        IReadOnlyDictionary<string, string> dict;
        try
        {
            dict = await GetTranslationsDictionaryAsync(locale, effectiveNamespace, customGroup, ct)
                .ConfigureAwait(false);
        }
        catch
        {
            return TryGetLocalFallbackValue(fullKeyForTracking, locale, out var localValue)
                ? new EnJsonTranslationResult(fullKeyForTracking, localValue, true)
                : new EnJsonTranslationResult(fullKeyForTracking, null, false, ErrorMessages.EnJsonRequestFailed);
        }

        if (dict.TryGetValue(fullKeyForTracking, out var value) && !string.IsNullOrWhiteSpace(value))
            return new EnJsonTranslationResult(fullKeyForTracking, value, true);

        return TryGetLocalFallbackValue(fullKeyForTracking, locale, out var fallbackValue)
            ? new EnJsonTranslationResult(fullKeyForTracking, fallbackValue, true)
            : new EnJsonTranslationResult(fullKeyForTracking, null, false);
    }


    /// <summary>
    ///     Gets translations dictionary
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> GetTranslationsDictionaryAsync(string locale,
        string? @namespace, string? customGroup, CancellationToken ct)
    {
        var namespaceKey = string.IsNullOrWhiteSpace(@namespace) ? "root" : @namespace;
        var customGroupKey = string.IsNullOrWhiteSpace(customGroup) ? "global" : customGroup;
        var cacheKey = $"enjson:{_options.ProjectId}:{locale}:{namespaceKey}:{customGroupKey}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached != null)
            return cached;

        var builder = new UriBuilder(_options.BaseUrl.TrimEnd('/'));

        builder.Path = $"{builder.Path.TrimEnd('/')}/integration/{_options.ProjectId}/translations";

        var query = HttpUtility.ParseQueryString(string.Empty);

        query["language"] = locale;
        query["fallbackLanguage"] = _options.FallBackLanguage;

        if (!string.IsNullOrWhiteSpace(@namespace))
            query["namespace"] = @namespace;

        if (!string.IsNullOrWhiteSpace(customGroup))
            query["customGroup"] = customGroup;

        builder.Query = query.ToString();

        var uri = builder.Uri;
        try
        {
            var dict = await _http.GetFromJsonAsync<Dictionary<string, string>>(uri, ct)
                       ?? new Dictionary<string, string>();

            IReadOnlyDictionary<string, string> ro = dict;

            _cache.Set(cacheKey, ro, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes)
            });

            return ro;
        }
        catch (Exception e)
        {
            _errorListener.OnError(ErrorSources.TranslationProvider, null, e, null);
            return new Dictionary<string, string>();
        }
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

    private bool TryGetLocalFallbackValue(string key, string language, out string? value)
    {
        value = null;

        _options.LocalFallbackPaths.TryGetValue(language, out var localFallBackPath);

        if (string.IsNullOrWhiteSpace(localFallBackPath))
            return false;

        var cacheKey = $"enjson:fallback:{localFallBackPath}";
        if (!_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) || cached == null)
            try
            {
                var json = File.ReadAllText(localFallBackPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                           ?? new Dictionary<string, string>();
                cached = dict;
                _cache.Set(cacheKey, cached, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes)
                });
            }
            catch (Exception ex)
            {
                _errorListener.OnError(ErrorSources.TranslationFallBackProvider, null, ex, null);
                return false;
            }

        return cached.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
    }
}