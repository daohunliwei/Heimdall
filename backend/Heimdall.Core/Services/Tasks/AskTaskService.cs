using System.Runtime.CompilerServices;
using System.Text;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Heimdall.Infrastructure.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Ask 派生任务服务实现——基于版本化知识底座 + BM25 检索构建问答上下文。
/// </summary>
public sealed class AskTaskService : IAskTaskService
{
    private readonly IVersionedKnowledgeService _versionedKnowledgeService;
    private readonly TaskLlmService _taskLlmService;
    private readonly ChatClientFactory _chatClientFactory;
    private readonly ILogger<AskTaskService> _logger;

    public AskTaskService(
        IVersionedKnowledgeService versionedKnowledgeService,
        TaskLlmService taskLlmService,
        ChatClientFactory chatClientFactory,
        ILogger<AskTaskService> logger)
    {
        _versionedKnowledgeService = versionedKnowledgeService;
        _taskLlmService = taskLlmService;
        _chatClientFactory = chatClientFactory;
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
        var historyContext = BuildHistoryContextMarkdown(request.History);
        var prompt = BuildAskPrompt(
            knowledgeContext,
            request.Question,
            request.FilePath,
            request.DeepResearch,
            artifactContext,
            pageContext,
            historyContext);

        var answer = await _taskLlmService.GenerateTextAsync(
            request.Options.Provider ?? "ollama",
            request.Options.Model,
            request.Options.CustomModel,
            prompt,
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
        var historyContext = BuildHistoryContextMarkdown(request.History);
        var prompt = BuildAskPrompt(
            knowledgeContext,
            request.Question,
            request.FilePath,
            request.DeepResearch,
            artifactContext,
            pageContext,
            historyContext);

        var providerId = request.Options.Provider ?? "ollama";
        var model = request.Options.Model ?? request.Options.CustomModel ?? string.Empty;

        var chatClient = _chatClientFactory.GetClient(providerId);

        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(Microsoft.Extensions.AI.ChatRole.User, prompt)
        };

        var options = new ChatOptions
        {
            ModelId = model,
            MaxOutputTokens = 8192,
        };

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }

        _logger.LogInformation(
            "Ask 流式任务完成 RepositoryVersionId={RvId} WikiVersionId={WvId}",
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id);
    }

    private static string BuildAskPrompt(
        VersionedKnowledgeContext knowledgeContext,
        string question,
        string? filePath,
        bool deepResearch,
        string artifactContext,
        string pageContext,
        string historyContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是一个代码仓库技术专家。");
        builder.AppendLine("你必须严格基于指定版本的仓库页面内容与工件证据回答问题。");
        builder.AppendLine();
        builder.AppendLine("## 版本绑定");
        builder.AppendLine($"- 仓库：{knowledgeContext.Repository.DisplayName}");
        builder.AppendLine($"- 地址：{knowledgeContext.Repository.RepoUrl}");
        builder.AppendLine($"- 分支：{knowledgeContext.EffectiveBranch}");
        builder.AppendLine($"- 输出语言：{knowledgeContext.EffectiveLanguage}");
        builder.AppendLine($"- RepositoryVersionId：{knowledgeContext.RepositoryVersion.Id}");
        builder.AppendLine($"- CommitSha：{knowledgeContext.RepositoryVersion.CommitSha}");
        builder.AppendLine($"- WikiVersionId：{knowledgeContext.WikiVersion.Id}");
        builder.AppendLine($"- WikiVersionNo：{knowledgeContext.WikiVersion.VersionNo}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            builder.AppendLine("## 用户关注文件");
            builder.AppendLine($"- {filePath}");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(historyContext))
        {
            builder.AppendLine(historyContext);
            builder.AppendLine();
        }

        builder.AppendLine(artifactContext);
        builder.AppendLine();
        builder.AppendLine(pageContext);
        builder.AppendLine();
        builder.AppendLine("## 用户问题");
        builder.AppendLine(question);
        builder.AppendLine();
        builder.AppendLine("## 回答要求");
        builder.AppendLine("- 只基于上述版本化证据回答，禁止回退到未指定版本或泛化臆测。");
        builder.AppendLine("- 优先引用版本化页面中的具体证据。");
        builder.AppendLine("- 当证据不足时，明确说明\"当前版本证据不足\"。");
        builder.AppendLine("- 回答使用中文。");
        builder.AppendLine(deepResearch
            ? "- 需要给出更完整的架构脉络、关键模块关系、潜在限制与可验证依据。"
            : "- 回答保持聚焦，优先解决当前问题。");

        return builder.ToString();
    }

    private static string BuildHistoryContextMarkdown(IReadOnlyList<TaskConversationMessage> history)
    {
        if (history.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("## 历史对话");
        foreach (var message in history.TakeLast(8))
            builder.AppendLine($"- {message.Role}: {message.Content}");

        return builder.ToString();
    }
}
