namespace Heimdall.Core.Models;

public class DashboardStats
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalRepositories { get; set; }
    public int TotalWikis { get; set; }
    public double SuccessRate { get; set; }
    public long TotalTokensUsed { get; set; }
}
