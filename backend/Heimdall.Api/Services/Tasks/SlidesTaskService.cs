using System.Text.RegularExpressions;
using Heimdall.Api.Models;

namespace Heimdall.Api.Services.Tasks;

/// <summary>
/// Slides 任务服务，负责后端主导生成演示文稿页面。
/// </summary>
public sealed class SlidesTaskService
{
    private readonly TaskLlmService _taskLlmService;
    private readonly TaskPromptService _taskPromptService;
    private readonly TaskRequestUtilityService _taskRequestUtilityService;
    private readonly WikiTaskService _wikiTaskService;

    /// <summary>
    /// 初始化 Slides 任务服务。
    /// </summary>
    public SlidesTaskService(
        TaskLlmService taskLlmService,
        TaskPromptService taskPromptService,
        TaskRequestUtilityService taskRequestUtilityService,
        WikiTaskService wikiTaskService)
    {
        _taskLlmService = taskLlmService;
        _taskPromptService = taskPromptService;
        _taskRequestUtilityService = taskRequestUtilityService;
        _wikiTaskService = wikiTaskService;
    }

    /// <summary>
    /// 生成幻灯片。
    /// </summary>
    public async Task<SlidesTaskResponse> GenerateAsync(SlidesTaskRequest request, CancellationToken cancellationToken)
    {
        var languageDisplayName = _taskRequestUtilityService.ResolveLanguageDisplayName(request);
        var wikiResponse = await _wikiTaskService.GenerateAsync(new WikiTaskRequest
        {
            RepoUrl = request.RepoUrl,
            Owner = request.Owner,
            Repo = request.Repo,
            Type = request.Type,
            Token = request.Token,
            Provider = request.Provider,
            Model = request.Model,
            CustomModel = request.CustomModel,
            Language = request.Language,
            ExcludedDirs = request.ExcludedDirs,
            ExcludedFiles = request.ExcludedFiles,
            IncludedDirs = request.IncludedDirs,
            IncludedFiles = request.IncludedFiles,
            ForceRefresh = request.ForceRefresh,
            Comprehensive = request.Comprehensive
        }, cancellationToken);

        var wikiContent = _taskPromptService.BuildWikiReferenceText(wikiResponse);
        var planPrompt = _taskPromptService.BuildSlidesPlanPrompt(
            wikiResponse.Repo.Owner,
            wikiResponse.Repo.Repo,
            wikiContent,
            languageDisplayName);
        var planContent = await _taskLlmService.GenerateTextAsync(request, planPrompt, cancellationToken);
        var slideItems = ExtractSlidePlan(planContent, wikiResponse.Repo.Repo);
        var slides = new List<GeneratedSlide>();

        for (var index = 0; index < slideItems.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slideItem = slideItems[index];
            var slidePrompt = _taskPromptService.BuildSlidePrompt(
                wikiResponse.Repo.Owner,
                wikiResponse.Repo.Repo,
                slideItem.Title,
                slideItem.Description,
                index + 1,
                slideItems.Count,
                wikiContent,
                languageDisplayName);

            var slideContent = await _taskLlmService.GenerateTextAsync(request, slidePrompt, cancellationToken);
            var slideHtml = ExtractSlideHtml(slideContent);
            if (!slideHtml.Contains("<style>", StringComparison.OrdinalIgnoreCase) &&
                !slideHtml.Contains("<link rel=\"stylesheet\"", StringComparison.OrdinalIgnoreCase))
            {
                slideHtml = WrapSlideHtml(slideItem.Title, slideHtml);
            }

            slides.Add(new GeneratedSlide
            {
                Id = $"slide-{index + 1}",
                Title = slideItem.Title,
                Content = string.IsNullOrWhiteSpace(slideItem.Description) ? slideItem.Title : slideItem.Description,
                Html = slideHtml
            });
        }

        return new SlidesTaskResponse
        {
            Plan = planContent,
            Slides = slides
        };
    }

