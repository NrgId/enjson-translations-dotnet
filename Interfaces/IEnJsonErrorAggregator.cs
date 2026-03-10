using System;
using NrgId.EnJson.Translations.Events;

namespace NrgId.EnJson.Translations.Interfaces;

/// <summary>
///     Error aggregator interface
/// </summary>
public interface IEnJsonErrorAggregator
{
    /// <summary>
    ///     Event for all errors
    /// </summary>
    event EventHandler<EnjsonErrorEventArgs>? OnAnyError;
}