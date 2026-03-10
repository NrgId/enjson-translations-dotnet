namespace NrgId.EnJson.Translations.Interfaces;

/// <summary>
///     Tracks translation key usage for reporting.
/// </summary>
public interface IEnJsonUsageTracker
{
    /// <summary>
    ///     Records usage for the given full key.
    /// </summary>
    void Track(string? fullKey);
}