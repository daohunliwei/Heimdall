using System.Text;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Heimdall.Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Ask 派生任务服务实现。
/// 该实现显式继承 RepositoryVersion 与 WikiVersion，并基于双向量检索构建问答上下文。
/// </summary>
public sealed class AskTaskService : IAskTaskService
{
    private readonly IVersionedKnowledgeService _versionedKnowledgeService;
    private readonly IDualVectorSearchService _dualVectorSearchService;
    private readonly ProviderRegistry _providerRegistry;
    private readonly TaskLlmService _taskLlmService;
    private readonly ILogger<AskTaskService> _logger;

    /// <summary>
    /// 初始化 Ask 派生任务服务。
    /// </summary>
    public AskTaskService(
        IVersionedKnowledgeService versionedKnowledgeService,
        IDualVectorSearchService dualVectorSearchService,
        ProviderRegistry providerRegistry,
        TaskLlmService taskLlmService,
        ILogger<AskTaskService> logger)
    {
        _versionedKnowledgeService = versionedKnowledgeService;
        _dualVectorSearchService = dualVectorSearchService;
        _providerRegistry = providerRegistry;
        _taskLlmService = taskLlmService;
        _logger = logger;
    }

    /// <summary>
    /// 执行 Ask 问答。
    /// </summary>
    public async Task<AskTaskExecutionResult> AskAsync(
        AskTaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Question))
            throw new InvalidOperationException("question 是必填字段。");

        var knowledgeContext = await _versionedKnowledgeService.ResolveAsync(request.Options, cancellationToken);
        var embedder = _providerRegistry.ResolveEmbeddingProvider();
        var queryVector = await embedder.EmbedAsync(request.Question, cancellationToken);

        var topK = request.DeepResearch ? 16 : 8;
        var combinedSearch = await _dualVectorSearchService.SearchCombinedAsync(
            queryVector,
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id,
            topK,
            cancellationToken);

        var artifactContext = _versionedKnowledgeService.BuildArtifactContextMarkdown(knowledgeContext, 8_000);
        var pageContext = _versionedKnowledgeService.BuildPageContextMarkdown(
            knowledgeContext,
            request.DeepResearch ? 12 : 8,
            request.DeepResearch ? 24_000 : 16_000);
        var ragContext = BuildRagContextMarkdown(combinedSearch, request.FilePath, request.DeepResearch);
        var historyContext = BuildHistoryContextMarkdown(request.History);
        var prompt = BuildAskPrompt(
            knowledgeContext,
            request.Question,
            request.FilePath,
            request.DeepResearch,
            artifactContext,
            pageContext,
            ragContext,
            historyContext);

        var answer = await _taskLlmService.GenerateTextAsync(
            request.Options.Provider ?? "ollama",
            request.Options.Model,
            request.Options.CustomModel,
            prompt,
            cancellationToken);

        _logger.LogInformation(
            "Ask 派生任务完成 RepositoryVersionId={RepositoryVersionId} WikiVersionId={WikiVersionId} CodeHits={CodeHits} WikiHits={WikiHits}",
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id,
            combinedSearch.CodeResults.Count,
            combinedSearch.WikiResults.Count);

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
                },
                new AskExecutionStage
                {
                    Title = "双向量检索命中",
                    Type = "update",
                    Iteration = 1,
                    Content = $"代码命中 {combinedSearch.CodeResults.Count} 条，Wiki 命中 {combinedSearch.WikiResults.Count} 条。"
                }
            ],
            Complete = true,
            Iterations = 1,
            RepositoryVersionId = knowledgeContext.RepositoryVersion.Id,
            WikiVersionId = knowledgeContext.WikiVersion.Id
        };
    }

    /// <summary>
    /// 构建 Ask 提示词。
    /// </summary>
    private static string BuildAskPrompt(
        VersionedKnowledgeContext knowledgeContext,
        string question,
        string? filePath,
        bool deepResearch,
        string artifactContext,
        string pageContext,
        string ragContext,
        string historyContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是一个代码仓库技术专家。");
        builder.AppendLine("你必须严格基于指定版本的 RepositoryVersion、WikiVersion、页面内容与工件证据回答问题。");
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
        builder.AppendLine(ragContext);
        builder.AppendLine();
        builder.AppendLine("## 用户问题");
        builder.AppendLine(question);
        builder.AppendLine();
        builder.AppendLine("## 回答要求");
        builder.AppendLine("- 只基于上述版本化证据回答，禁止回退到未指定版本或泛化臆测。");
        builder.AppendLine("- 优先引用版本化页面与双向量命中的具体证据。");
        builder.AppendLine("- 当证据不足时，明确说明“当前版本证据不足”。");
        builder.AppendLine("- 回答使用中文。");
        builder.AppendLine(deepResearch
            ? "- 需要给出更完整的架构脉络、关键模块关系、潜在限制与可验证依据。"
            : "- 回答保持聚焦，优先解决当前问题。");

        return builder.ToString();
    }

    /// <summary>
    /// 构建对话历史上下文。
    /// </summary>
    private static string BuildHistoryContextMarkdown(IReadOnlyList<TaskConversationMessage> history)
    {
        if (history.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("## 历史对话");

        foreach (var message in history.TakeLast(8))
        {
            builder.AppendLine($"- {message.Role}: {message.Content}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 构建双向量检索上下文。
    /// </summary>
    private static string BuildRagContextMarkdown(
        CombinedSearchResult combinedSearch,
        string? filePath,
        bool deepResearch)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## 双向量检索证据");
        builder.AppendLine($"- {combinedSearch.RerankSummary}");
        builder.AppendLine();

        var codeResults = combinedSearch.CodeResults
            .Where(result => string.IsNullOrWhiteSpace(filePath)
                || result.Chunk.FilePath.Contains(filePath, StringComparison.OrdinalIgnoreCase))
            .Take(deepResearch ? 6 : 4)
            .ToList();

        if (codeResults.Count > 0)
        {
            builder.AppendLine("### 代码命中");
            foreach (var (chunk, similarity) in codeResults)
            {
                builder.AppendLine($"- {chunk.FilePath}:{chunk.StartLine}-{chunk.EndLine}（相似度 {similarity:F2}）");
                builder.AppendLine("```text");
                builder.AppendLine(TrimToLength(chunk.ContentRaw, 600));
                builder.AppendLine("```");
            }

            builder.AppendLine();
        }

        var wikiResults = combinedSearch.WikiResults.Take(deepResearch ? 6 : 4).ToList();
        if (wikiResults.Count > 0)
        {
            builder.AppendLine("### Wiki 命中");
            foreach (var (chunk, similarity) in wikiResults)
            {
                builder.AppendLine($"- WikiPageId={chunk.WikiPageId}（相似度 {similarity:F2}）");
                builder.AppendLine("```text");
                builder.AppendLine(TrimToLength(chunk.ContentRaw, 600));
                builder.AppendLine("```");
            }
        }

        if (codeResults.Count == 0 && wikiResults.Count == 0)
        {
            builder.AppendLine("当前未命中向量证据，需要更多依赖版本化页面与任务工件。");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 按最大长度截断文本片段。
    /// </summary>
    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength] + "\n... (内容已截断)";
    }
}
