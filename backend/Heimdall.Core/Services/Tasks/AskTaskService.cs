using System.Runtime.CompilerServices;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Ask 派生任务服务实现——基于版本化知识底座 + BM25 检索构建问答上下文。
/// </summary>
public sealed class AskTaskService : IAskTaskService
{
    private readonly IVersionedKnowledgeService _versionedKnowledgeService;
    private readonly TaskLlmService _taskLlmService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ChatMessageBuilderService _chatMessageBuilder;
    private readonly ILogger<AskTaskService> _logger;

    public AskTaskService(
        IVersionedKnowledgeService versionedKnowledgeService,
        TaskLlmService taskLlmService,
        IServiceProvider serviceProvider,
        ChatMessageBuilderService chatMessageBuilder,
        ILogger<AskTaskService> logger)
    {
        _versionedKnowledgeService = versionedKnowledgeService;
        _taskLlmService = taskLlmService;
        _serviceProvider = serviceProvider;
        _chatMessageBuilder = chatMessageBuilder;
        _logger = logger;
    }

    public async Task<AskTaskExecutionResult> AskAsync(
        AskTaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Question))
            throw new InvalidOperationException("question 是必填字段。");

        var knowledgeContext = await _versionedKnowledgeService.ResolveAsync(request.Options, cancellationToken);

        var artifactContext = _versionedKnowledgeService.BuildArtifactContextMarkdown(knowledgeContext, 8_000);
        var pageContext = _versionedKnowledgeService.BuildPageContextMarkdown(
            knowledgeContext,
            request.DeepResearch ? 12 : 8,
            request.DeepResearch ? 24_000 : 16_000);
        var messages = _chatMessageBuilder.BuildAskMessages(
            knowledgeContext,
            request.Question,
            request.FilePath,
            request.DeepResearch,
            artifactContext,
            pageContext,
            request.History);
        var options = new ChatOptions
        {
            MaxOutputTokens = 8192
        };

        var answer = await _taskLlmService.GenerateTextAsync(
            request.Options.Provider ?? "ollama",
            request.Options.Model,
            request.Options.CustomModel,
            messages,
            options,
            cancellationToken);

        _logger.LogInformation(
            "Ask 派生任务完成 RepositoryVersionId={RvId} WikiVersionId={WvId} Pages={Pages}",
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id,
            knowledgeContext.Pages.Count);

        return new AskTaskExecutionResult
        {
            Content = answer,
            Stages =
            [
                new AskExecutionStage
                {
                    Title = "版本知识底座",
                    Type = "plan",
                    Iteration = 1,
                    Content = $"RepositoryVersion={knowledgeContext.RepositoryVersion.Id}，WikiVersion={knowledgeContext.WikiVersion.Id}，页面数={knowledgeContext.Pages.Count}，工件数={knowledgeContext.Artifacts.Count}"
                }
            ],
            Complete = true,
            Iterations = 1,
            RepositoryVersionId = knowledgeContext.RepositoryVersion.Id,
            WikiVersionId = knowledgeContext.WikiVersion.Id
        };
    }

    /// <summary>
    /// 流式 Ask——基于 IChatClient.GetStreamingResponseAsync() 实现真流式输出。
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> AskStreamingAsync(
        AskTaskExecutionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Question))
            throw new InvalidOperationException("question 是必填字段。");

        var knowledgeContext = await _versionedKnowledgeService.ResolveAsync(request.Options, cancellationToken);

        var artifactContext = _versionedKnowledgeService.BuildArtifactContextMarkdown(knowledgeContext, 8_000);
        var pageContext = _versionedKnowledgeService.BuildPageContextMarkdown(
            knowledgeContext,
            request.DeepResearch ? 12 : 8,
            request.DeepResearch ? 24_000 : 16_000);
        var messages = _chatMessageBuilder.BuildAskMessages(
            knowledgeContext,
            request.Question,
            request.FilePath,
            request.DeepResearch,
            artifactContext,
            pageContext,
            request.History);

        var providerId = request.Options.Provider ?? "ollama";
        var model = request.Options.Model ?? request.Options.CustomModel ?? string.Empty;

        var chatClient = _serviceProvider.GetRequiredKeyedService<IChatClient>(providerId);

        var options = new ChatOptions
        {
            MaxOutputTokens = 8192,
        };
        if (!string.IsNullOrWhiteSpace(model))
        {
            options.ModelId = model;
        }

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }

        _logger.LogInformation(
            "Ask 流式任务完成 RepositoryVersionId={RvId} WikiVersionId={WvId}",
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id);
    }
}
