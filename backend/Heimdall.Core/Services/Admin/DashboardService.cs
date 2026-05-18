using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;

namespace Heimdall.Core.Services.Admin;

/// <summary>
/// 管理仪表盘服务，聚合任务、用户、仓库、Wiki 版本统计。
/// V4：已移除旧 Wiki 实体依赖，使用 Wiki 任务数量替代 Wiki 计数。
/// </summary>
public sealed class DashboardService
{
    private readonly ITaskRepository _taskRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRepositoryConfigRepository _repoConfigRepo;

    /// <summary>
    /// 初始化仪表盘服务。
    /// </summary>
    public DashboardService(
        ITaskRepository taskRepo,
        IUserRepository userRepo,
        IRepositoryConfigRepository repoConfigRepo)
    {
        _taskRepo = taskRepo;
        _userRepo = userRepo;
        _repoConfigRepo = repoConfigRepo;
    }

    /// <summary>
    /// 获取仪表盘聚合统计数据。
    /// </summary>
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var (allTasks, _) = await _taskRepo.GetAllAsync(null, null, null, 0, int.MaxValue);
        var allUsers = await _userRepo.GetAllAsync();
        var allRepos = await _repoConfigRepo.GetAllAsync();

        var completedTasks = allTasks.Count(t => t.Status == "completed");
        var failedTasks = allTasks.Count(t => t.Status == "failed");
        var totalTasks = allTasks.Count;
        var totalTokens = allTasks.Sum(t => (long)(t.TotalPromptTokens + t.TotalCompletionTokens));
        // V4：Wiki 数量统计使用已完成的 Wiki 任务数替代旧 Wiki 实体计数
        var totalWikiTasks = allTasks.Count(t => t.TaskType == "wiki" && t.Status == "completed");

        return new DashboardStats
        {
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            FailedTasks = failedTasks,
            ActiveUsers = allUsers.Count(u => u.IsActive),
            TotalRepositories = allRepos.Count,
            TotalWikis = totalWikiTasks,
            SuccessRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 100,
            TotalTokensUsed = totalTokens
        };
    }
}
