using Heimdall.Infrastructure.AstAnalysis;

namespace Heimdall.Core.Models;

/// <summary>
/// AST 持久化投影——包含可直接序列化到 AstVersion 的全量解析结果与轻量索引数据。
/// </summary>
public class AstPersistenceProjection
{
    public List<AstFileResult> FileResults { get; init; } = new();
    public List<SymbolNameEntry> SymbolNames { get; init; } = new();
    public List<FileListEntry> FileList { get; init; } = new();
    public int TotalFiles { get; set; }
    public int TotalSymbols { get; set; }
    public int TotalCallEdges { get; set; }
    public int TotalChunks { get; set; }
}

public record SymbolNameEntry(string Name, string Kind, string File);

public record FileListEntry(string Path, string Language, int SymbolCount);
