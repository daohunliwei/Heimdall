using System.Text;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Slides 派生任务服务实现。
/// 该实现基于版本化页面与新工件构建演示文稿输入，而不再直接绕过版本层消费旧 Wiki 聚合数据。
/// </summary>
public sealed class SlidesTaskService : ISlidesTaskService
{
    private readonly IVersionedKnowledgeService _versionedKnowledgeService;
    private readonly TaskLlmService _taskLlmService;
    private readonly TaskPromptService _taskPromptService;
    private readonly ILogger<SlidesTaskService> _logger;

    /// <summary>
    /// 初始化 Slides 派生任务服务。
    /// </summary>
    public SlidesTaskService(
        IVersionedKnowledgeService versionedKnowledgeService,
        TaskLlmService taskLlmService,
        TaskPromptService taskPromptService,
        ILogger<SlidesTaskService> logger)
    {
        _versionedKnowledgeService = versionedKnowledgeService;
        _taskLlmService = taskLlmService;
        _taskPromptService = taskPromptService;
        _logger = logger;
    }

    /// <summary>
    /// 生成演示文稿。
    /// </summary>
    public async Task<SlidesTaskExecutionResult> GenerateAsync(
        SlidesTaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var knowledgeContext = await _versionedKnowledgeService.ResolveAsync(request.Options, cancellationToken);
        var languageDisplayName = ResolveLanguageDisplayName(knowledgeContext.EffectiveLanguage);
        var promptCorpus = BuildSlidesCorpusMarkdown(knowledgeContext);

        var planPrompt = _taskPromptService.BuildSlidesPlanPrompt(
            knowledgeContext.Repository.Owner,
            knowledgeContext.Repository.RepoName,
            promptCorpus,
            languageDisplayName);
        var planText = await _taskLlmService.GenerateTextAsync(
            request.Options.Provider ?? "ollama",
            request.Options.Model,
            request.Options.CustomModel,
            planPrompt,
            cancellationToken);

        var slideDefinitions = ParseSlidePlan(planText);
        var slideReferenceContext = TrimToLength(promptCorpus, 32_000);
        var generatedSlides = new List<GeneratedSlideResult>();

        for (var index = 0; index < slideDefinitions.Count; index++)
        {
            var slidePrompt = _taskPromptService.BuildSlidePrompt(
                knowledgeContext.Repository.Owner,
                knowledgeContext.Repository.RepoName,
                slideDefinitions[index].Title,
                BuildSlideDescription(slideDefinitions[index].Description, knowledgeContext),
                index + 1,
                slideDefinitions.Count,
                slideReferenceContext,
                languageDisplayName);

            var slideHtml = await _taskLlmService.GenerateTextAsync(
                request.Options.Provider ?? "ollama",
                request.Options.Model,
                request.Options.CustomModel,
                slidePrompt,
                cancellationToken);

            generatedSlides.Add(new GeneratedSlideResult
            {
                Id = $"slide-{index + 1}",
                Title = slideDefinitions[index].Title,
                Content = slideDefinitions[index].Description,
                Html = CleanHtmlResponse(slideHtml)
            });
        }

        _logger.LogInformation(
            "Slides 派生任务完成 RepositoryVersionId={RepositoryVersionId} WikiVersionId={WikiVersionId} SlideCount={SlideCount}",
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id,
            generatedSlides.Count);

        return new SlidesTaskExecutionResult
        {
            Plan = planText,
            Slides = generatedSlides,
            RepositoryVersionId = knowledgeContext.RepositoryVersion.Id,
            WikiVersionId = knowledgeContext.WikiVersion.Id
        };
    }

