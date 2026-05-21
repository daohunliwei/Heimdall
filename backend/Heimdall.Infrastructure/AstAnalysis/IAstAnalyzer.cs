namespace Heimdall.Infrastructure.AstAnalysis;

public record AstSymbol(
    string Name, string Kind, string FullSignature, string FilePath,
    int StartLine, int EndLine, string? ParentClass, string[]? Modifiers,
    string[]? BaseTypes, string[]? AttributeAnnotations);

public record AstCallEdge(
    string CallerSymbol, string CallerFilePath,
    string CalleeSymbol, string CalleeFilePath,
    string CallType, double Confidence);

public record AstFileResult(
    string FilePath, string Language,
    List<AstSymbol> Symbols,
    List<AstCallEdge> CallEdges,
    List<SourceChunk> Chunks,
    List<string> DesignPatternHints);

public record SourceChunk(int StartLine, int EndLine, string Label, string Content);

public interface IAstAnalyzer
{
    string Language { get; }
    bool CanAnalyze(string fileExtension);
    Task<AstFileResult> AnalyzeAsync(string filePath, string source, CancellationToken ct = default);
}
