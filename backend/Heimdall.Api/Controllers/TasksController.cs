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
    /// POST /tasks/wiki — 提交 Wiki 生成任务。必须提供 repository_id。
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
                null, // userId
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
