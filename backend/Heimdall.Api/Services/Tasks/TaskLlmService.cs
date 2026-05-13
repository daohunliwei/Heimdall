using Heimdall.Api.Models;
using Heimdall.Api.Services.Providers;

namespace Heimdall.Api.Services.Tasks;

/// <summary>
/// 任务级模型调用服务，负责直接执行编排后的提示词。
/// </summary>
public sealed class TaskLlmService
{
    private readonly ProviderRegistry _providerRegistry;
    private readonly TaskRequestUtilityService _taskRequestUtilityService;

    /// <summary>
    /// 初始化任务模型调用服务。
    /// </summary>
    public TaskLlmService(ProviderRegistry providerRegistry, TaskRequestUtilityService taskRequestUtilityService)
    {
        _providerRegistry = providerRegistry;
        _taskRequestUtilityService = taskRequestUtilityService;
    }

    /// <summary>
    /// 直接调用模型生成文本。
    /// </summary>
    public async Task<string> GenerateTextAsync(TaskRequestBase request, string prompt, CancellationToken cancellationToken)
    {
        var resolverRequest = _taskRequestUtilityService.BuildChatRequest(request, new[]
        {
            new ChatMessage
            {
                Role = "user",
                Content = prompt
            }
        });

        var (providerId, model, parameters, provider) = _providerRegistry.ResolveChatProvider(resolverRequest);
        var providerRequest = new ProviderChatRequest
        {
            ProviderId = providerId,
            Model = model,
            Prompt = prompt,
            Temperature = parameters.Temperature,
            TopP = parameters.TopP,
            TopK = parameters.TopK,
            Options = parameters.Options
        };

        return await provider.GenerateAsync(providerRequest, cancellationToken);
    }

    /// <summary>
    /// 解析当前任务的实际模型目标。
    /// </summary>
    public (string ProviderId, string Model) ResolveTarget(TaskRequestBase request)
    {
        var resolverRequest = _taskRequestUtilityService.BuildChatRequest(request, new[]
        {
            new ChatMessage
            {
                Role = "user",
                Content = "resolve"
            }
        });

        var (providerId, model, _, _) = _providerRegistry.ResolveChatProvider(resolverRequest);
        return (providerId, model);
    }
}
