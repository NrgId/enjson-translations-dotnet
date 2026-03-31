namespace NrgId.EnJson.Translations.Interfaces;

/// <summary>
///     Tracks translation key usage for reporting.
/// </summary>
public interface IEnJsonUsageTracker
{
	/// <summary>
	///     Records usage for the given full key.
	/// </summary>
	/// <example>
	/// <code>
	/// usageTracker.Track("loginForm.email.placeholder");
	/// </code>
	/// </example>
	void Track(string key, string? customGroup);
}
