using Heimdall.Api.Models;
using Heimdall.Api.Services.Configuration;

namespace Heimdall.Api.Services.Providers;

/// <summary>
/// Provider 注册中心，统一解析聊天模型和嵌入模型。
/// </summary>
public sealed class ProviderRegistry
{
    private readonly HeimdallConfigService _configService;
    private readonly IEnumerable<IChatProvider> _chatProviders;
    private readonly IEnumerable<IEmbeddingProvider> _embeddingProviders;

    /// <summary>
    /// 初始化 Provider 注册中心。
    /// </summary>
    public ProviderRegistry(
        HeimdallConfigService configService,
        IEnumerable<IChatProvider> chatProviders,
        IEnumerable<IEmbeddingProvider> embeddingProviders)
    {
        _configService = configService;
        _chatProviders = chatProviders;
        _embeddingProviders = embeddingProviders;
    }

    /// <summary>
    /// 解析聊天 Provider。
    /// </summary>
    public (string ProviderId, string Model, ProviderModelParameters Parameters, IChatProvider Provider) ResolveChatProvider(ChatCompletionRequest request)
    {
        var providerId = _configService.ResolveProvider(request);
        var model = _configService.ResolveModel(request, providerId);
        var parameters = _configService.GetProviderModelParameters(providerId, model);
        var provider = _chatProviders.FirstOrDefault(item => item.ProviderId == providerId)
            ?? throw new InvalidOperationException($"未找到 provider `{providerId}` 的聊天适配器。");
        return (providerId, model, parameters, provider);
    }

    /// <summary>
    /// 解析嵌入器。
    /// </summary>
    public IEmbeddingProvider ResolveEmbeddingProvider()
    {
        var embedderType = _configService.GetEmbedderType();
        return _embeddingProviders.FirstOrDefault(item => item.EmbedderType == embedderType)
            ?? throw new InvalidOperationException($"未找到嵌入器 `{embedderType}` 的适配器。");
    }
}
