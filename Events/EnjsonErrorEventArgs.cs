using System;

namespace NrgId.EnJson.Translations.Events;

/// <summary>
///     Common class for lib exceptions
/// </summary>
public class EnjsonErrorEventArgs(Exception exception, string source, string? context = null) : EventArgs
{
    /// <summary>
    ///     Basic exception
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    ///     Name of source
    /// </summary>
    public string Source { get; } = source;

    /// <summary>
    ///     Error context
    /// </summary>
    public string? Context { get; } = context;

    /// <summary>
    ///     Time of exception
    /// </summary>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    /// <summary>
    ///     Handled flag
    /// </summary>
    public bool Handled { get; set; }
}