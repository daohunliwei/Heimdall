namespace Heimdall.Infrastructure.Logging;

/// <summary>
/// 日志类别过滤器单例 — 运行时控制 EF Core SQL 日志和结构化日志的输出。
/// </summary>
public sealed class LogCategoryFilter
{
    /// <summary>是否输出 SQL 命令日志 (Microsoft.EntityFrameworkCore.Database.Command)</summary>
    public bool ShowSqlCommands { get; set; }

    /// <summary>是否输出 EF Core 基础架构日志 (Microsoft.EntityFrameworkCore)</summary>
    public bool ShowEfCore { get; set; }

    /// <summary>是否输出结构化进度日志</summary>
    public bool ShowStructuredProgress { get; set; } = true;
}
