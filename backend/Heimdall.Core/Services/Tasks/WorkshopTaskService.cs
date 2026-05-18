using System.Text;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Workshop 派生任务服务实现。
/// 该实现基于版本化页面与任务工件派生训练营内容，确保输出与当前浏览版本保持一致。
/// </summary>
public sealed class WorkshopTaskService : IWorkshopTaskService
{
    private readonly IVersionedKnowledgeService _versionedKnowledgeService;
    private readonly TaskLlmService _taskLlmService;
    private readonly TaskPromptService _taskPromptService;
    private readonly ILogger<WorkshopTaskService> _logger;

    /// <summary>
    /// 初始化 Workshop 派生任务服务。
    /// </summary>
    public WorkshopTaskService(
        IVersionedKnowledgeService versionedKnowledgeService,
        TaskLlmService taskLlmService,
        TaskPromptService taskPromptService,
        ILogger<WorkshopTaskService> logger)
    {
        _versionedKnowledgeService = versionedKnowledgeService;
        _taskLlmService = taskLlmService;
        _taskPromptService = taskPromptService;
        _logger = logger;
    }

    /// <summary>
    /// 生成训练营内容。
    /// </summary>
    public async Task<WorkshopTaskExecutionResult> GenerateAsync(
        WorkshopTaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var knowledgeContext = await _versionedKnowledgeService.ResolveAsync(request.Options, cancellationToken);
        var languageDisplayName = ResolveLanguageDisplayName(knowledgeContext.EffectiveLanguage);
        var corpus = BuildWorkshopCorpusMarkdown(knowledgeContext);

        var prompt = _taskPromptService.BuildWorkshopPrompt(
            knowledgeContext.Repository.Owner,
            knowledgeContext.Repository.RepoName,
            corpus,
            languageDisplayName);
        var content = await _taskLlmService.GenerateTextAsync(
            request.Options.Provider ?? "ollama",
            request.Options.Model,
            request.Options.CustomModel,
            prompt,
            cancellationToken);

        _logger.LogInformation(
            "Workshop 派生任务完成 RepositoryVersionId={RepositoryVersionId} WikiVersionId={WikiVersionId}",
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id);

        return new WorkshopTaskExecutionResult
        {
            Content = content,
            RepositoryVersionId = knowledgeContext.RepositoryVersion.Id,
            WikiVersionId = knowledgeContext.WikiVersion.Id
        };
    }

    /// <summary>
    /// 构建 Workshop 使用的统一知识语料。
    /// </summary>
    private string BuildWorkshopCorpusMarkdown(VersionedKnowledgeContext knowledgeContext)
    {
        var artifactContext = _versionedKnowledgeService.BuildArtifactContextMarkdown(knowledgeContext, 10_000);
        var pageContext = _versionedKnowledgeService.BuildPageContextMarkdown(knowledgeContext, 14, 40_000);

        var builder = new StringBuilder();
        builder.AppendLine("# 版本化训练营输入");
        builder.AppendLine($"- 仓库：{knowledgeContext.Repository.DisplayName}");
        builder.AppendLine($"- RepositoryVersionId：{knowledgeContext.RepositoryVersion.Id}");
        builder.AppendLine($"- CommitSha：{knowledgeContext.RepositoryVersion.CommitSha}");
        builder.AppendLine($"- WikiVersionId：{knowledgeContext.WikiVersion.Id}");
        builder.AppendLine($"- WikiVersionNo：{knowledgeContext.WikiVersion.VersionNo}");
        builder.AppendLine($"- 页面数：{knowledgeContext.Pages.Count}");
        builder.AppendLine();
        builder.AppendLine(artifactContext);
        builder.AppendLine();
        builder.AppendLine(pageContext);
        return builder.ToString();
    }

    /// <summary>
    /// 将语言代码转换为提示词友好的展示名称。
    /// </summary>
    private static string ResolveLanguageDisplayName(string language)
    {
        return string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase) ? "中文" : "English";
    }
}
