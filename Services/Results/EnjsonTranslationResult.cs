namespace NrgId.EnJson.Translations.Services.Results;

/// <summary>
///     Result of a translation lookup.
/// </summary>
internal class EnJsonTranslationResult
{
    /// <summary>
    ///     Creates a new translation result.
    /// </summary>
    internal EnJsonTranslationResult(string key, string? value, bool found, string? errorCode = null)
    {
        Key = key;
        Value = value;
        Found = found;
        ErrorCode = errorCode;
    }

    /// <summary>
    ///     Translation key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Translated value if found.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    ///     Whether the translation was found.
    /// </summary>
    public bool Found { get; }

    /// <summary>
    ///     Optional error code when request fails.
    /// </summary>
    public string? ErrorCode { get; }
}