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

    // 向量嵌入（pgvector bytea 格式）
    public byte[]? Embedding { get; set; }

    public Guid CodeIndexEntryId { get; set; }
    public CodeIndexEntry? CodeIndexEntry { get; set; }
}
