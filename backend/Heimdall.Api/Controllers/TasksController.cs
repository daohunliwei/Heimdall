using System.Text.Json;
using Heimdall.Core.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("tasks")]
public class TasksController : ControllerBase
{
    private readonly WikiTaskService _wikiTaskService;
    private readonly TaskQueueService _taskQueue;
    private readonly TaskProgressService _progressService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        WikiTaskService wikiTaskService,
        TaskQueueService taskQueue,
        TaskProgressService progressService,
        ILogger<TasksController> logger)
    {
        _wikiTaskService = wikiTaskService;
        _taskQueue = taskQueue;
        _progressService = progressService;
        _logger = logger;
    }

    [HttpPost("wiki")]
    public async Task<IActionResult> GenerateWiki([FromBody] WikiGenerateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RepoUrl))
            return BadRequest(new { error = "repo_url 是必填字段" });

        _logger.LogInformation("收到 Wiki 生成请求 Url={Url} Type={Type}", request.RepoUrl, request.Type);

        try
        {
            var result = await _wikiTaskService.GenerateAsync(
                request.RepoUrl,
                request.Type ?? DetectRepoType(request.RepoUrl),
                request.Token,
                request.Provider,
                request.Model,
                request.CustomModel,
                request.Language ?? "zh",
                request.Comprehensive,
                request.ForceRefresh,
                request.RepositoryId,
                ct);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "任务已取消" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki 生成失败");
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
    [System.Text.Json.Serialization.JsonPropertyName("repo_url")]
    public string RepoUrl { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("token")]
    public string? Token { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("provider")]
    public string? Provider { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("model")]
    public string? Model { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("custom_model")]
    public string? CustomModel { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("language")]
    public string? Language { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("comprehensive")]
    public bool Comprehensive { get; set; } = true;
    [System.Text.Json.Serialization.JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("repository_id")]
    public Guid? RepositoryId { get; set; }
}
