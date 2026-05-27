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
        var (totalTasks, completedTasks, failedTasks, totalWikiTasks, totalTokens) = await _taskRepo.GetStatisticsAsync();
        var activeUsers = await _userRepo.CountActiveAsync();
        var totalRepos = await _repoConfigRepo.CountAsync();

        return new DashboardStats
        {
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            FailedTasks = failedTasks,
            ActiveUsers = activeUsers,
            TotalRepositories = totalRepos,
            TotalWikis = totalWikiTasks,
            SuccessRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 100,
            TotalTokensUsed = totalTokens
        };
    }
}
