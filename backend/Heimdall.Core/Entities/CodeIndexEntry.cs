using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("code_index_entries")]
public class CodeIndexEntry
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "file_path", Length = 1024)]
    public string FilePath { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "module_name", Length = 256)]
    public string ModuleName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "file_type", Length = 64)]
    public string FileType { get; set; } = "source";

    [SugarColumn(ColumnName = "language", Length = 64, IsNullable = true)]
    public string Language { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "size_bytes")]
    public long SizeBytes { get; set; }

    [SugarColumn(ColumnName = "importance_score")]
    public int ImportanceScore { get; set; }

    [SugarColumn(ColumnName = "exported_symbols", ColumnDataType = "text", IsJson = true)]
    public string ExportedSymbolsJson { get; set; } = "[]";

    [SugarColumn(ColumnName = "dependency_hints", ColumnDataType = "text", IsJson = true)]
    public string DependencyHintsJson { get; set; } = "[]";

    [SugarColumn(ColumnName = "call_graph_json", ColumnDataType = "text", IsNullable = true)]
    public string? CallGraphJson { get; set; }

    [SugarColumn(ColumnName = "dependency_edges_json", ColumnDataType = "text", IsNullable = true)]
    public string? DependencyEdgesJson { get; set; }

    [SugarColumn(ColumnName = "design_pattern_hints", ColumnDataType = "text", IsNullable = true)]
    public string? DesignPatternHints { get; set; }

    [SugarColumn(ColumnName = "repository_version_id")]
    public Guid RepositoryVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryVersionId))]
    public RepositoryVersion? RepositoryVersion { get; set; }

    [Navigate(NavigateType.OneToMany, nameof(CodeIndexChunk.CodeIndexEntryId))]
    public List<CodeIndexChunk> Chunks { get; set; } = new();
}

[SugarTable("code_index_chunks")]
public class CodeIndexChunk
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "content", ColumnDataType = "text")]
    public string Content { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "start_line")]
    public int StartLine { get; set; }

    [SugarColumn(ColumnName = "end_line")]
    public int EndLine { get; set; }

    [SugarColumn(ColumnName = "language", Length = 64)]
    public string Language { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "code_index_entry_id")]
    public Guid CodeIndexEntryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(CodeIndexEntryId))]
    public CodeIndexEntry? CodeIndexEntry { get; set; }
}
