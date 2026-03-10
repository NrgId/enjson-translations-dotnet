using System;
using System.Threading;
using NrgId.EnJson.Translations.Diagnostics;
using NrgId.EnJson.Translations.Interfaces;

namespace NrgId.EnJson.Translations.BackgroundServices;

/// <summary>
///     Service for processing errors with queue
/// </summary>
internal class ErrorProcessorService : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private readonly EnJsonErrorAggregator _aggregator;
    private readonly Timer _timer;
    private bool _disposed;

    /// <summary>
    ///     Creates service for processing errors with queue
    /// </summary>
    /// <param name="aggregator"></param>
    internal ErrorProcessorService(IEnJsonErrorAggregator aggregator)
    {
        _aggregator = (EnJsonErrorAggregator)aggregator;
        _timer = new Timer(DrainQueue, null, TimeSpan.Zero, Interval);
    }

    /// <summary>
    ///     Dispose
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Change(Timeout.Infinite, 0); // stop timer from firing again
        DrainQueue(null); // flush remaining errors
        _timer.Dispose();
    }

    private void DrainQueue(object? state)
    {
        while (_aggregator.Reader.TryRead(out var error))
            try
            {
                _aggregator.RaiseOnAnyError(error);
            }
            catch
            {
                // Never crash the timer thread
            }
    }
}