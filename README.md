# NrgId.EnJson.Translations

A .NET translation provider and DI extensions for loading translations from enjson.com as part of the NrgId platform.

## Installation

```bash
dotnet add package NrgId.EnJson.Translations
```

## Configuration

Add configuration for the `EnjsonTranslations` section:

```json
{
  "EnjsonTranslations": {
    "BaseUrl": "https://api.enjson.com",
    "ProjectId": "YOUR_PROJECT_ID",
    "CacheMinutes": 5,
    "HttpTimeoutSeconds": 20,
    "DefaultLocale": "en",
    "NamespaceDepth": 1,
    "LocalFallbackPath": "Resources/en.json",
    "EnableUsageTracking": true,
    "UsageReportIntervalMinutes": 5,
    "UsageReportBatchSize": 200
  }
}
```

## Dependency Injection

```csharp
using NrgId.EnJson.Translations;

builder.Services.AddEnjsonTranslations(builder.Configuration);
```

## Usage

```csharp
using NrgId.EnJson.Translations;

public class MyService
{
    private readonly IExternalTranslationProvider _provider;

    public MyService(IExternalTranslationProvider provider)
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
