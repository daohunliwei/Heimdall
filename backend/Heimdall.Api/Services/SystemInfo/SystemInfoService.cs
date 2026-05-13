using Heimdall.Api.Models;

namespace Heimdall.Api.Services.SystemInfo;

/// <summary>
/// 负责构建系统首页与健康检查响应。
/// </summary>
public sealed class SystemInfoService
{
    private static readonly string[] Endpoints =
    [
        "/health",
        "/lang/config",
        "/auth/status",
        "/auth/validate",
        "/models/config",
        "/chat/completions/stream",
        "/local_repo/structure",
        "/export/wiki",
        "/api/wiki_cache",
        "/api/processed_projects"
    ];

    /// <summary>
    /// 构建首页响应。
    /// </summary>
    public AppInfoResponse BuildAppInfo()
    {
        return new AppInfoResponse
        {
            Message = "Heimdall C# 后端已启动",
            Runtime = ".NET 10",
            Endpoints = Endpoints.ToList()
        };
    }

    /// <summary>
    /// 构建健康检查响应。
    /// </summary>
    public HealthStatusResponse BuildHealthStatus()
    {
        return new HealthStatusResponse
        {
            Status = "healthy",
            Service = "heimdall-csharp-api",
            Runtime = ".NET 10",
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