    /// <summary>
    /// 构建 Slides 使用的统一知识语料。
    /// </summary>
    private string BuildSlidesCorpusMarkdown(VersionedKnowledgeContext knowledgeContext)
    {
        var artifactContext = _versionedKnowledgeService.BuildArtifactContextMarkdown(knowledgeContext, 10_000);
        var pageContext = _versionedKnowledgeService.BuildPageContextMarkdown(knowledgeContext, 12, 36_000);
        var builder = new StringBuilder();
        builder.AppendLine("# 版本化演示文稿输入");
        builder.AppendLine($"- 仓库：{knowledgeContext.Repository.DisplayName}");
        builder.AppendLine($"- RepositoryVersionId：{knowledgeContext.RepositoryVersion.Id}");
        builder.AppendLine($"- CommitSha：{knowledgeContext.RepositoryVersion.CommitSha}");
        builder.AppendLine($"- WikiVersionId：{knowledgeContext.WikiVersion.Id}");
        builder.AppendLine($"- WikiVersionNo：{knowledgeContext.WikiVersion.VersionNo}");
        builder.AppendLine();
        builder.AppendLine(artifactContext);
        builder.AppendLine();
        builder.AppendLine(pageContext);
        return builder.ToString();
    }

    /// <summary>
    /// 解析幻灯片规划文本。
    /// </summary>
    private static List<(string Title, string Description)> ParseSlidePlan(string planText)
    {
        var result = new List<(string Title, string Description)>();
        var lines = planText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var match = System.Text.RegularExpressions.Regex.Match(
                trimmed,
                @"^\d+[\.\)]\s*(?:\*\*)?(.+?)(?:\*\*)?\s*[-–—:]\s*(.+)$");
            if (match.Success)
            {
                var title = match.Groups[1].Value.Trim();
                var description = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    result.Add((title, description));
            }
            else if (!string.IsNullOrWhiteSpace(trimmed) && char.IsDigit(trimmed[0]) && result.Count < 8)
            {
                var cleaned = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^\d+[\.\)]\s*\*?\*?", "");
                cleaned = cleaned.Replace("**", string.Empty).Trim();
                if (cleaned.Length > 3 && cleaned.Length < 120)
                    result.Add((cleaned, string.Empty));
            }
        }

        if (result.Count == 0)
        {
            result.Add(("项目概览", "整体介绍与核心功能"));
            result.Add(("架构设计", "系统架构与技术选型"));
        }

        return result;
    }

    /// <summary>
    /// 组合单页幻灯片描述。
    /// </summary>
    private static string BuildSlideDescription(
        string description,
        VersionedKnowledgeContext knowledgeContext)
    {
        var versionSummary = $"请显式体现 RepositoryVersion {knowledgeContext.RepositoryVersion.CommitSha[..Math.Min(8, knowledgeContext.RepositoryVersion.CommitSha.Length)]} 与 WikiVersion v{knowledgeContext.WikiVersion.VersionNo} 的内容基线。";
        return string.IsNullOrWhiteSpace(description)
            ? versionSummary
            : $"{description} {versionSummary}";
    }

    /// <summary>
    /// 清理大模型返回的 HTML 包裹符号。
    /// </summary>
    private static string CleanHtmlResponse(string html)
    {
        html = html.Trim();
        if (html.StartsWith("```html", StringComparison.OrdinalIgnoreCase))
        {
            html = html["```html".Length..].TrimStart();
            if (html.EndsWith("```", StringComparison.Ordinal))
                html = html[..^3].TrimEnd();
        }
        else if (html.StartsWith("```", StringComparison.Ordinal))
        {
            html = html[3..].TrimStart();
            if (html.EndsWith("```", StringComparison.Ordinal))
                html = html[..^3].TrimEnd();
        }

        return html;
    }

    /// <summary>
    /// 将语言代码转换为提示词友好的展示名称。
    /// </summary>
    private static string ResolveLanguageDisplayName(string language)
    {
        return string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase) ? "中文" : "English";
    }

    /// <summary>
    /// 按最大长度截断文本。
    /// </summary>
    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength] + "\n\n... (内容已截断)";
    }
}
