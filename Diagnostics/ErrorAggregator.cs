using System;
using System.Collections.Generic;
using System.Threading.Channels;
using NrgId.EnJson.Translations.Events;
using NrgId.EnJson.Translations.Interfaces;

namespace NrgId.EnJson.Translations.Diagnostics;

/// <summary>
///     Error aggregator
/// </summary>
public class EnJsonErrorAggregator : IEnJsonErrorAggregator, IDisposable
{
    private readonly Channel<EnjsonErrorEventArgs> _channel =
        Channel.CreateUnbounded<EnjsonErrorEventArgs>();

    private readonly List<Action> _unsubscribeActions = [];

    /// <summary>
    ///     Expose reader for the background service to consume
    /// </summary>
    internal ChannelReader<EnjsonErrorEventArgs> Reader => _channel.Reader;

    /// <summary>
    ///     Dispose
    /// </summary>
    public void Dispose()
    {
        foreach (var unsub in _unsubscribeActions)
            unsub();

        _channel.Writer.TryComplete();
        _unsubscribeActions.Clear();
    }

    /// <summary>
    ///     <inheritdoc />
    /// </summary>
    public event EventHandler<EnjsonErrorEventArgs>? OnAnyError;

    internal void Register<TArgs>(
        Action<EventHandler<TArgs>> subscribe,
        Action<EventHandler<TArgs>> unsubscribe)
        where TArgs : EnjsonErrorEventArgs
    {
        EventHandler<TArgs> handler = (_, e) => Enqueue(e);

        subscribe(handler);
        _unsubscribeActions.Add(() => unsubscribe(handler));
    }

    private void Enqueue(EnjsonErrorEventArgs e)
    {
        _channel.Writer.TryWrite(e);
    }

    // Called by background service after dequeuing
    internal void RaiseOnAnyError(EnjsonErrorEventArgs e)
    {
        OnAnyError?.Invoke(this, e);
    }
}