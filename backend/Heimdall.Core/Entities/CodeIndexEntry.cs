using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("code_index_entries")]
public class CodeIndexEntry
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [SugarColumn(Length = 1024)]
    public string FilePath { get; set; } = string.Empty;

    [SugarColumn(Length = 256)]
    public string ModuleName { get; set; } = string.Empty;

    [SugarColumn(Length = 64)]
    public string FileType { get; set; } = "source";

    [SugarColumn(Length = 64, IsNullable = true)]
    public string Language { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
    public int ImportanceScore { get; set; }

    [SugarColumn(ColumnName = "exported_symbols", ColumnDataType = "text")]
    public string ExportedSymbolsJson { get; set; } = "[]";

    [SugarColumn(ColumnName = "dependency_hints", ColumnDataType = "text")]
    public string DependencyHintsJson { get; set; } = "[]";

    [SugarColumn(IsNullable = true)]
    public string? CallGraphJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DependencyEdgesJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DesignPatternHints { get; set; }

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
    public Guid Id { get; set; } = Guid.NewGuid();

    [SugarColumn(ColumnDataType = "text")]
    public string Content { get; set; } = string.Empty;

    public int StartLine { get; set; }
    public int EndLine { get; set; }

    [SugarColumn(Length = 64)]
    public string Language { get; set; } = string.Empty;

    public Guid CodeIndexEntryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(CodeIndexEntryId))]
    public CodeIndexEntry? CodeIndexEntry { get; set; }
}
