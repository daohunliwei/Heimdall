namespace Heimdall.Core.Entities;

/// <summary>
/// 代码索引条目——数据库持久化实体。
/// </summary>
public class CodeIndexEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string FileType { get; set; } = "source";
    public string Language { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int ImportanceScore { get; set; }
    public string ExportedSymbolsJson { get; set; } = "[]";
    public string DependencyHintsJson { get; set; } = "[]";

    /// <summary>方法级调用关系 JSON（V7: CallEdge 数组序列化）。</summary>
    public string? CallGraphJson { get; set; }

    /// <summary>模块间依赖边 JSON（V7: DependencyEdge 数组序列化）。</summary>
    public string? DependencyEdgesJson { get; set; }

    /// <summary>设计模式启发式提示（V7: 逗号分隔的模式名，如 "Factory,Strategy"）。</summary>
    public string? DesignPatternHints { get; set; }

    // 版本关联
    public Guid RepositoryVersionId { get; set; }
    public RepositoryVersion? RepositoryVersion { get; set; }

    // 关联的分块
    public List<CodeIndexChunk> Chunks { get; set; } = new();
}

/// <summary>
/// 代码索引分块——嵌入和检索的基本单元。
/// </summary>
public class CodeIndexChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string Language { get; set; } = string.Empty;

    public Guid CodeIndexEntryId { get; set; }
    public CodeIndexEntry? CodeIndexEntry { get; set; }
}
