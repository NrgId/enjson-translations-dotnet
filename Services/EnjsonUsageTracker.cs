using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NrgId.EnJson.Translations.Config;
using NrgId.EnJson.Translations.Core;
using NrgId.EnJson.Translations.Diagnostics;
using NrgId.EnJson.Translations.Events;
using NrgId.EnJson.Translations.Interfaces;

namespace NrgId.EnJson.Translations.Services;

/// <summary>
///     Default implementation of <see cref="IEnJsonUsageTracker" />.
/// </summary>
public sealed class EnJsonUsageTracker : IEnJsonUsageTracker, IDisposable
{
    private readonly HttpClient _http;
    private readonly EnJsonTranslationsOptions _options;

    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);

    private readonly Timer? _timer;
    private int _isFlushing;

    /// <summary>
    ///     Creates a new usage tracker.
    /// </summary>
    public EnJsonUsageTracker(
        IHttpClientFactory httpClientFactory,
        IOptions<EnJsonTranslationsOptions> options,
        IEnJsonErrorAggregator errorAggregator)
    {
        _options = options.Value;

        _http = httpClientFactory.CreateClient(ServiceCollectionExtensions.EnJsonHttpClientName);

        if (!string.IsNullOrEmpty(_options.ApiKey))
            _http.DefaultRequestHeaders.Add("apiKey", _options.ApiKey);

        if (_options.EnableUsageTracking && _options.UsageReportIntervalMinutes > 0)
            _timer = new Timer(_ => _ = FlushAsync(), null,
                TimeSpan.FromMinutes(_options.UsageReportIntervalMinutes),
                TimeSpan.FromMinutes(_options.UsageReportIntervalMinutes));

        var aggregator = (EnJsonErrorAggregator)errorAggregator;
        aggregator.Register<ApiEnjsonErrorEventArgs>(
            h => Error += h,
            h => Error -= h
        );
    }

    /// <summary>
    ///     Disposes timer resources.
    /// </summary>
    public void Dispose()
    {
        _timer?.Dispose();
    }

    /// <inheritdoc />
    public void Track(string? fullKey)
    {
        if (!_options.EnableUsageTracking || string.IsNullOrWhiteSpace(fullKey))
            return;

        var key = fullKey!;
        _pending.TryAdd(key, 0);
    }

    /// <summary>
    ///     Event for api error
    /// </summary>
    public event EventHandler<ApiEnjsonErrorEventArgs>? Error;

    private async Task FlushAsync()
    {
        if (!_options.EnableUsageTracking)
            return;

        if (Interlocked.Exchange(ref _isFlushing, 1) == 1)
            return;

        try
        {
            if (_pending.IsEmpty)
                return;

            var batch = _pending.Keys
                .Take(Math.Max(1, _options.UsageReportBatchSize))
                .ToList();

            if (batch.Count == 0)
                return;

            var url = $"{_options.BaseUrl.TrimEnd('/')}/integration/{_options.ProjectId}/last-used";
            var payload = new
            {
                translationKeys = batch
            };

            var response = await _http.PostAsJsonAsync(url, payload).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                OnError(new ApiEnjsonErrorEventArgs(
                    new HttpRequestException($"API returned {(int)response.StatusCode}"),
                    ErrorSources.UsageTracker,
                    (int)response.StatusCode));
                return;
            }

            foreach (var key in batch)
                _pending.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            OnError(new ApiEnjsonErrorEventArgs(ex, ErrorSources.UsageTracker));
        }
        finally
        {
            Interlocked.Exchange(ref _isFlushing, 0);
        }
    }

    private void OnError(ApiEnjsonErrorEventArgs e)
    {
        Error?.Invoke(this, e);
    }
}