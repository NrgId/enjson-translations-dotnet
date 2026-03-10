using System;

namespace NrgId.EnJson.Translations.Events;

/// <summary>
///     Class for api exceptions
/// </summary>
public class ApiEnjsonErrorEventArgs(Exception ex, string? source = null, int? statusCode = null)
    : EnjsonErrorEventArgs(ex, source ?? "Api", $"HTTP {statusCode}")
{
    /// <summary>
    ///     Response code
    /// </summary>
    public int? StatusCode { get; } = statusCode;

    /// <summary>
    ///     Helper
    /// </summary>
    public bool IsNetworkError => StatusCode is null;
}