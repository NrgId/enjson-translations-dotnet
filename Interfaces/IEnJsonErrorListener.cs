using System;
using System.Net.Http;
using NrgId.EnJson.Translations.Core;

namespace NrgId.EnJson.Translations.Interfaces;

/// <summary>
/// Error listener. Implement and add as singleton service to get notified of errors during background processing.
/// </summary>
public interface IEnJsonErrorListener
{
	/// <summary>
	/// Will be called with error context
	/// </summary>
	/// <param name="source">Where error comes from, <see cref="EnJsonErrorSources"/></param>
	/// <param name="context">What actually happened, <see cref="EnJsonErrorMessages"/>, null if unknown</param>
	/// <param name="exception">If error was produced as a result of an exception, will be set</param>
	/// <param name="httpResponseMessage">If error was produced as a result of a network call, will be set</param>
	void OnError(
		string source,
		string? context,
		Exception? exception,
		HttpResponseMessage? httpResponseMessage
	);
}
