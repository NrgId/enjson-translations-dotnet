using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Options;
using NrgId.EnJson.Translations.Config;
using NrgId.EnJson.Translations.Core;
using NrgId.EnJson.Translations.Interfaces;
using NrgId.EnJson.Translations.Services.Results;

namespace NrgId.EnJson.Translations.Services;

internal sealed class EnJsonHttpClient
{
	private readonly HttpClient _httpClient;
	private readonly IEnJsonErrorListener _enJsonErrorListener;
	private readonly IOptions<EnJsonTranslationsOptions> _options;
	
	public EnJsonHttpClient(IHttpClientFactory httpClientFactory, IEnJsonErrorListener enJsonErrorListener, IOptions<EnJsonTranslationsOptions> options)
	{
		_options = options;
		_enJsonErrorListener = enJsonErrorListener;

		_httpClient = httpClientFactory.CreateClient(nameof(EnJsonHttpClient));

		var opt = options.Value;
		_httpClient.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);

		var baseUri = opt.BaseUrl.TrimEnd('/');
		_httpClient.BaseAddress = new Uri(baseUri);
            
		if (!string.IsNullOrEmpty(opt.ApiKey))
		{
			_httpClient.DefaultRequestHeaders.Add("apiKey", opt.ApiKey);
		}
	}
	
	public async Task<List<EnJsonLanguage>?> GetLanguagesAsync(bool includeInactive, CancellationToken cancellationToken)
	{
		var requestEndpoint = $"/integration/{_options.Value.ProjectId}/languages";
		var query = HttpUtility.ParseQueryString(string.Empty);
		if (includeInactive)
		{
			query["includeInactive"] = includeInactive.ToString();
		}

		var queryString = query.ToString();

		string requestUri;
		if (queryString.Length > 0)
		{
			requestUri = $"{requestEndpoint}?{query}";
		}
		else
		{
			requestUri = requestEndpoint;
		}
		
		var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			_enJsonErrorListener.OnError(ErrorSources.TranslationProvider, ErrorMessages.EnJsonRequestFailed, null, response);
			return null;
		}
		
		return await response.Content.ReadFromJsonAsync<List<EnJsonLanguage>>(cancellationToken).ConfigureAwait(false);
	}

	public async Task<T?> GetTranslationsAsync<T>(
		string locale, 
		string? @namespace, 
		string? customGroup, 
		CancellationToken cancellationToken
	) where T : class
	{
		var requestEndpoint = $"/integration/{_options.Value.ProjectId}/translations";

		var query = HttpUtility.ParseQueryString(string.Empty);
		query["language"] = locale;
		query["fallbackLanguage"] = _options.Value.FallBackLanguage;
		query["nested"] = _options.Value.Nested.ToString();

		if (!string.IsNullOrWhiteSpace(@namespace))
		{
			query["namespace"] = @namespace;
		}
		if (!string.IsNullOrWhiteSpace(customGroup))
		{
			query["customGroup"] = customGroup;
		}

		var requestUri = $"{requestEndpoint}?{query}";
		
		var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			_enJsonErrorListener.OnError(ErrorSources.TranslationProvider, ErrorMessages.EnJsonRequestFailed, null, response);
			return null;
		}
		
		return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
	}

	public async Task<bool> PostLastUsedAsync(List<string> translationKeys)
	{
		var requestEndpoint = $"/integration/{_options.Value.ProjectId}/last-used";
		var payload = new
		{
			translationKeys,
		};
		
		var response = await _httpClient.PostAsJsonAsync(requestEndpoint, payload).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			_enJsonErrorListener.OnError(ErrorSources.UsageTracker, ErrorMessages.EnJsonRequestFailed, null, response);
			return false;
		}

		return true;
	}
}
