namespace NrgId.EnJson.Translations
{
    /// <summary>
    /// Result of a translation lookup.
    /// </summary>
    public sealed class EnjsonTranslationResult
    {
        /// <summary>
        /// Creates a new translation result.
        /// </summary>
        public EnjsonTranslationResult(string key, string? value, bool found, string? errorCode = null)
        {
            Key = key;
            Value = value;
            Found = found;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Translation key.
        /// </summary>
        public string Key { get; }
        /// <summary>
        /// Translated value if found.
        /// </summary>
        public string? Value { get; }
        /// <summary>
        /// Whether the translation was found.
        /// </summary>
        public bool Found { get; }
        /// <summary>
        /// Optional error code when request fails.
        /// </summary>
        public string? ErrorCode { get; }
    }
}
