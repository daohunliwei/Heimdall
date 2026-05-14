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
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        WikiTaskService wikiTaskService,
        TaskQueueService taskQueue,
        IRepositoryConfigRepository repoRepo,
        ILogger<TasksController> logger)
    {
        _wikiTaskService = wikiTaskService;
        _taskQueue = taskQueue;
        _repoRepo = repoRepo;
        _logger = logger;
    }

    /// <summary>
    /// POST /tasks/wiki — 提交 Wiki 生成任务。支持 repository_id（推荐）或 repo_url（兼容）。
    /// </summary>
    [HttpPost("wiki")]
    public async Task<IActionResult> GenerateWiki([FromBody] WikiGenerateRequest request)
    {
        // 解析仓库标识：优先使用 repository_id，兼容 repo_url
        string repoUrl;
        string repoType;

        if (!string.IsNullOrWhiteSpace(request.RepositoryId) && Guid.TryParse(request.RepositoryId, out var repoId))
        {
            var repo = await _repoRepo.GetByIdAsync(repoId);
            if (repo is null)
                return NotFound(new { error = "仓库不存在" });
            repoUrl = repo.RepoUrl ?? $"https://github.com/{repo.Owner}/{repo.RepoName}";
            repoType = repo.RepoType;
        }
        else if (!string.IsNullOrWhiteSpace(request.RepoUrl))
        {
            repoUrl = request.RepoUrl;
            repoType = request.Type ?? DetectRepoType(repoUrl);
        }
        else
        {
            return BadRequest(new { error = "repository_id 或 repo_url 是必填字段" });
        }

        var branch = !string.IsNullOrWhiteSpace(request.Branch) ? request.Branch : "main";
        var refreshStrategy = !string.IsNullOrWhiteSpace(request.RefreshStrategy) ? request.RefreshStrategy : "latest";

        _logger.LogInformation("收到 Wiki 生成请求 Url={Url} Type={Type} Provider={Provider} Branch={Branch} Strategy={Strategy}",
            repoUrl, repoType, request.Provider, branch, refreshStrategy);

        try
        {
            // 步骤 1：创建任务记录并落库
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
                null // userId
            );

            // 步骤 2：如果任务是新创建的（非去重），入队后台处理
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
                            request.Comprehensive, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "后台 Wiki 生成失败 TaskId={TaskId}", task.Id);
                    }
                });
            }

            // 步骤 3：立即返回 task_id
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

    private static string DetectRepoType(string url)
    {
        if (Directory.Exists(url)) return "local";
        if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase)) return "github";
        if (url.Contains("gitlab", StringComparison.OrdinalIgnoreCase)) return "gitlab";
        if (url.Contains("bitbucket", StringComparison.OrdinalIgnoreCase)) return "bitbucket";
        return "github";
    }
}

public class WikiGenerateRequest
{
    /// <summary>仓库主标识（推荐，V2 新增）</summary>
    [JsonPropertyName("repository_id")]
    public string? RepositoryId { get; set; }

    /// <summary>仓库 URL（兼容旧接口）</summary>
    [JsonPropertyName("repo_url")]
    public string? RepoUrl { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("token")]
    public string? Token { get; set; }
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    [JsonPropertyName("custom_model")]
    public string? CustomModel { get; set; }
    [JsonPropertyName("language")]
    public string? Language { get; set; }
    [JsonPropertyName("comprehensive")]
    public bool Comprehensive { get; set; } = true;
    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; set; }

    /// <summary>目标分支，默认 main（V2 新增）</summary>
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    /// <summary>刷新策略：current / latest（V2 新增）</summary>
    [JsonPropertyName("refresh_strategy")]
    public string? RefreshStrategy { get; set; }

    /// <summary>生成档位：concise / comprehensive（V2 新增）</summary>
    [JsonPropertyName("generation_profile")]
    public string? GenerationProfile { get; set; }
}
