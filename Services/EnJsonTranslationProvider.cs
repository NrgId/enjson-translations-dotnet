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
	private readonly IOptions<EnJsonTranslationsOptions> _options;
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
		_options = options;
		_usageTracker = usageTracker;
		_errorListener = errorListener;
		_memoryCacheEntryOptions = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.Value.CacheMinutes),
		};

		if (string.IsNullOrWhiteSpace(_options.Value.ProjectId))
		{
			throw new ArgumentException(
				EnJsonErrorMessages.MissingProjectId,
				nameof(_options.Value.ProjectId)
			);
		}

		foreach (var fallbackPath in _options.Value.Fallback.LocalPaths)
		{
			if (!string.IsNullOrWhiteSpace(fallbackPath.Value) && !File.Exists(fallbackPath.Value))
			{
				throw new ArgumentException(
					EnJsonErrorMessages.FallbackNotFound,
					nameof(_options.Value.Fallback.LocalPaths)
				);
			}
		}
	}

	public Task<List<EnJsonLanguage>?> GetLanguagesAsync(
		bool includeInactive,
		CancellationToken cancellationToken
	)
	{
		var cacheKey = $"{CachePrefix}:{_options.Value.ProjectId}:languages:{includeInactive}";

		return GetTroughCacheAsync(
			cacheKey,
			async () =>
			{
				var languages = await _enJsonHttpClient.GetLanguagesAsync(
					includeInactive,
					cancellationToken
				);
				return (languages, true);
			}
		);
	}

	/// <inheritdoc />
	public Task<string?> GetTranslationAsync(
		string language,
		string key,
		string? customGroup = null,
		CancellationToken cancellationToken = default
	)
	{
		if (string.IsNullOrWhiteSpace(language))
		{
			throw new ArgumentException(EnJsonErrorMessages.MissingLanguage, nameof(language));
		}

		if (string.IsNullOrWhiteSpace(key))
		{
			return Task.FromResult<string?>(null);
		}

		_usageTracker.Track(key, customGroup);

		var cacheKey =
			$"{CachePrefix}:{_options.Value.ProjectId}:translation:language:{language}:key:{key}:customGroup:{customGroup}";
		return GetTroughCacheAsync(
			cacheKey,
			async () =>
			{
				var value = await GetTranslationCoreAsync(
					language,
					key,
					customGroup,
					cancellationToken
				);
				return (value, value != null);
			}
		);
	}

	private async Task<string?> GetTranslationCoreAsync(
		string language,
		string key,
		string? customGroup,
		CancellationToken cancellationToken
	)
	{
		if (_options.Value.Nested)
		{
			var keyParts = key.Split('.');
			string? @namespace = null;
			if (keyParts.Length > 1)
			{
				@namespace = keyParts[0];
			}

			var dict = await GetTranslationsAsync(
				language,
				@namespace,
				customGroup,
				cancellationToken
			);
			if (dict == null)
			{
				return null;
			}

			return TraverseTree(keyParts, @namespace, dict);
		}
		else
		{
			var dict = await GetTranslationsAsync(language, null, customGroup, cancellationToken);
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

	private JsonObject? ReadFallbackFile(string language)
	{
		_options.Value.Fallback.LocalPaths.TryGetValue(language, out var localFallbackPath);
		if (string.IsNullOrWhiteSpace(localFallbackPath))
		{
			return null;
		}

		try
		{
			var text = File.ReadAllText(localFallbackPath);
			var jsonNode = JsonNode.Parse(text);
			return jsonNode?.AsObject();
		}
		catch (Exception e)
		{
			_errorListener.OnError(
				EnJsonErrorSources.TranslationFallBackProvider,
				EnJsonErrorMessages.FallbackReadFailed,
				e,
				null
			);
			return null;
		}
	}

	private JsonObject? GetFallbackSourceFromFile(string language, string? @namespace)
	{
		try
		{
			var jsonObject = ReadFallbackFile(language);
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
			_errorListener.OnError(EnJsonErrorSources.TranslationFallBackProvider, null, e, null);
			return null;
		}
	}

	private (
		JsonObject? defaultLanguageSource,
		JsonObject? requestedLanguageSource
	) GetFallbackSources(string language, string? @namespace)
	{
		var requestedLanguageSource = GetFallbackSourceFromFile(language, @namespace);
		if (language != _options.Value.Fallback.Language)
		{
			var defaultLanguageSource = GetFallbackSourceFromFile(
				_options.Value.Fallback.Language,
				@namespace
			);
			return (defaultLanguageSource, requestedLanguageSource);
		}

		return (null, requestedLanguageSource);
	}

	private async Task<(JsonObject? dict, bool cacheable)> GetTranslationsCoreAsync(
		string language,
		string? @namespace,
		string? customGroup,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var remoteSource = await _enJsonHttpClient.GetTranslationsAsync<JsonObject>(
				language,
				@namespace,
				customGroup,
				cancellationToken
			);

			var cacheable = remoteSource != null;

			if (_options.Value.Fallback.AlwaysMerge || remoteSource == null)
			{
				var (defaultLanguageSource, requestedLanguageSource) = GetFallbackSources(
					language,
					@namespace
				);

				return (
					DeepMerge(defaultLanguageSource, requestedLanguageSource, remoteSource),
					cacheable
				);
			}

			return (remoteSource, cacheable);
		}
		catch (Exception e)
		{
			_errorListener.OnError(EnJsonErrorSources.TranslationProvider, null, e, null);
			return (null, false);
		}
	}

	/// <inheritdoc />
	public Task<JsonObject?> GetTranslationsAsync(
		string language,
		string? @namespace,
		string? customGroup,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(language))
		{
			throw new ArgumentException(EnJsonErrorMessages.MissingLanguage, nameof(language));
		}

		var cacheKey =
			$"{CachePrefix}:language:{language}:namespace:{@namespace}:customGroup:{customGroup}";
		return GetTroughCacheAsync(
			cacheKey,
			() => GetTranslationsCoreAsync(language, @namespace, customGroup, cancellationToken)
		);
	}

	public Task PostLastUsedAsync(IEnumerable<string> translationKeys, string? customGroup)
	{
		return _enJsonHttpClient.PostLastUsedAsync(translationKeys, customGroup);
	}

	/// <summary>
	/// Not pure, all sources should be considered unusable.
	/// </summary>
	/// <param name="sources">List of sources, older to newer.</param>
	private static JsonObject? DeepMerge(params IEnumerable<JsonObject?> sources)
	{
		var notNullSources = sources.Where(s => s != null).Select(s => s!).ToList();
		var target = notNullSources.LastOrDefault();
		if (target == null)
		{
			return null;
		}

		var keys = notNullSources
			.SelectMany(s => ((IDictionary<string, JsonNode?>)s).Keys)
			.Distinct()
			.ToList(); // to list required to mutate in loop (via source.Remove)

		foreach (var key in keys)
		{
			var foundInTarget = target.TryGetPropertyValue(key, out var targetNode);

			// Merge the rest into the target
			foreach (var source in notNullSources)
			{
				if (source == target)
				{
					// skip target itself
					continue;
				}

				var foundInSource = source.TryGetPropertyValue(key, out var sourceNode);
				if (!foundInSource)
				{
					continue;
				}

				if (!foundInTarget)
				{
					// not in target, just put it in
					source.Remove(key);
					target[key] = sourceNode;

					// and now it's in target!
					targetNode = sourceNode;
					foundInTarget = true;
					continue;
				}

				if (targetNode is JsonObject targetObject && sourceNode is JsonObject sourceObject)
				{
					// both are objects, merge
					DeepMerge(sourceObject, targetObject);
				}

				// types differ, keep newer.
			}
		}

		return target;
	}

	private async Task<T?> GetTroughCacheAsync<T>(
		string cacheKey,
		Func<Task<(T? data, bool cacheable)>> fetcher
	)
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
