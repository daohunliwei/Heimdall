using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("ast_versions")]
public class AstVersion
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "repository_version_id")]
    public Guid RepositoryVersionId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(RepositoryVersionId))]
    public RepositoryVersion RepositoryVersion { get; set; } = null!;

    [SugarColumn(ColumnName = "branch_name", Length = 256)]
    public string BranchName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "commit_sha", Length = 64)]
    public string CommitSha { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "config_fingerprint", Length = 128)]
    public string ConfigFingerprint { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "projection_format_version", Length = 32)]
    public string ProjectionFormatVersion { get; set; } = "1.0";

    [SugarColumn(ColumnName = "status", Length = 16)]
    public string Status { get; set; } = "pending";

    [SugarColumn(ColumnName = "total_files")]
    public int TotalFiles { get; set; }

    [SugarColumn(ColumnName = "total_symbols")]
    public int TotalSymbols { get; set; }

    [SugarColumn(ColumnName = "total_call_edges")]
    public int TotalCallEdges { get; set; }

    [SugarColumn(ColumnName = "total_chunks")]
    public int TotalChunks { get; set; }

    [SugarColumn(ColumnName = "result_json", ColumnDataType = "text", IsNullable = true)]
    [Obsolete("已迁移到 Workspace 文件系统，请使用 ast_dir_path 定位文件")]
    public string? ResultJson { get; set; }

    [SugarColumn(ColumnName = "ast_dir_path", Length = 512, IsNullable = true)]
    public string? AstDirPath { get; set; }

    [SugarColumn(ColumnName = "symbol_names_json", ColumnDataType = "text", IsJson = true, IsNullable = true)]
    public string? SymbolNamesJson { get; set; }

    [SugarColumn(ColumnName = "file_list_json", ColumnDataType = "text", IsJson = true, IsNullable = true)]
    public string? FileListJson { get; set; }

    [SugarColumn(ColumnName = "error_message", ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "completed_at", IsNullable = true)]
    public DateTime? CompletedAt { get; set; }

    [Navigate(NavigateType.OneToMany, nameof(WikiVersion.AstVersionId))]
    public List<WikiVersion> WikiVersions { get; set; } = new();
}
