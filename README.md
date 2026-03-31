# NrgId.EnJson.Translations

A .NET translation provider and DI extensions for loading translations from EnJson.com as part of the NrgId platform.

## Installation

```bash
dotnet add package NrgId.EnJson.Translations
```

## Configuration

Add configuration for the `EnJsonTranslations` section:

```json
{
	"EnJsonTranslations": {
		"BaseUrl": "https://api.enjson.com",
		"ProjectId": "YOUR_PROJECT_ID",
		"ApiKey": "YOUR_API_KEY",
		"CacheMinutes": 5,
		"HttpTimeoutSeconds": 20,
		"Nested": true,
		"Fallback": {
			"Language": "en",
			"AlwaysMerge": false,
			"LocalPaths": {
				"en": "Resources/en.json",
				"fr": "Resources/fr.json"
			}
		},
		"UsageTracking": {
			"Enabled": true,
			"ReportIntervalMinutes": 5,
			"ReportBatchSize": 200
		}
	}
}
```

## Dependency Injection

```csharp
using NrgId.EnJson.Translations;

builder.Services.AddEnJsonTranslations(builder.Configuration);

builder.Services.AddSingleton<IEnJsonErrorListener, MyErrorListener>(); // optional
```

## Usage

```csharp
using NrgId.EnJson.Translations.Interfaces;

public class MyService
{
    private readonly IEnJsonTranslationProvider _provider;

    public MyService(IEnJsonTranslationProvider provider)
    {
        _provider = provider;
    }

    public async Task<string> GetTitleAsync()
    {
        var value = await _provider.GetTranslationAsync("en", "emails.user_registered.subject");
        return value ?? "Title";
    }

    public Task<JsonObject?> GetEntireNamespaceAsync()
    {
        return _provider.GetNamespace("en", "errorCodes");
    }
}
```

## Error handling

Override `IEnJsonErrorListener` service to handle errors.

Default implementation just logs errors:

```csharp
public class DefaultEnJsonErrorListener(ILogger<IEnJsonErrorListener> logger) : IEnJsonErrorListener
{
	public void OnError(
		string source,
		string? context,
		Exception? exception,
		HttpResponseMessage? httpResponseMessage
	)
	{
		if (httpResponseMessage != null)
		{
			logger.LogError(
				exception,
				"Source={Source}, context={Context}, Request failed with status={Status}.",
				source,
				context,
				httpResponseMessage.StatusCode
			);
		}
		else
		{
			logger.LogError(exception, "Source={Source}, context={Context}", source, context);
		}
	}
}
```
