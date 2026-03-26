using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
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
internal class EnJsonTranslationProvider : IEnJsonTranslationProvider
{
    private readonly IMemoryCache _cache;
    private readonly EnJsonHttpClient _enJsonHttpClient;
    private readonly EnJsonTranslationsOptions _options;
    private readonly IEnJsonUsageTracker _usageTracker;
    private readonly IEnJsonErrorListener _errorListener;
    

    private const string CachePrefix = "enjson";
    private readonly MemoryCacheEntryOptions _memoryCacheEntryOptions;

    /// <summary>
    ///     Creates a new translation provider.
    /// </summary>
    public EnJsonTranslationProvider(
        IMemoryCache cache,
        IOptions<EnJsonTranslationsOptions> options, 
        IEnJsonUsageTracker usageTracker,
        EnJsonHttpClient enJsonHttpClient,
        IEnJsonErrorListener errorListener
    )
    {
        _enJsonHttpClient = enJsonHttpClient;
        _cache = cache;
        _options = options.Value;
        _usageTracker = usageTracker;
        _errorListener = errorListener;
        _memoryCacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes),
        };

        foreach (var fallbackPath in _options.LocalFallbackPaths)
            if (!string.IsNullOrWhiteSpace(fallbackPath.Value) && !File.Exists(fallbackPath.Value))
                throw new ArgumentException(ErrorMessages.EnJsonFallbackNotFound, nameof(_options.LocalFallbackPaths));
    }

    public Task<List<EnJsonLanguage>?> GetLanguagesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return _enJsonHttpClient.GetLanguagesAsync(includeInactive, cancellationToken);
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
        string? @namespace, string? customGroup, CancellationToken cancellationToken)
    {
        var namespaceKey = string.IsNullOrWhiteSpace(@namespace) ? "root" : @namespace;
        var customGroupKey = string.IsNullOrWhiteSpace(customGroup) ? "global" : customGroup;
        var cacheKey = $"{CachePrefix}:{_options.ProjectId}:{locale}:{namespaceKey}:{customGroupKey}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached != null)
            return cached;

        try
        {
            var dict = await _enJsonHttpClient.GetTranslationsAsync<IReadOnlyDictionary<string, string>>(locale, @namespace, customGroup, false, cancellationToken);
            if (dict != null)
            {
                _cache.Set(cacheKey, dict, _memoryCacheEntryOptions);
            }

            return dict ?? new Dictionary<string, string>();
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

    private bool TryGetLocalFallbackValue(string key, string locale, out string? value)
    {
        value = null;

        _options.LocalFallbackPaths.TryGetValue(locale, out var localFallBackPath);

        if (string.IsNullOrWhiteSpace(localFallBackPath))
            return false;

        var cacheKey = $"{CachePrefix}:fallback:{localFallBackPath}";
        if (!_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) || cached == null)
            try
            {
                var json = File.ReadAllText(localFallBackPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                           ?? new Dictionary<string, string>();
                cached = dict;
                _cache.Set(cacheKey, cached, _memoryCacheEntryOptions);
            }
            catch (Exception ex)
            {
                _errorListener.OnError(ErrorSources.TranslationFallBackProvider, null, ex, null);
                return false;
            }

        return cached.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
    }

    private JsonObject? LoadLocalFile(string locale, string @namespace)
    {
        _options.LocalFallbackPaths.TryGetValue(locale, out var localFallbackPath);
        if (string.IsNullOrWhiteSpace(localFallbackPath))
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText(localFallbackPath);
            var jsonNode = JsonNode.Parse(text);
            var jsonObject = jsonNode?.AsObject();
            if (jsonObject == null)
            {
                return null;
            }
            var found = jsonObject.TryGetPropertyValue(@namespace, out var node);
            if (!found)
            {
                return null;
            }

            return node?.AsObject();
        }
        catch (Exception e)
        {
            _errorListener.OnError(ErrorSources.TranslationFallBackProvider, null, e, null);
            return null;
        }
    }

    private async Task<(JsonObject? dict, bool cacheable)> GetNamespaceCoreAsync(
        string locale, 
        string @namespace, 
        string? customGroup,
        CancellationToken cancellationToken
    )
    {
        var localDict = LoadLocalFile(locale, @namespace);
        
        try
        {
            var remoteDict = await _enJsonHttpClient.GetTranslationsAsync<JsonObject>(locale, @namespace, customGroup, true, cancellationToken);
            if (remoteDict == null)
            {
                return (localDict, false);
            }

            return (DeepMerge(localDict, remoteDict), true);
        }
        catch (Exception e)
        {
            _errorListener.OnError(ErrorSources.TranslationProvider, null, e, null);
            return (null, false);
        }
    }

    /// <inheritdoc />
    public async Task<JsonObject?> GetNamespaceAsync(
        string locale,
        string @namespace,
        string? customGroup,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            throw new ArgumentException(ErrorMessages.EnJsonMissingLocale, nameof(locale));
        }

        if (string.IsNullOrWhiteSpace(@namespace))
        {
            throw new ArgumentException(ErrorMessages.EnJsonKeyMissingNamespace, nameof(@namespace));
        }
        
        var cacheKey = $"{CachePrefix}:locale:{locale}:namespace:{@namespace}:customGroup:{customGroup}";
        var found = _cache.TryGetValue<JsonObject>(cacheKey, out var cached);
        if (found && cached != null)
        {
            return cached;
        }

        var (dict, cacheable) = await GetNamespaceCoreAsync(locale, @namespace, customGroup, cancellationToken);
        if (dict == null)
        {
            return null;
        }

        if (cacheable)
        {
            _cache.Set(cacheKey, dict, _memoryCacheEntryOptions);
        }

        return dict;
    }
    
    /// <summary>
    /// Not pure, returns modified older, newer gets used up and becomes invalid.
    /// </summary>
    private static JsonObject? DeepMerge(JsonObject? older, JsonObject? newer) {
        if (older is null)
        {
            return newer;
        }
        if (newer is null)
        {
            return older;
        }
        
        // to list needed to detach from newer
        foreach (var kv in newer.ToList())
        {
            if (!older.ContainsKey(kv.Key))
            {
                // not found, just move
                newer.Remove(kv.Key); // must be detached from newer first
                older[kv.Key] = kv.Value;
                continue;
            }
            
            // merge them
            
            var oldValue = older[kv.Key];
            var newValue = kv.Value;

            if (oldValue is JsonObject oldObj && newValue is JsonObject newObj)
            {
                older[kv.Key] = DeepMerge(oldObj, newObj);
            }
            else
            {
                newer.Remove(kv.Key);
                older[kv.Key] = newValue;
            }
        }

        return older;
    }
}