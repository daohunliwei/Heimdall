namespace Heimdall.Core.Interfaces.Services;

/// <summary>Dashboard: aggregated system statistics for the home page.</summary>
public interface IDashboardService
{
    /// <summary>Get aggregated dashboard statistics.</summary>
    Task<DashboardStats> GetDashboardStatsAsync();
}

/// <summary>Aggregated dashboard statistics.</summary>
public class DashboardStats
{
    public int TotalRepositories { get; init; }
    public int TotalWikis { get; init; }
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int FailedTasks { get; init; }
    public int PendingTasks { get; init; }
    public int TotalUsers { get; init; }
    public long TotalTokensUsed { get; init; }
}
