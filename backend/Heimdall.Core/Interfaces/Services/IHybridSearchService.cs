namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// 混合检索结果。
/// </summary>
public class HybridSearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public double Bm25Score { get; set; }
    public double? VectorScore { get; set; }
    public double CombinedScore { get; set; }
}

public interface IHybridSearchService
{
    /// <summary>
    /// 构建搜索引擎：BM25 索引 + 向量嵌入。
    /// </summary>
    Task BuildIndexAsync(
        string indexKey,
        List<CodeSnippetInput> snippets,
        CancellationToken ct = default);

    /// <summary>
    /// 执行混合检索（BM25 + 向量搜索）。
    /// </summary>
    Task<List<HybridSearchResult>> SearchAsync(
        string indexKey,
        string query,
        List<string>? keyFilePaths = null,
        int topK = 20,
        int maxTotalTokens = 20_000,
        CancellationToken ct = default);

    /// <summary>
    /// 将检索结果格式化为注入提示词的 Markdown 代码块。
    /// </summary>
    string FormatForPrompt(List<HybridSearchResult> results);
}

public class CodeSnippetInput
{
    public string FilePath { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Symbols { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
}
