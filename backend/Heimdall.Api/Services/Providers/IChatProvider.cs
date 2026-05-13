using Heimdall.Api.Models;

namespace Heimdall.Api.Services.Providers;

/// <summary>
/// 聊天模型调用接口。
/// </summary>
public interface IChatProvider
{
    /// <summary>
    /// Provider 标识。
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// 执行一次聊天补全请求，并返回完整文本。
    /// </summary>
    Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken);
}
