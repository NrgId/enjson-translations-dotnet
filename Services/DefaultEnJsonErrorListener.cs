using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NrgId.EnJson.Translations.Interfaces;

namespace NrgId.EnJson.Translations.Services;

/// <inheritdoc />
public class DefaultEnJsonErrorListener(ILogger<IEnJsonErrorListener> logger) : IEnJsonErrorListener
{
	/// <inheritdoc />
	public void OnError(string source, string? context, Exception? exception, HttpResponseMessage? httpResponseMessage)
	{
		if (httpResponseMessage != null)
		{
			logger.LogError(exception, "Source={Source}, context={Context}, Request failed with status={Status}.", source, context, httpResponseMessage.StatusCode);
		}
		else
		{
			logger.LogError(exception, "Source={Source}, context={Context}", source, context);
		}
	}
}
