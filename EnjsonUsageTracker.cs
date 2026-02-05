using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NrgId.EnJson.Translations
{
    /// <summary>
    /// Tracks translation key usage for reporting.
    /// </summary>
    public interface IEnjsonUsageTracker
    {
        /// <summary>
        /// Records usage for the given full key.
        /// </summary>
        void Track(string? fullKey);
    }

    /// <summary>
    /// Default implementation of <see cref="IEnjsonUsageTracker"/>.
    /// </summary>
    public sealed class EnjsonUsageTracker : IEnjsonUsageTracker, IDisposable
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EnjsonTranslationsOptions _options;
        private readonly ConcurrentDictionary<string, byte> _pending =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer? _timer;
        private int _isFlushing;

        /// <summary>
        /// Creates a new usage tracker.
        /// </summary>
        public EnjsonUsageTracker(
            IHttpClientFactory httpClientFactory,
            IOptions<EnjsonTranslationsOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;

            if (_options.EnableUsageTracking && _options.UsageReportIntervalMinutes > 0)
            {
                _timer = new Timer(_ => _ = FlushAsync(), null,
                    TimeSpan.FromMinutes(_options.UsageReportIntervalMinutes),
                    TimeSpan.FromMinutes(_options.UsageReportIntervalMinutes));
            }
        }

        /// <inheritdoc />
        public void Track(string? fullKey)
        {
            if (!_options.EnableUsageTracking || string.IsNullOrWhiteSpace(fullKey))
                return;

            var key = fullKey!;
            _pending.TryAdd(key, 0);
        }

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

                var client = _httpClientFactory.CreateClient(ServiceCollectionExtensions.EnjsonHttpClientName);
                var response = await client.PostAsJsonAsync(url, payload).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return;

                foreach (var key in batch)
                    _pending.TryRemove(key, out _);
            }
            catch
            {
                // Ignore tracking failures
            }
            finally
            {
                Interlocked.Exchange(ref _isFlushing, 0);
            }
        }

        /// <summary>
        /// Disposes timer resources.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
