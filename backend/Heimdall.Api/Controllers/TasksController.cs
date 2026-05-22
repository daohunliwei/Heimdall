using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("tasks")]
public class TasksController : ControllerBase
{
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IAskTaskService _askTaskService;
    private readonly ISlidesTaskService _slidesTaskService;
    private readonly IWorkshopTaskService _workshopTaskService;
    private readonly ITaskRepository _taskRepo;
    private readonly WikiTaskService _wikiTaskService;
    private readonly ILogger<TasksController> _logger;

    /// <summary>
    /// 初始化任务控制器。
    /// </summary>
    public TasksController(
        IRepositoryConfigRepository repoRepo,
        IAskTaskService askTaskService,
        ISlidesTaskService slidesTaskService,
        IWorkshopTaskService workshopTaskService,
        ITaskRepository taskRepo,
        WikiTaskService wikiTaskService,
        ILogger<TasksController> logger)
    {
        _repoRepo = repoRepo;
        _askTaskService = askTaskService;
        _slidesTaskService = slidesTaskService;
        _workshopTaskService = workshopTaskService;
        _taskRepo = taskRepo;
        _wikiTaskService = wikiTaskService;
        _logger = logger;
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
            var result = await _askTaskService.AskAsync(
                new AskTaskExecutionRequest
                {
                    Options = BuildVersionedTaskOptions(request, repo, repoId),
                    Question = request.Question,
                    History = request.History?.Select(message => new TaskConversationMessage
                    {
                        Role = message.Role,
                        Content = message.Content
                    }).ToList() ?? [],
                    DeepResearch = request.DeepResearch,
                    FilePath = request.FilePath
                },
                HttpContext.RequestAborted);

            return Ok(new
            {
                content = result.Content,
                stages = result.Stages.Select(stage => new
                {
                    title = stage.Title,
                    content = stage.Content,
                    iteration = stage.Iteration,
                    type = stage.Type
                }),
                complete = result.Complete,
                iterations = result.Iterations,
                repository_version_id = result.RepositoryVersionId,
                wiki_version_id = result.WikiVersionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ask 请求处理失败");
            return StatusCode(500, new { error = $"问答处理失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// POST /tasks/ask/stream — AI 流式问答（SSE），基于 IChatClient.GetStreamingResponseAsync
    /// </summary>
    [HttpPost("ask/stream")]
    public async Task AskStream(CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        AskRequest request;
        try
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            request = JsonSerializer.Deserialize<AskRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AskRequest();
        }
        catch
        {
            request = new AskRequest();
        }

        if (string.IsNullOrWhiteSpace(request.RepositoryId) || !Guid.TryParse(request.RepositoryId, out var repoId))
        {
            await WriteSseError("repository_id 是必填字段", ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            await WriteSseError("question 是必填字段", ct);
            return;
        }

        try
        {
            var repo = await _repoRepo.GetByIdAsync(repoId);
            if (repo is null)
            {
                await WriteSseError("仓库不存在", ct);
                return;
            }

            var executionRequest = new AskTaskExecutionRequest
            {
                Options = BuildVersionedTaskOptions(request, repo, repoId),
                Question = request.Question,
                History = request.History?.Select(message => new TaskConversationMessage
                {
                    Role = message.Role,
                    Content = message.Content
                }).ToList() ?? [],
                DeepResearch = request.DeepResearch,
                FilePath = request.FilePath
            };

            await foreach (var update in _askTaskService.AskStreamingAsync(executionRequest, ct))
            {
                if (ct.IsCancellationRequested) break;

                if (!string.IsNullOrEmpty(update.Text))
                {
                    var sseData = $"data: {JsonSerializer.Serialize(new { content = update.Text })}\n\n";
                    await Response.WriteAsync(sseData, ct);
                    await Response.Body.FlushAsync(ct);
                }

                if (update.FinishReason.HasValue) break;
            }

            await Response.WriteAsync("event: done\ndata: [DONE]\n\n", ct);
        }
        catch (OperationCanceledException)
        {
            // 客户端断开
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式 Ask 失败");
            try
            {
                await WriteSseError(ex.Message, ct);
            }
            catch { /* 客户端已断开 */ }
        }
    }

    private async Task WriteSseError(string error, CancellationToken ct)
    {
        var msg = $"event: error\ndata: {JsonSerializer.Serialize(new { error })}\n\n";
        await Response.WriteAsync(msg, ct);
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
            var result = await _slidesTaskService.GenerateAsync(
                new SlidesTaskExecutionRequest
                {
                    Options = BuildVersionedTaskOptions(request, repo, repoId)
                },
                HttpContext.RequestAborted);

            return Ok(new
            {
                plan = result.Plan,
                slides = result.Slides.Select(slide => new
                {
                    id = slide.Id,
                    title = slide.Title,
                    content = slide.Content,
                    html = slide.Html
                }),
                repository_version_id = result.RepositoryVersionId,
                wiki_version_id = result.WikiVersionId
            });
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
            var result = await _workshopTaskService.GenerateAsync(
                new WorkshopTaskExecutionRequest
                {
                    Options = BuildVersionedTaskOptions(request, repo, repoId)
                },
                HttpContext.RequestAborted);

            return Ok(new
            {
                content = result.Content,
                repository_version_id = result.RepositoryVersionId,
                wiki_version_id = result.WikiVersionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workshop 生成失败");
            return StatusCode(500, new { error = $"训练营生成失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// POST /tasks/{id}/resume — 恢复中断的任务
    /// </summary>
    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> ResumeTask(Guid id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task is null) return NotFound(new { error = "任务不存在" });

        if (task.Status == "completed")
            return BadRequest(new { error = "任务已完成，无需恢复" });

        if (task.Status == "running")
            return Conflict(new { error = "任务正在运行中" });

        if (task.TaskType != "wiki")
            return BadRequest(new { error = "仅支持恢复 Wiki 生成任务" });

        try
        {
            task.ResumeCount++;
            task.Status = "running";
            task.ErrorMessage = null;
            task.UpdatedAt = DateTime.UtcNow;
            await _taskRepo.UpdateAsync(task);

            // 在后台恢复执行
            _ = Task.Run(async () =>
            {
                try
                {
                    await _wikiTaskService.ExecuteAsync(
                        task,
                        task.Repository?.RepoUrl ?? "",
                        "git",
                        null,
                        task.Provider,
                        task.Model,
                        null,
                        task.Language ?? "zh",
                        true,
                        HttpContext.RequestAborted,
                        task.SourceBranch,
                        task.RefreshStrategy ?? "comprehensive");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "任务手动恢复失败 TaskId={TaskId}", task.Id);
                }
            });

            return Ok(new { message = "任务已恢复执行", taskId = task.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复任务失败 TaskId={TaskId}", id);
            return StatusCode(500, new { error = $"恢复失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 将 API 请求模型转换为版本化派生任务通用选项。
    /// </summary>
    private static VersionedTaskExecutionOptions BuildVersionedTaskOptions(
        RepositoryScopedTaskRequest request,
        Heimdall.Core.Entities.Repository repository,
        Guid repositoryId)
    {
        var branch = string.IsNullOrWhiteSpace(request.Branch)
            ? repository.DefaultBranch ?? "main"
            : request.Branch;

        // 空字符串 → null，确保下游 ?? 运算符正确回退
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider;
        var model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model;
        var customModel = string.IsNullOrWhiteSpace(request.CustomModel) ? null : request.CustomModel;

        if (request.IsCustomModel == true)
        {
            return new VersionedTaskExecutionOptions
            {
                RepositoryId = repositoryId,
                RepositoryVersionId = request.RepositoryVersionId,
                WikiVersionId = request.WikiVersionId,
                Language = request.Language,
                Branch = branch,
                Provider = provider,
                CustomModel = customModel
            };
        }

        return new VersionedTaskExecutionOptions
        {
            RepositoryId = repositoryId,
            RepositoryVersionId = request.RepositoryVersionId,
            WikiVersionId = request.WikiVersionId,
            Language = request.Language,
            Branch = branch,
            Provider = provider,
            Model = model,
            CustomModel = null
        };
    }
}

/// <summary>
/// 仓库范围任务请求基类。
/// 该模型承载 Ask、Slides、Workshop 共享的版本继承与模型执行参数。
/// </summary>
public abstract class RepositoryScopedTaskRequest
{
    /// <summary>
    /// 仓库标识。
    /// </summary>
    [JsonPropertyName("repository_id")]
    public string? RepositoryId { get; set; }

    /// <summary>
    /// 私有仓库访问令牌。
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>
    /// 模型提供方。
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// 标准模型名称。
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// 自定义模型名称。
    /// </summary>
    [JsonPropertyName("custom_model")]
    public string? CustomModel { get; set; }

    /// <summary>
    /// 是否使用自定义模型。
    /// </summary>
    [JsonPropertyName("is_custom_model")]
    public bool? IsCustomModel { get; set; }

    /// <summary>
    /// 输出语言。
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// 期望绑定的分支名称。
    /// </summary>
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    /// <summary>
    /// 期望消费的 RepositoryVersion 标识。
    /// 指定后，Ask、Slides、Workshop 将优先绑定该仓库快照版本。
    /// </summary>
    [JsonPropertyName("repository_version_id")]
    public Guid? RepositoryVersionId { get; set; }

    /// <summary>
    /// 期望消费的 WikiVersion 标识。
    /// 指定后，Ask、Slides、Workshop 将优先绑定该 WikiVersion，并校验与 RepositoryVersion 的一致性。
    /// </summary>
    [JsonPropertyName("wiki_version_id")]
    public Guid? WikiVersionId { get; set; }
}

/// <summary>
/// Wiki 生成请求。
/// </summary>
public sealed class WikiGenerateRequest : RepositoryScopedTaskRequest
{
    /// <summary>
    /// 是否生成综合版 Wiki。
    /// </summary>
    [JsonPropertyName("comprehensive")]
    public bool Comprehensive { get; set; } = true;

    /// <summary>
    /// 是否强制刷新。
    /// </summary>
    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; set; }

    /// <summary>
    /// 刷新策略。
    /// </summary>
    [JsonPropertyName("refresh_strategy")]
    public string? RefreshStrategy { get; set; }

    /// <summary>
    /// 生成档位。
    /// </summary>
    [JsonPropertyName("generation_profile")]
    public string? GenerationProfile { get; set; }
}

/// <summary>
/// Ask 请求。
/// </summary>
public sealed class AskRequest : RepositoryScopedTaskRequest
{
    /// <summary>
    /// 用户问题。
    /// </summary>
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// 历史消息集合。
    /// </summary>
    [JsonPropertyName("history")]
    public List<AskMessage>? History { get; set; }

    /// <summary>
    /// 是否启用深度研究。
    /// </summary>
    [JsonPropertyName("deep_research")]
    public bool DeepResearch { get; set; }

    /// <summary>
    /// 可选关注文件路径。
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}

/// <summary>
/// Ask 历史消息。
/// </summary>
public sealed class AskMessage
{
    /// <summary>
    /// 消息角色。
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    /// <summary>
    /// 消息内容。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
