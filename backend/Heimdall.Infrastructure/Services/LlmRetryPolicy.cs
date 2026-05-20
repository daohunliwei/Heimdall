using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// LLM 调用重试策略——指数退避 + 抖动，处理 429/5xx 错误。
/// </summary>
public sealed class LlmRetryPolicy
{
    private readonly ILogger<LlmRetryPolicy> _logger;

    /// <summary>最大重试次数。</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>初始退避时间（毫秒）。</summary>
    public int InitialBackoffMs { get; init; } = 1000;

    /// <summary>最大退避时间（毫秒）。</summary>
    public int MaxBackoffMs { get; init; } = 30000;

    /// <summary>退避倍数。</summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    public LlmRetryPolicy(ILogger<LlmRetryPolicy> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 带重试的异步操作执行器。
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken ct = default)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await operation(ct);
            }
            catch (Exception ex) when (attempt < MaxRetries && IsRetryable(ex))
            {
                attempt++;
                var delay = CalculateDelay(attempt);

                _logger.LogWarning(
                    "LLM 调用失败，准备重试 Operation={Op} Attempt={Attempt}/{Max} Delay={Delay}ms Error={Error}",
                    operationName, attempt, MaxRetries, delay, ex.Message);

                await Task.Delay(delay, ct);
            }
        }
    }

    /// <summary>
    /// 判断异常是否可重试。
    /// </summary>
    private static bool IsRetryable(Exception ex)
    {
        // HttpRequestException 中的 429 或 5xx
        if (ex is HttpRequestException httpEx)
        {
            var statusCode = (int?)httpEx.StatusCode;
            return statusCode is 429 or (>= 500 and <= 599);
        }

        // 超时
        if (ex is TaskCanceledException or OperationCanceledException)
        {
            return true;
        }

        // 检查内部异常
        if (ex.InnerException != null)
        {
            return IsRetryable(ex.InnerException);
        }

        // 检查异常消息中是否包含速率限制相关关键词
        var msg = ex.Message.ToLowerInvariant();
        return msg.Contains("rate limit") || msg.Contains("429") || msg.Contains("too many requests")
            || msg.Contains("server error") || msg.Contains("timeout");
    }

    /// <summary>
    /// 计算退避延迟（指数退避 + 随机抖动）。
    /// </summary>
    private int CalculateDelay(int attempt)
    {
        var baseDelay = InitialBackoffMs * Math.Pow(BackoffMultiplier, attempt - 1);
        var capped = Math.Min(baseDelay, MaxBackoffMs);

        // 添加 0-25% 的随机抖动
        var jitter = Random.Shared.NextDouble() * 0.25 * capped;
        return (int)(capped + jitter);
    }
}
