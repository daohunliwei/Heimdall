using Heimdall.Core.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 任务恢复服务——启动时扫描僵尸任务并自动恢复。
/// 作为 <see cref="IHostedService"/> 在应用启动完成后执行。
/// </summary>
public sealed class TaskResumeService : IHostedService
{
    private const int ZombieThresholdMinutes = 5;
    private const int MaxAutoRetryCount = 3;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskResumeService> _logger;

    public TaskResumeService(IServiceScopeFactory scopeFactory, ILogger<TaskResumeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Resume] 启动时扫描需要恢复的任务...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

            // 扫描 Running 状态且超过阈值无更新的任务
            var zombieThreshold = DateTime.UtcNow.AddMinutes(-ZombieThresholdMinutes);
            var (runningTasks, _) = await taskRepo.GetAllAsync(
                status: "running", taskType: "wiki", limit: 200);
            var zombieTasks = runningTasks
                .Where(t => t.UpdatedAt < zombieThreshold
                    && t.AutoResumeFailCount < MaxAutoRetryCount)
                .OrderBy(t => t.CreatedAt)
                .ToList();

            if (zombieTasks.Count == 0)
            {
                _logger.LogDebug("[Resume] 扫描完成——无可恢复的僵尸任务");
                return;
            }

            _logger.LogInformation("[Resume] 发现 {Count} 个可恢复的僵尸任务，按创建时间顺序恢复",
                zombieTasks.Count);

            var wikiTaskService = scope.ServiceProvider.GetRequiredService<WikiTaskService>();

            foreach (var zombie in zombieTasks)
            {
                try
                {
                    _logger.LogInformation("[Resume] 正在恢复任务 TaskId={TaskId} Stage={Stage}",
                        zombie.Id, zombie.CurrentStage);

                    zombie.ResumeCount++;
                    zombie.AutoResumeFailCount++;
                    zombie.Status = "running";
                    zombie.ErrorMessage = null;
                    zombie.UpdatedAt = DateTime.UtcNow;
                    await taskRepo.UpdateAsync(zombie);

                    // 在后台线程中恢复执行
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 从任务记录中恢复执行上下文
                            var repoUrl = zombie.ResultJson is not null
                                ? ExtractRepoUrl(zombie)
                                : null;
                            if (string.IsNullOrEmpty(repoUrl))
                            {
                                _logger.LogWarning("[Resume] 无法从任务记录提取仓库 URL TaskId={TaskId}", zombie.Id);
                                zombie.Status = "failed";
                                zombie.ErrorMessage = "无法恢复：缺少仓库 URL";
                                zombie.UpdatedAt = DateTime.UtcNow;
                                await taskRepo.UpdateAsync(zombie);
                                return;
                            }

                            await wikiTaskService.ExecuteAsync(
                                zombie,
                                repoUrl,
                                "git",
                                null,
                                zombie.Provider,
                                zombie.Model,
                                null,
                                zombie.Language ?? "zh",
                                true,
                                CancellationToken.None,
                                zombie.SourceBranch,
                                zombie.RefreshStrategy ?? "comprehensive");
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("[Resume] 任务恢复被取消 TaskId={TaskId}", zombie.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[Resume] 任务自动恢复失败 TaskId={TaskId}", zombie.Id);
                            zombie.Status = "failed";
                            zombie.ErrorMessage = $"自动恢复失败：{ex.Message}";
                            zombie.UpdatedAt = DateTime.UtcNow;
                            await taskRepo.UpdateAsync(zombie);
                        }
                    }, cancellationToken);

                    _logger.LogInformation("[Resume] 任务已恢复执行 TaskId={TaskId}", zombie.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Resume] 恢复任务失败 TaskId={TaskId}", zombie.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Resume] 启动扫描期间发生异常");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string? ExtractRepoUrl(Entities.TaskRecord task)
    {
        // 从结果 JSON 或关联的仓库中提取 URL
        if (task.ResultJson is not null)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(task.ResultJson);
                if (doc.RootElement.TryGetProperty("repo_url", out var urlProp))
                    return urlProp.GetString();
            }
            catch { }
        }
        return task.Repository?.RepoUrl;
    }
}
