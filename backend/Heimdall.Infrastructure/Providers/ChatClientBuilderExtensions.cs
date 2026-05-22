using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers;

/// <summary>
/// IChatClient 扩展方法 — 包装日志和重试
/// </summary>
public static class ChatClientBuilderExtensions
{
    /// <summary>
    /// 用日志包裹 IChatClient
    /// </summary>
    public static IChatClient WithLogging(this IChatClient inner, ILogger? logger = null)
    {
        return new LoggingChatClient(inner, logger);
    }

    /// <summary>
    /// 用重试包裹 IChatClient
    /// </summary>
    public static IChatClient WithRetry(this IChatClient inner, int maxRetries = 3, ILogger? logger = null)
    {
        return new RetryChatClient(inner, maxRetries, logger);
    }

    private sealed class LoggingChatClient : DelegatingChatClient
    {
        private readonly ILogger? _logger;

        public LoggingChatClient(IChatClient inner, ILogger? logger) : base(inner)
        {
            _logger = logger;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("LLM 调用开始: Model={ModelId}", options?.ModelId ?? "unknown");
            try
            {
                var response = await base.GetResponseAsync(messages, options, cancellationToken);
                _logger?.LogDebug("LLM 调用完成: Model={ModelId}, In={In}, Out={Out}",
                    options?.ModelId ?? "unknown",
                    response?.Usage?.InputTokenCount ?? 0,
                    response?.Usage?.OutputTokenCount ?? 0);
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LLM 调用失败: Model={ModelId}", options?.ModelId ?? "unknown");
                throw;
            }
        }
    }

    private sealed class RetryChatClient : DelegatingChatClient
    {
        private readonly int _maxRetries;
        private readonly ILogger? _logger;

        public RetryChatClient(IChatClient inner, int maxRetries, ILogger? logger) : base(inner)
        {
            _maxRetries = maxRetries;
            _logger = logger;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            for (int i = 0; i <= _maxRetries; i++)
            {
                try
                {
                    return await base.GetResponseAsync(messages, options, cancellationToken);
                }
                catch (Exception ex) when (i < _maxRetries && IsRetryable(ex))
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, i) * 1000);
                    _logger?.LogWarning(ex, "LLM 重试 {Retry}/{MaxRetries}, 等待 {Delay}ms", i + 1, _maxRetries, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            throw new InvalidOperationException("LLM 调用重试耗尽");
        }

        private static bool IsRetryable(Exception ex) =>
            ex is HttpRequestException or TaskCanceledException or TimeoutException
            || ex.Message.Contains("429") || ex.Message.Contains("503") || ex.Message.Contains("rate");
    }
}
