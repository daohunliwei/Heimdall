using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Providers;

/// <summary>
/// ChatClient 工厂 — 根据 Provider ID 创建并缓存 IChatClient 实例（替代原 ProviderRegistry）
/// </summary>
public class ChatClientFactory
{
    private readonly ConcurrentDictionary<string, IChatClient> _clients = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatClientFactory> _logger;

    public ChatClientFactory(IServiceProvider serviceProvider, ILogger<ChatClientFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 根据 Provider 标识获取或创建 IChatClient 实例
    /// </summary>
    public IChatClient GetClient(string providerId)
    {
        return _clients.GetOrAdd(providerId, key =>
        {
            _logger.LogInformation("创建 ChatClient: {ProviderId}", key);
            // 尝试通过 key 匹配（每个 provider 以 Named 方式注册）
            var client = _serviceProvider.GetKeyedService<IChatClient>(key);
            if (client != null) return client;

            // Fallback: 尝试解析默认 IChatClient
            var defaultClient = _serviceProvider.GetService<IChatClient>();
            if (defaultClient != null)
            {
                _logger.LogWarning("Provider {ProviderId} 未注册专用 IChatClient，使用默认客户端", key);
                return defaultClient;
            }

            throw new InvalidOperationException($"未找到 Provider '{key}' 的 IChatClient 注册");
        });
    }

    /// <summary>
    /// 尝试获取 IChatClient，失败时返回 null
    /// </summary>
    public IChatClient? TryGetClient(string providerId)
    {
        try { return GetClient(providerId); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取 ChatClient 失败: {ProviderId}", providerId);
            return null;
        }
    }
}
