namespace Heimdall.Core.Models;

/// <summary>
/// 代码结构索引条目——记录单个文件的元信息（本地索引，不涉及 LLM）。
/// </summary>
public class CodeIndexEntry
{
    public string FilePath { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string FileType { get; init; } = "source";
    public long SizeBytes { get; init; }
    public List<string> DependencyHints { get; init; } = new();
    public int ImportanceScore { get; set; }
    public string Language { get; init; } = string.Empty;
    public List<string> ExportedSymbols { get; init; } = new();
}
