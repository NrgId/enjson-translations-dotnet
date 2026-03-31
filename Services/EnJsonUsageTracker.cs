using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NrgId.EnJson.Translations.Config;
using NrgId.EnJson.Translations.Core;
using NrgId.EnJson.Translations.Interfaces;

namespace NrgId.EnJson.Translations.Services;

internal sealed class EnJsonUsageTracker : IEnJsonUsageTracker, IDisposable
{
	private readonly EnJsonHttpClient _enJsonHttpClient;
	private readonly IOptions<EnJsonTranslationsOptions> _options;
	private readonly IEnJsonErrorListener _errorListener;

	private readonly ConcurrentDictionary<KeyUsage, bool> _pending = new();
	private bool Enabled =>
		_options.Value.UsageTracking is { Enabled: true, ReportIntervalMinutes: > 0 };

	private readonly CancellationTokenSource? _cancellationTokenSource;
	private readonly Task? _backgroundTask;

	public EnJsonUsageTracker(
		EnJsonHttpClient enJsonHttpClient,
		IOptions<EnJsonTranslationsOptions> options,
		IEnJsonErrorListener errorListener
	)
	{
		_enJsonHttpClient = enJsonHttpClient;
		_options = options;
		_errorListener = errorListener;

		if (Enabled)
		{
			_cancellationTokenSource = new CancellationTokenSource();
			_backgroundTask = Task.Run(() => BackgroundLoopAsync(_cancellationTokenSource.Token));
		}
	}

	public void Track(string fullKey, string? customGroup = null)
	{
		if (Enabled)
		{
			_pending.TryAdd(new KeyUsage(fullKey, customGroup), true);
		}
	}

	private async Task BackgroundLoopAsync(CancellationToken token)
	{
		var delay = TimeSpan.FromMinutes(_options.Value.UsageTracking.ReportIntervalMinutes);

		while (!token.IsCancellationRequested)
		{
			try
			{
				await FlushAsync();
			}
			catch (Exception ex)
			{
				_errorListener.OnError(ErrorSources.UsageTracker, null, ex, null);
			}

			try
			{
				await Task.Delay(delay, token);
			}
			catch (TaskCanceledException)
			{
				break;
			}
		}
	}

	private async Task FlushAsync()
	{
		if (_pending.IsEmpty)
		{
			return;
		}

		var batch = _pending
			.Keys.Take(Math.Max(1, _options.Value.UsageTracking.ReportBatchSize))
			.ToList();

		if (batch.Count == 0)
		{
			return;
		}

		foreach (var group in batch.GroupBy(k => k.CustomGroup))
		{
			var keys = group.Select(u => u.Key).ToList();
			var ok = await _enJsonHttpClient.PostLastUsedAsync(keys, group.Key);
			if (ok)
			{
				foreach (var keyUsage in group)
				{
					_pending.TryRemove(keyUsage, out _);
				}
			}
		}
	}

	public void Dispose()
	{
		if (_cancellationTokenSource != null)
		{
			_cancellationTokenSource.Cancel();
			try
			{
				_backgroundTask?.Wait();
			}
			catch
			{
				/* ignored */
			}
			_cancellationTokenSource.Dispose();
		}

		_pending.Clear();
	}

	private readonly record struct KeyUsage(string Key, string? CustomGroup);
}
