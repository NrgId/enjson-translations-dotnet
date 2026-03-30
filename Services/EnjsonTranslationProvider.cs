using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        if (string.IsNullOrWhiteSpace(_options.ProjectId))
            throw new ArgumentException(ErrorMessages.EnJsonMissingProjectId, nameof(_options.ProjectId));

        foreach (var fallbackPath in _options.LocalFallbackPaths)
            if (!string.IsNullOrWhiteSpace(fallbackPath.Value) && !File.Exists(fallbackPath.Value))
                throw new ArgumentException(ErrorMessages.EnJsonFallbackNotFound, nameof(_options.LocalFallbackPaths));
    }

    public Task<List<EnJsonLanguage>?> GetLanguagesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CachePrefix}:{_options.ProjectId}:languages:{includeInactive}";
     
        return GetTroughCacheAsync(
            cacheKey,
            async () =>
            {
                var languages = await _enJsonHttpClient.GetLanguagesAsync(includeInactive, cancellationToken);
                return (languages, true);
            }
        );
    }

    /// <inheritdoc />
    public Task<string?> GetTranslationAsync(string locale, string key, string? customGroup = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            throw new ArgumentException(ErrorMessages.EnJsonMissingLocale, nameof(locale));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<string?>(null);
        }

        _usageTracker.Track(key);
        
        var cacheKey = $"{CachePrefix}:{_options.ProjectId}:translation:locale:{locale}:key:{key}:customGroup:{customGroup}";
        return GetTroughCacheAsync(
            cacheKey,
            async () =>
            {
                var value = await GetTranslationCoreAsync(locale, key, customGroup, ct);
                return (value, value != null);
            }
        );
    }

    private async Task<string?> GetTranslationCoreAsync(string locale, string key, string? customGroup = null, CancellationToken ct = default)
    {
        if (_options.Nested)
        {        
            var keyParts = key.Split('.');
            string? @namespace = null;
            if (keyParts.Length > 1)
            {
                @namespace = keyParts[0];
            }
        
            var dict = await GetNamespaceAsync(locale, @namespace, customGroup, ct);
            if (dict == null)
            {
                return null;
            }

            return TraverseTree(keyParts, @namespace, dict);
        }
        else
        {
            var dict = await GetNamespaceAsync(locale, null, customGroup, ct);
            if (dict == null)
            {
                return null;
            }

            return TraverseTree([key], null, dict);
        }
    }

    private static string? TraverseTree(string[] keyParts, string? @namespace, JsonObject dict)
    {
        JsonNode? currentNode = dict;
        var startIndex = @namespace != null ? 1 : 0; // skip namespace if used
        for (var i = startIndex; i < keyParts.Length; i++)
        {
            if (currentNode is not JsonObject obj)
            {
                return null;
            }

            var found = obj.TryGetPropertyValue(keyParts[i], out var nextNode);
            if (!found)
            {
                return null;
            }
            
            currentNode = nextNode;
        }

        if (currentNode is JsonObject)
        {
            return null;
        }

        return currentNode?.ToString();
    }
    
    private string? ReadFallbackFile(string locale)
    {
        _options.LocalFallbackPaths.TryGetValue(locale, out var localFallbackPath);
        if (string.IsNullOrWhiteSpace(localFallbackPath))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(localFallbackPath);
        }
        catch (Exception e)
        {
            _errorListener.OnError(ErrorSources.TranslationFallBackProvider, ErrorMessages.EnJsonFallbackReadFailed, e, null);
            return null;
        }
    }
    
    private JsonObject? GetFallbackNamespace(string locale, string? @namespace)
    {
        try
        {
            var text = ReadFallbackFile(locale);
            if (text == null)
            {
                return null;
            }
            var jsonNode = JsonNode.Parse(text);
            var jsonObject = jsonNode?.AsObject();
            if (jsonObject == null)
            {
                return null;
            }

            if (@namespace == null)
            {
                return jsonObject;
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
        string? @namespace, 
        string? customGroup,
        CancellationToken cancellationToken
    )
    {
        var localDict = GetFallbackNamespace(locale, @namespace);
        
        try
        {
            var remoteDict = await _enJsonHttpClient.GetTranslationsAsync<JsonObject>(locale, @namespace, customGroup, cancellationToken);
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
    public Task<JsonObject?> GetNamespaceAsync(
        string locale,
        string? @namespace,
        string? customGroup,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            throw new ArgumentException(ErrorMessages.EnJsonMissingLocale, nameof(locale));
        }
        
        var cacheKey = $"{CachePrefix}:locale:{locale}:namespace:{@namespace}:customGroup:{customGroup}";
        return GetTroughCacheAsync(
            cacheKey,
            () => GetNamespaceCoreAsync(locale, @namespace, customGroup, cancellationToken)
        );
    }
    
    public Task PostLastUsedAsync(IEnumerable<string> translationKeys, string? customGroup)
    { 
        return _enJsonHttpClient.PostLastUsedAsync(translationKeys, customGroup);
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

    private async Task<T?> GetTroughCacheAsync<T>(string cacheKey, Func<Task<(T? data, bool cacheable)>> fetcher)
        where T : class
    {
        var found = _cache.TryGetValue<T>(cacheKey, out var cached);
        if (found && cached != null)
        {
            return cached;
        }

        var (data, cacheable) = await fetcher();
        if (data == null)
        {
            return null;
        }

        if (cacheable)
        {
            _cache.Set(cacheKey, data, _memoryCacheEntryOptions);
        }

        return data;
    }
    
}