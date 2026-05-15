namespace Heimdall.Core.Models;

/// <summary>
/// 代码结构索引条目——记录单个文件的元信息。
/// </summary>
public class CodeIndexEntry
{
    public string FilePath { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string FileType { get; init; } = "source";
    public long SizeBytes { get; init; }
    public List<string> DependencyHints { get; init; } = new();
    public int ImportanceScore { get; set; }
}

/// <summary>
/// 文件摘要结果。
/// </summary>
public class FileSummary
{
    public string FilePath { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public int TokenCount { get; init; }
}

/// <summary>
/// 模块摘要结果。
/// </summary>
public class ModuleSummary
{
    public string ModuleName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<string> KeyFiles { get; init; } = new();
    public int FileCount { get; init; }
}

/// <summary>
/// 系统级摘要结果。
/// </summary>
public class SystemSummary
{
    public string ProjectType { get; init; } = string.Empty;
    public string TechStack { get; init; } = string.Empty;
    public string ArchitectureOverview { get; init; } = string.Empty;
    public List<string> CoreComponents { get; init; } = new();
    public int TotalFileCount { get; init; }
    public int ModuleCount { get; init; }
    public int EntryPointCount { get; init; }
}
