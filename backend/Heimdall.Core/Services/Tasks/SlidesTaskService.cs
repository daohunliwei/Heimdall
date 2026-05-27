using System.Text;
using System.Text.RegularExpressions;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Slides 派生任务服务实现
/// 该实现基于版本化页面与任务工件派生演示文稿规划和单页 HTML 内容
/// </summary>
public sealed class SlidesTaskService : ISlidesTaskService
{
    private static readonly Regex OrderedListLineRegex = new(
        @"^\s*(\d+)[\.\)]\s*(.+?)\s*$",
        RegexOptions.Compiled);

    private readonly IVersionedKnowledgeService _versionedKnowledgeService;
    private readonly TaskLlmService _taskLlmService;
    private readonly TaskPromptService _taskPromptService;
    private readonly ILogger<SlidesTaskService> _logger;

    /// <summary>
    /// 初始化 Slides 派生任务服务
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
    /// 生成演示文稿规划与单页幻灯片 HTML
    /// </summary>
    public async Task<SlidesTaskExecutionResult> GenerateAsync(
        SlidesTaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var knowledgeContext = await _versionedKnowledgeService.ResolveAsync(request.Options, cancellationToken);
        var languageDisplayName = ResolveLanguageDisplayName(knowledgeContext.EffectiveLanguage);
        var corpus = BuildSlidesCorpusMarkdown(knowledgeContext);

        var planPrompt = _taskPromptService.BuildSlidesPlanPrompt(
            knowledgeContext.Repository.Owner,
            knowledgeContext.Repository.RepoName,
            corpus,
            languageDisplayName);

        var plan = await _taskLlmService.GenerateTextAsync(
            request.Options.Provider ?? "ollama",
            request.Options.Model,
            request.Options.CustomModel,
            planPrompt,
            cancellationToken);

        var outlines = ParseSlideOutlines(plan);
        if (outlines.Count == 0)
        {
            throw new InvalidOperationException("Slides 规划结果为空，无法生成幻灯片");
        }

        var slides = new List<GeneratedSlideResult>(outlines.Count);
        for (var index = 0; index < outlines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outline = outlines[index];
            var slidePrompt = _taskPromptService.BuildSlidePrompt(
                knowledgeContext.Repository.Owner,
                knowledgeContext.Repository.RepoName,
                outline.Title,
                outline.Description,
                index + 1,
                outlines.Count,
                corpus,
                languageDisplayName);

            var html = await _taskLlmService.GenerateTextAsync(
                request.Options.Provider ?? "ollama",
                request.Options.Model,
                request.Options.CustomModel,
                slidePrompt,
                cancellationToken);

            slides.Add(new GeneratedSlideResult
            {
                Id = $"slide-{index + 1}",
                Title = outline.Title,
                Content = outline.Description,
                Html = html.Trim()
            });
        }

        _logger.LogInformation(
            "Slides 派生任务完成 RepositoryVersionId={RepositoryVersionId} WikiVersionId={WikiVersionId} SlideCount={SlideCount}",
            knowledgeContext.RepositoryVersion.Id,
            knowledgeContext.WikiVersion.Id,
            slides.Count);

        return new SlidesTaskExecutionResult
        {
            Plan = plan,
            Slides = slides,
            RepositoryVersionId = knowledgeContext.RepositoryVersion.Id,
            WikiVersionId = knowledgeContext.WikiVersion.Id
        };
    }

    /// <summary>
    /// 构建 Slides 使用的统一知识语料
    /// </summary>
    private string BuildSlidesCorpusMarkdown(VersionedKnowledgeContext knowledgeContext)
    {
        var artifactContext = _versionedKnowledgeService.BuildArtifactContextMarkdown(knowledgeContext, 10_000);
        var pageContext = _versionedKnowledgeService.BuildPageContextMarkdown(knowledgeContext, 18, 45_000);

        var builder = new StringBuilder();
        builder.AppendLine("# 版本化演示文稿输入");
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
    /// 解析模型返回的演示文稿规划文本
    /// 优先识别有序列表，并提取标题与简述
    /// </summary>
    private static List<SlideOutline> ParseSlideOutlines(string plan)
    {
        var outlines = new List<SlideOutline>();
        if (string.IsNullOrWhiteSpace(plan))
        {
            return outlines;
        }

        foreach (var rawLine in plan.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = OrderedListLineRegex.Match(rawLine);
            if (!match.Success)
            {
                continue;
            }

            var content = match.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            outlines.Add(ParseSingleOutline(content, outlines.Count + 1));
        }

        return outlines;
    }

    /// <summary>
    /// 解析单条规划项，尽量拆出标题和描述
    /// </summary>
    private static SlideOutline ParseSingleOutline(string content, int index)
    {
        var normalized = content.Trim().TrimStart('-', '*').Trim();
        normalized = normalized.Replace('“', '"').Replace('”', '"');

        var quotedTitleMatch = Regex.Match(normalized, "\"([^\"]+)\"");
        if (quotedTitleMatch.Success)
        {
            var title = NormalizeTitle(quotedTitleMatch.Groups[1].Value, index);
            var description = normalized.Replace(quotedTitleMatch.Value, string.Empty).Trim(' ', '-', ':', '：', '—');
            return new SlideOutline(title, string.IsNullOrWhiteSpace(description) ? title : description);
        }

        var separators = new[] { " - ", " — ", " – ", ": ", "： " };
        foreach (var separator in separators)
        {
            var parts = normalized.Split(separator, 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                return new SlideOutline(
                    NormalizeTitle(parts[0], index),
                    string.IsNullOrWhiteSpace(parts[1]) ? parts[0] : parts[1]);
            }
        }

        return new SlideOutline(
            NormalizeTitle(normalized, index),
            normalized);
    }

    /// <summary>
    /// 规范化标题，避免空标题进入后续生成链路
    /// </summary>
    private static string NormalizeTitle(string title, int index)
    {
        var normalized = title.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(normalized) ? $"幻灯片 {index}" : normalized;
    }

    /// <summary>
    /// 将语言代码转换为提示词友好的展示名称
    /// </summary>
    private static string ResolveLanguageDisplayName(string language)
    {
        return string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase) ? "中文" : "English";
    }

    /// <summary>
    /// 幻灯片规划项
    /// </summary>
    private sealed record SlideOutline(string Title, string Description);
}
