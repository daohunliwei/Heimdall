namespace Heimdall.Infrastructure.Models;

/// <summary>
/// Workspace 配置模型，从 HEIMDALL_WORKSPACE 环境变量读取根路径。
/// </summary>
public sealed class WorkspaceConfig
{
    public string RootPath { get; }

    public WorkspaceConfig()
    {
        var env = Environment.GetEnvironmentVariable("HEIMDALL_WORKSPACE");
        RootPath = !string.IsNullOrWhiteSpace(env)
            ? Path.GetFullPath(env.Trim())
            : Path.Combine(AppContext.BaseDirectory, "workspace");
    }
}
