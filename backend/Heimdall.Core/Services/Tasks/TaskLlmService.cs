using System.Diagnostics;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public sealed class TaskLlmService
{
    private readonly ProviderRegistry _providerRegistry;
    private readonly ILogger<TaskLlmService> _logger;

    public TaskLlmService(ProviderRegistry providerRegistry, ILogger<TaskLlmService> logger)
    {
        _providerRegistry = providerRegistry;
        _logger = logger;
    }

    public async Task<string> GenerateTextAsync(
        string provider, string? model, string? customModel, string prompt, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = new ChatCompletionRequest
        {
            Provider = provider,
            Model = model ?? customModel ?? string.Empty,
            CustomModel = customModel,
            Messages = [new ChatMessage { Role = "user", Content = prompt }]
        };

        var (resolvedProviderId, resolvedModel, _, chatProvider) = _providerRegistry.ResolveChatProvider(request);

        _logger.LogInformation("LLM 调用 Provider={Provider} Model={Model} PromptLen={Len}",
            resolvedProviderId, resolvedModel, prompt.Length);

        var result = await chatProvider.GenerateAsync(new ProviderChatRequest
        {
            ProviderId = resolvedProviderId,
            Model = resolvedModel,
            Prompt = prompt
        }, ct);

        _logger.LogInformation("LLM 调用完成 ElapsedMs={Ms} ResultLen={Len}",
            stopwatch.ElapsedMilliseconds, result.Length);

        return result;
    }

    public (string providerId, string model) ResolveTarget(string? provider, string? model, string? customModel)
    {
        var request = new ChatCompletionRequest
        {
            Provider = provider,
            Model = model ?? customModel ?? string.Empty,
            CustomModel = customModel
        };
        var (pid, m, _, _) = _providerRegistry.ResolveChatProvider(request);
        return (pid, m);
    }
}
