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
    "DefaultLocale": "en",
    "NamespaceDepth": 1,
    "LocalFallbackPath": {
        "en": "Resources/en.json",
        "fr": "Resources/fr.json",
    },
    "EnableUsageTracking": true,
    "UsageReportIntervalMinutes": 5,
    "UsageReportBatchSize": 200
  }
}
```

## Dependency Injection

```csharp
using NrgId.EnJson.Translations;

builder.Services.AddEnJsonTranslations(builder.Configuration);
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
        var value = await _provider.GetTranslationAsync("emails.user_registered.subject", "en");
        return string.IsNullOrWhiteSpace(value) ? "Title" : value;
    }
}
```

## Error handling

```csharp
using NrgId.EnJson.Translations.Interfaces;

public class ErrorHandler
{
    private readonly IEnJsonErrorAggregator _errorAggregator;

    public MyService(IEnJsonErrorAggregator errorAggregator)
    {
        _errorAggregator = errorAggregator;
        _errorAggregator.OnAnyError += OnError;
    }

    public void OnError(EnjsonErrorEventArgs args)
    {
        // Handle the error here (log, alert, etc.)
        Console.WriteLine(args.ToString());
    }
}
```
