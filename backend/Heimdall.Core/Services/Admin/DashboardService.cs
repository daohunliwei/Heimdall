using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;

namespace Heimdall.Core.Services.Admin;

public sealed class DashboardService
{
    private readonly ITaskRepository _taskRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRepositoryConfigRepository _repoConfigRepo;
    private readonly IWikiRepository _wikiRepo;

    public DashboardService(
        ITaskRepository taskRepo,
        IUserRepository userRepo,
        IRepositoryConfigRepository repoConfigRepo,
        IWikiRepository wikiRepo)
    {
        _taskRepo = taskRepo;
        _userRepo = userRepo;
        _repoConfigRepo = repoConfigRepo;
        _wikiRepo = wikiRepo;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var (allTasks, _) = await _taskRepo.GetAllAsync(null, null, null, 0, int.MaxValue);
        var allUsers = await _userRepo.GetAllAsync();
        var allRepos = await _repoConfigRepo.GetAllAsync();

        var completedTasks = allTasks.Count(t => t.Status == "completed");
        var failedTasks = allTasks.Count(t => t.Status == "failed");
        var totalTasks = allTasks.Count;
        var totalTokens = allTasks.Sum(t => (long)(t.TotalPromptTokens + t.TotalCompletionTokens));

        return new DashboardStats
        {
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            FailedTasks = failedTasks,
            ActiveUsers = allUsers.Count(u => u.IsActive),
            TotalRepositories = allRepos.Count,
            TotalWikis = 0, // 需要从 wiki repo 查询
            SuccessRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 100,
            TotalTokensUsed = totalTokens
        };
    }
}
