namespace Heimdall.Core.Interfaces.Services;

/// <summary>刷新编排服务接口</summary>
public interface IRefreshOrchestrationService
{
    /// <summary>刷新结果</summary>
    Task<RefreshResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
}

public class RefreshRequest
{
    public Guid RepositoryId { get; set; }
    public string Branch { get; set; } = "main";
    public string RefreshStrategy { get; set; } = "latest"; // current / latest
    public bool ForceRefresh { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string Language { get; set; } = "zh";
    public string GenerationProfile { get; set; } = "comprehensive";
}

public class RefreshResult
{
    public Guid? TaskId { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid? RepositoryVersionId { get; set; }
    public Guid? WikiVersionId { get; set; }
    /// <summary>queued / reused / no_change</summary>
    public string ResultType { get; set; } = "queued";
    public string RefreshStrategy { get; set; } = "latest";
    /// <summary>changed / unchanged</summary>
    public string ChangeStatus { get; set; } = "changed";
    public string? Message { get; set; }
}
