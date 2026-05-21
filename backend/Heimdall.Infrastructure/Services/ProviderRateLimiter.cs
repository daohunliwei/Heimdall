using System.Collections.Concurrent;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// Provider 速率限制器——使用滑动窗口计数器限制每分钟调用次数。
/// </summary>
public sealed class ProviderRateLimiter
{
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<ProviderRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, SlidingWindowCounter> _counters = new();

    public ProviderRateLimiter(HeimdallConfigService configService, ILogger<ProviderRateLimiter> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 获取调用许可。如果已达速率限制，等待直到有配额释放或超时。
    /// </summary>
    public async Task<bool> AcquireAsync(string provider, string model, CancellationToken ct = default)
    {
        var metadata = _configService.GetProviderModelMetadata(provider, model);
        if (metadata.RateLimitPerMinute is null or <= 0) return true;

        var key = $"{provider}:{model}";
        var counter = _counters.GetOrAdd(key, _ => new SlidingWindowCounter(metadata.RateLimitPerMinute.Value));

        // 尝试立即获取
        if (counter.TryAcquire()) return true;

        // 等待最多 60 秒
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var waitMs = counter.GetWaitTimeMs();
            _logger.LogDebug("速率限制等待 Provider={Provider} Model={Model} WaitMs={Ms}", provider, model, waitMs);
            await Task.Delay(Math.Min(waitMs, 1000), ct);

            if (counter.TryAcquire()) return true;
        }

        _logger.LogWarning("速率限制超时 Provider={Provider} Model={Model}", provider, model);
        return false;
    }

    /// <summary>
    /// 获取当前速率使用情况（用于可观测性）。
    /// </summary>
    public (int currentCount, int? limit) GetUsage(string provider, string model)
    {
        var metadata = _configService.GetProviderModelMetadata(provider, model);
        var key = $"{provider}:{model}";
        if (_counters.TryGetValue(key, out var counter))
        {
            return (counter.GetCurrentCount(), metadata.RateLimitPerMinute);
        }
        return (0, metadata.RateLimitPerMinute);
    }

    /// <summary>
    /// 滑动窗口计数器——统计最近 60 秒内的调用次数。
    /// </summary>
    private sealed class SlidingWindowCounter
    {
        private readonly int _maxPerMinute;
        private readonly ConcurrentQueue<DateTime> _timestamps = new();

        public SlidingWindowCounter(int maxPerMinute)
        {
            _maxPerMinute = maxPerMinute;
        }

        public bool TryAcquire()
        {
            Cleanup();
            if (_timestamps.Count >= _maxPerMinute) return false;
            _timestamps.Enqueue(DateTime.UtcNow);
            return true;
        }

        public int GetCurrentCount()
        {
            Cleanup();
            return _timestamps.Count;
        }

        public int GetWaitTimeMs()
        {
            Cleanup();
            if (_timestamps.TryPeek(out var oldest))
            {
                var elapsed = (DateTime.UtcNow - oldest).TotalMilliseconds;
                return Math.Max(100, (int)(60_000 - elapsed));
            }
            return 100;
        }

        private void Cleanup()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            while (_timestamps.TryPeek(out var ts) && ts < cutoff)
            {
                _timestamps.TryDequeue(out _);
            }
        }
    }
}
