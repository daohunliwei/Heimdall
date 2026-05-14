using System.Text.Json.Serialization;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("tasks")]
public class TasksController : ControllerBase
{
    private readonly WikiTaskService _wikiTaskService;
    private readonly TaskQueueService _taskQueue;
    private readonly TaskLlmService _llmService;
    private readonly TaskPromptService _promptService;
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IWikiRepository _wikiRepo;
    private readonly IWikiPageRepository _pageRepo;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        WikiTaskService wikiTaskService,
        TaskQueueService taskQueue,
        TaskLlmService llmService,
        TaskPromptService promptService,
        IRepositoryConfigRepository repoRepo,
        IWikiRepository wikiRepo,
        IWikiPageRepository pageRepo,
        ILogger<TasksController> logger)
    {
        _wikiTaskService = wikiTaskService;
        _taskQueue = taskQueue;
        _llmService = llmService;
        _promptService = promptService;
        _repoRepo = repoRepo;
        _wikiRepo = wikiRepo;
        _pageRepo = pageRepo;
        _logger = logger;
    }

    /// <summary>
    /// POST /tasks/wiki — 提交 Wiki 生成任务。
    /// </summary>
    [HttpPost("wiki")]
    public async Task<IActionResult> GenerateWiki([FromBody] WikiGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryId) || !Guid.TryParse(request.RepositoryId, out var repoId))
            return BadRequest(new { error = "repository_id 是必填字段" });

        var repo = await _repoRepo.GetByIdAsync(repoId);
        if (repo is null)
            return NotFound(new { error = "仓库不存在" });

        var repoUrl = repo.RepoUrl ?? $"https://github.com/{repo.Owner}/{repo.RepoName}";
        var repoType = repo.RepoType;
        var branch = !string.IsNullOrWhiteSpace(request.Branch) ? request.Branch : "main";
        var refreshStrategy = !string.IsNullOrWhiteSpace(request.RefreshStrategy) ? request.RefreshStrategy : "latest";

        _logger.LogInformation("收到 Wiki 生成请求 RepoId={RepoId} Url={Url} Branch={Branch} Strategy={Strategy}",
            repoId, repoUrl, branch, refreshStrategy);

        try
        {
            var task = await _wikiTaskService.CreateTaskAsync(
                repoUrl,
                repoType,
                request.Token,
                request.Provider,
                request.Model,
                request.CustomModel,
                request.Language ?? "zh",
                request.Comprehensive,
                request.ForceRefresh,
                null,
                branch,
                refreshStrategy,
                request.GenerationProfile ?? "comprehensive"
            );

            if (task.Status == "pending")
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _wikiTaskService.ExecuteAsync(
                            task, repoUrl,
                            repoType,
                            request.Token, request.Provider, request.Model,
                            request.CustomModel, request.Language ?? "zh",
                            request.Comprehensive, CancellationToken.None,
                            branch,
                            request.GenerationProfile ?? "comprehensive");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "后台 Wiki 生成失败 TaskId={TaskId}", task.Id);
                    }
                });
            }

            return Ok(new
            {
                task_id = task.Id.ToString(),
                status = task.Status,
                message = task.Status == "pending" ? "任务已接收，后台处理中" : "已有相同任务在执行"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 Wiki 任务失败");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /tasks/ask — AI 问答：基于仓库 Wiki 内容回答用户问题。
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryId) || !Guid.TryParse(request.RepositoryId, out var repoId))
            return BadRequest(new { error = "repository_id 是必填字段" });

        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "question 是必填字段" });

        try
        {
            var repo = await _repoRepo.GetByIdAsync(repoId);
            if (repo is null) return NotFound(new { error = "仓库不存在" });

            // 获取 Wiki 内容作为上下文
            var language = request.Language ?? repo.DefaultLanguage ?? "zh";
            var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repoId, "main", language);
            var wikiContext = "";
            if (wiki is not null)
            {
                var pages = await _pageRepo.GetByWikiIdAsync(wiki.Id);
                wikiContext = string.Join("\n\n", pages
                    .OrderBy(p => p.PageOrder)
                    .Take(10)
                    .Select(p => $"## {p.Title}\n{p.ContentMarkdown}"));
            }

            // 截断过长上下文
            if (wikiContext.Length > 30000)
                wikiContext = wikiContext[..30000] + "\n\n... (内容已截断)";

            var prompt = $"""
你是一个代码仓库技术专家。基于以下 Wiki 文档内容回答用户的问题。

## 仓库信息
- 仓库: {repo.DisplayName}
- 地址: {repo.RepoUrl}

## Wiki 参考内容
{wikiContext}

## 用户问题
{request.Question}

请基于以上 Wiki 内容回答。如果 Wiki 中没有足够信息，请如实告知。回答使用中文。
""";

            var provider = !string.IsNullOrWhiteSpace(request.Provider) ? request.Provider : "ollama";
            var model = request.IsCustomModel == true ? request.CustomModel : request.Model;
            var customModel = request.IsCustomModel == true ? request.CustomModel : null;

            var answer = await _llmService.GenerateTextAsync(
                provider, model, customModel, prompt, HttpContext.RequestAborted);

            return Ok(new
            {
                content = answer,
                stages = Array.Empty<object>(),
                complete = true,
                iterations = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ask 请求处理失败");
            return StatusCode(500, new { error = $"问答处理失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// POST /tasks/slides — 生成演示文稿大纲与 HTML 幻灯片。
    /// </summary>
    [HttpPost("slides")]
    public async Task<IActionResult> Slides([FromBody] WikiGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryId) || !Guid.TryParse(request.RepositoryId, out var repoId))
            return BadRequest(new { error = "repository_id 是必填字段" });

        try
        {
            var repo = await _repoRepo.GetByIdAsync(repoId);
            if (repo is null) return NotFound(new { error = "仓库不存在" });

            var language = request.Language ?? repo.DefaultLanguage ?? "zh";
            var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repoId, "main", language);
            var wikiContent = "";
            if (wiki is not null)
            {
                var pages = await _pageRepo.GetByWikiIdAsync(wiki.Id);
                wikiContent = string.Join("\n\n", pages
                    .OrderBy(p => p.PageOrder)
                    .Select(p => $"## {p.Title}\n{p.ContentMarkdown}"));
            }

            if (wikiContent.Length > 40000)
                wikiContent = wikiContent[..40000];

            var langName = language == "zh" ? "中文" : "English";
            var provider = !string.IsNullOrWhiteSpace(request.Provider) ? request.Provider : "ollama";
            var model = request.IsCustomModel == true ? request.CustomModel : request.Model;
            var customModel = request.IsCustomModel == true ? request.CustomModel : null;

            // 生成大纲
            var planPrompt = _promptService.BuildSlidesPlanPrompt(
                repo.Owner, repo.RepoName, wikiContent, langName);
            var planText = await _llmService.GenerateTextAsync(provider, model, customModel, planPrompt, HttpContext.RequestAborted);

            // 解析为幻灯片标题
            var titles = ParseSlidePlan(planText);

            // 生成每张幻灯片
            var slides = new List<object>();
            for (var i = 0; i < titles.Count; i++)
            {
                var slidePrompt = _promptService.BuildSlidePrompt(
                    repo.Owner, repo.RepoName, titles[i].Title, titles[i].Description,
                    i + 1, titles.Count, wikiContent, langName);
                var slideHtml = await _llmService.GenerateTextAsync(provider, model, customModel, slidePrompt, HttpContext.RequestAborted);
                slideHtml = CleanHtmlResponse(slideHtml);

                slides.Add(new { id = $"slide-{i + 1}", title = titles[i].Title, content = titles[i].Description, html = slideHtml });
            }

            return Ok(new { plan = planText, slides });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slides 生成失败");
            return StatusCode(500, new { error = $"幻灯片生成失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// POST /tasks/workshop — 生成训练营内容。
    /// </summary>
    [HttpPost("workshop")]
    public async Task<IActionResult> Workshop([FromBody] WikiGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryId) || !Guid.TryParse(request.RepositoryId, out var repoId))
            return BadRequest(new { error = "repository_id 是必填字段" });

        try
        {
            var repo = await _repoRepo.GetByIdAsync(repoId);
            if (repo is null) return NotFound(new { error = "仓库不存在" });

            var language = request.Language ?? repo.DefaultLanguage ?? "zh";
            var wiki = await _wikiRepo.GetByRepoBranchLanguageAsync(repoId, "main", language);
            var wikiContent = "";
            if (wiki is not null)
            {
                var pages = await _pageRepo.GetByWikiIdAsync(wiki.Id);
                wikiContent = string.Join("\n\n", pages
                    .OrderBy(p => p.PageOrder)
                    .Select(p => $"## {p.Title}\n{p.ContentMarkdown}"));
            }

            if (wikiContent.Length > 40000)
                wikiContent = wikiContent[..40000];

            var langName = language == "zh" ? "中文" : "English";
            var provider = !string.IsNullOrWhiteSpace(request.Provider) ? request.Provider : "ollama";
            var model = request.IsCustomModel == true ? request.CustomModel : request.Model;
            var customModel = request.IsCustomModel == true ? request.CustomModel : null;

            var prompt = _promptService.BuildWorkshopPrompt(
                repo.Owner, repo.RepoName, wikiContent, langName);
            var content = await _llmService.GenerateTextAsync(provider, model, customModel, prompt, HttpContext.RequestAborted);

            return Ok(new { content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workshop 生成失败");
            return StatusCode(500, new { error = $"训练营生成失败：{ex.Message}" });
        }
    }

    private static List<(string Title, string Description)> ParseSlidePlan(string planText)
    {
        var result = new List<(string Title, string Description)>();
        var lines = planText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var match = System.Text.RegularExpressions.Regex.Match(trimmed,
                @"^\d+[\.\)]\s*(?:\*\*)?(.+?)(?:\*\*)?\s*[-–—:]\s*(.+)$");
            if (match.Success)
            {
                var title = match.Groups[1].Value.Trim();
                var desc = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    result.Add((title, desc));
            }
            else if (!string.IsNullOrWhiteSpace(trimmed) && char.IsDigit(trimmed[0]) && result.Count < 8)
            {
                var clean = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^\d+[\.\)]\s*\*?\*?", "");
                clean = clean.Replace("**", "").Trim();
                if (clean.Length > 3 && clean.Length < 120)
                    result.Add((clean, ""));
            }
        }

        if (result.Count == 0)
        {
            result.Add(("项目概览", "整体介绍与核心功能"));
            result.Add(("架构设计", "系统架构与技术选型"));
        }

        return result;
    }

    private static string CleanHtmlResponse(string html)
    {
        html = html.Trim();
        if (html.StartsWith("```html", StringComparison.OrdinalIgnoreCase))
        {
            html = html["```html".Length..].TrimStart();
            if (html.EndsWith("```"))
                html = html[..^3].TrimEnd();
        }
        else if (html.StartsWith("```"))
        {
            html = html[3..].TrimStart();
            if (html.EndsWith("```"))
                html = html[..^3].TrimEnd();
        }
        return html;
    }
}

public class WikiGenerateRequest
{
    [JsonPropertyName("repository_id")]
    public string? RepositoryId { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    [JsonPropertyName("custom_model")]
    public string? CustomModel { get; set; }
    [JsonPropertyName("is_custom_model")]
    public bool? IsCustomModel { get; set; }
    [JsonPropertyName("language")]
    public string? Language { get; set; }
    [JsonPropertyName("comprehensive")]
    public bool Comprehensive { get; set; } = true;
    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; set; }
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
    [JsonPropertyName("refresh_strategy")]
    public string? RefreshStrategy { get; set; }
    [JsonPropertyName("generation_profile")]
    public string? GenerationProfile { get; set; }
}

public class AskRequest
{
    [JsonPropertyName("repository_id")]
    public string? RepositoryId { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("history")]
    public List<AskMessage>? History { get; set; }

    [JsonPropertyName("deep_research")]
    public bool DeepResearch { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    [JsonPropertyName("custom_model")]
    public string? CustomModel { get; set; }
    [JsonPropertyName("is_custom_model")]
    public bool? IsCustomModel { get; set; }
    [JsonPropertyName("language")]
    public string? Language { get; set; }
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}

public class AskMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
