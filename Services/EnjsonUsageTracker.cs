using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NrgId.EnJson.Translations.Config;
using NrgId.EnJson.Translations.Core;
using NrgId.EnJson.Translations.Interfaces;

namespace NrgId.EnJson.Translations.Services;

/// <summary>
///     Default implementation of <see cref="IEnJsonUsageTracker" />.
/// </summary>
internal sealed class EnJsonUsageTracker : IEnJsonUsageTracker, IDisposable
{
    private readonly EnJsonHttpClient _enJsonHttpClient;
    private readonly EnJsonTranslationsOptions _options;
    private readonly IEnJsonErrorListener _errorListener;

    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);

    private readonly Timer? _timer;
    private int _isFlushing;

    private readonly bool _enabled;

    /// <summary>
    ///     Creates a new usage tracker.
    /// </summary>
    public EnJsonUsageTracker(
        EnJsonHttpClient enJsonHttpClient,
        IOptions<EnJsonTranslationsOptions> options,
        IEnJsonErrorListener errorListener
    )
    {
        _options = options.Value;
        _errorListener = errorListener;
        _enJsonHttpClient = enJsonHttpClient;
        _enabled = _options.UsageTracking is { Enabled: true, ReportIntervalMinutes: > 0 };

        if (!_enabled)
        {
            return;
        }
      
        var timeSpan = TimeSpan.FromMinutes(_options.UsageTracking.ReportIntervalMinutes);
        _timer = new Timer(_ => _ = FlushAsync(), null, timeSpan, timeSpan);
    }

    /// <summary>
    ///     Disposes timer resources.
    /// </summary>
    public void Dispose()
    {
        _timer?.Dispose();
    }

    /// <inheritdoc />
    public void Track(string fullKey)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(fullKey))
        {
            return;
        }

        var key = fullKey!;
        _pending.TryAdd(key, 0);
    }

    private async Task FlushAsync()
    {
        if (Interlocked.Exchange(ref _isFlushing, 1) == 1)
        {
            return;
        }

        try
        {
            if (_pending.IsEmpty) 
            {
                return;
            }

            var batch = _pending.Keys
                .Take(Math.Max(1, _options.UsageTracking.ReportBatchSize))
                .ToList();

            if (batch.Count == 0)
            {
                return;
            }

            var ok = await _enJsonHttpClient.PostLastUsedAsync(batch);
            if (!ok)
            {
                return;
            }

            foreach (var key in batch)
                _pending.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            _errorListener.OnError(ErrorSources.UsageTracker, null, ex, null);
        }
        finally
        {
            Interlocked.Exchange(ref _isFlushing, 0);
        }
    }
}