    private static List<(string Title, string Description)> ExtractSlidePlan(string planContent, string repo)
    {
        var lines = Regex.Matches(planContent, @"(?:^|\n)\s*(?:\d+[\.\)]|Slide\s+\d+\s*:?)\s*(.+?)(?=\n\s*(?:\d+[\.\)]|Slide\s+\d+\s*:?)|\z)", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (lines.Count == 0)
        {
            lines =
            [
                $"Title Slide: Introduction to {repo}",
                $"Overview: Key features and purpose of {repo}",
                "Architecture: System components and structure",
                "Features: Main capabilities and functionalities",
                "Implementation: How it works and technical details",
                $"Use Cases: How to use {repo} effectively",
                "Conclusion: Summary and next steps"
            ];
        }

        return lines.Select(line =>
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
        }).ToList();
    }

    private static string ExtractSlideHtml(string slideContent)
    {
        var codeBlockMatch = Regex.Match(slideContent, "```(?:html)?\\s*([\\s\\S]*?)\\s*```", RegexOptions.IgnoreCase);
        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        var divMatch = Regex.Match(slideContent, "<div class=\"slide\"[\\s\\S]*", RegexOptions.IgnoreCase);
        if (divMatch.Success)
        {
            return divMatch.Value.Trim();
        }

        return slideContent.Trim();
    }

    private static string WrapSlideHtml(string slideTitle, string slideHtml)
    {
        return $$"""
<div class="slide">
    <div class="code-pattern"></div>
    <div class="accent-glow"></div>

    <div class="content">
        <div class="slide-header">
            <h1 class="main-title">{{slideTitle}}</h1>
        </div>

        <div class="slide-body">
            <div class="left-column">
                <div class="slide-content">
                    {{slideHtml}}
                </div>
            </div>
            <div class="right-column">
                <div class="visual-content">
                    <i class="fas fa-code fa-5x" style="opacity: 0.3; color: #58a6ff; margin: 2rem auto; display: block; text-align: center;"></i>
                </div>
            </div>
        </div>
    </div>
</div>
<style>
    .slide {
        width: 100%;
        height: 100%;
        position: relative;
        overflow: hidden;
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        color: #e6edf3;
        background: linear-gradient(135deg, #0d1117 0%, #161b22 100%);
        display: flex;
        flex-direction: column;
    }
    .code-pattern {
        position: absolute;
        width: 100%;
        height: 100%;
        background-image: url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%2330363d' fill-opacity='0.15'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E");
        opacity: 0.2;
        z-index: 0;
    }
    .accent-glow {
        position: absolute;
        width: 600px;
        height: 600px;
        border-radius: 50%;
        background: radial-gradient(circle, rgba(88, 166, 255, 0.1) 0%, rgba(88, 166, 255, 0) 70%);
        top: -200px;
        right: -100px;
        z-index: 1;
    }
    .content {
        z-index: 2;
        position: relative;
        height: 100%;
        padding: 40px 60px;
        display: flex;
        flex-direction: column;
    }
    .slide-header {
        margin-bottom: 30px;
    }
    .slide-body {
        display: flex;
        flex: 1;
        gap: 40px;
        align-items: flex-start;
    }
    .left-column, .right-column {
        flex: 1;
        display: flex;
        flex-direction: column;
    }
    .main-title {
        font-size: 3.5rem;
        font-weight: 700;
        background: linear-gradient(135deg, #58a6ff 0%, #8957e5 100%);
        -webkit-background-clip: text;
        background-clip: text;
        -webkit-text-fill-color: transparent;
        line-height: 1.1;
        margin-bottom: 10px;
    }
    .slide-content {
        font-size: 1.5rem;
        color: #e6edf3;
        line-height: 1.5;
        display: flex;
        flex-direction: column;
    }
</style>
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.4.0/css/all.min.css">
""";
    }
}
