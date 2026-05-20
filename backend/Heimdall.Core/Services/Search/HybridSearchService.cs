using System.Collections.Concurrent;
using System.Text;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Search;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Search;

/// <summary>
/// 混合检索引擎——组合 BM25 精确匹配 + 向量语义搜索（RRF 融合）。
/// 在页面生成时按需检索真实代码片段。
/// </summary>
public sealed class HybridSearchService : IHybridSearchService
{
    private readonly Bm25SearchService _bm25;
    private readonly ILogger<HybridSearchService> _logger;

    // 任务级缓存：同一 Wiki 生成任务内复用结果
    private readonly ConcurrentDictionary<string, List<HybridSearchResult>> _searchCache = new();
    // 向量索引可用性标记
    private readonly ConcurrentDictionary<string, bool> _vectorAvailable = new();
    // 向量搜索结果缓存（模拟，实际需 pgvector）
    private readonly ConcurrentDictionary<string, List<VectorSearchResult>> _vectorIndex = new();
    // 上下文 token 预算
    private const int CharsPerToken = 4;
    // RRF 融合参数
    private const int RrfK = 60;

    public HybridSearchService(Bm25SearchService bm25, ILogger<HybridSearchService> logger)
    {
        _bm25 = bm25;
        _logger = logger;
    }

    public async Task BuildIndexAsync(string indexKey, List<CodeSnippetInput> snippets, CancellationToken ct = default)
    {
        var documents = snippets.Select(s => new Bm25Document
        {
            FilePath = s.FilePath,
            ModuleName = s.ModuleName,
            Content = s.Content,
            Symbols = s.Symbols,
            Title = s.FilePath,
            Language = s.Language,
            StartLine = s.StartLine,
            EndLine = s.EndLine
        }).ToList();

        _bm25.BuildIndex(indexKey, documents);
        _searchCache.Clear();

        // V7: 标记向量索引尚不可用（嵌入需异步完成后才可用）
        _vectorAvailable[indexKey] = false;

        _logger.LogInformation("混合检索引擎索引构建完成：{Key}, {Count} 文档", indexKey, documents.Count);
        await Task.CompletedTask;
    }

    /// <summary>
    /// V7: 标记向量索引已可用（嵌入完成后调用）。
    /// </summary>
    public void MarkVectorIndexAvailable(string indexKey)
    {
        _vectorAvailable[indexKey] = true;
        _logger.LogInformation("向量索引已就绪：{Key}", indexKey);
    }

    /// <summary>
    /// V7: 注册向量搜索结果（供 RRF 融合使用）。
    /// 实际生产环境应直接查询 pgvector，此处为集成接口。
    /// </summary>
    public void RegisterVectorResults(string indexKey, string query, List<VectorSearchResult> results)
    {
        var cacheKey = $"vec:{indexKey}:{query}";
        _vectorIndex[cacheKey] = results;
    }

    public async Task<List<HybridSearchResult>> SearchAsync(
        string indexKey,
        string query,
        List<string>? keyFilePaths = null,
        int topK = 20,
        int maxTotalTokens = 20_000,
        CancellationToken ct = default)
    {
        // 检查缓存
        var cacheKey = $"{indexKey}:{query}:{topK}";
        if (_searchCache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogDebug("从缓存返回检索结果：{Key}", cacheKey);
            return cached;
        }

        // BM25 搜索
        var bm25Results = _bm25.Search(indexKey, query, topK * 2);
        _logger.LogDebug("BM25 命中 {Count} 条：{Query}", bm25Results.Count, query);

        // V7: 向量搜索（如果可用）
        List<VectorSearchResult>? vectorResults = null;
        if (_vectorAvailable.TryGetValue(indexKey, out var available) && available)
        {
            var vecCacheKey = $"vec:{indexKey}:{query}";
            _vectorIndex.TryGetValue(vecCacheKey, out vectorResults);
            if (vectorResults is not null)
                _logger.LogDebug("向量搜索命中 {Count} 条：{Query}", vectorResults.Count, query);
        }

        // RRF 融合（BM25 + 向量）
        var merged = MergeResultsWithRrf(bm25Results, vectorResults, keyFilePaths, topK);

        // Token 预算截断
        var final = ApplyTokenBudget(merged, maxTotalTokens);

        _searchCache[cacheKey] = final;
        return await Task.FromResult(final);
    }

    public string FormatForPrompt(List<HybridSearchResult> results)
    {
        if (results.Count == 0) return "（未找到相关源代码）";

        var sb = new StringBuilder();
        sb.AppendLine("## 相关源代码片段");
        sb.AppendLine();

        var byFile = results.GroupBy(r => r.FilePath);
        foreach (var group in byFile.OrderByDescending(g => g.First().CombinedScore))
        {
            foreach (var result in group.Take(3))
            {
                sb.AppendLine($"**文件**: `{result.FilePath}` (行 {result.StartLine}-{result.EndLine})");
                sb.AppendLine($"**语言**: {result.Language}  **相关性**: {result.CombinedScore:F2}");
                sb.AppendLine();
                sb.AppendLine("```" + result.Language);
                sb.AppendLine(result.Content);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // ── RRF 融合算法 ──

    /// <summary>
    /// V7: RRF (Reciprocal Rank Fusion) 算法融合 BM25 + 向量搜索结果。
    /// score = sum(1/(K + rank_i))，K = 60
    /// </summary>
    private List<HybridSearchResult> MergeResultsWithRrf(
        List<Bm25Result> bm25Results,
        List<VectorSearchResult>? vectorResults,
        List<string>? keyFilePaths,
        int topK)
    {
        var merged = new Dictionary<string, HybridSearchResult>();

        // BM25 结果 RRF 评分
        for (var i = 0; i < bm25Results.Count; i++)
        {
            var bm = bm25Results[i];
            var key = $"{bm.FilePath}:{bm.StartLine}";
            var rrfScore = 1.0 / (RrfK + i + 1);

            if (merged.TryGetValue(key, out var existing))
            {
                existing.Bm25Score = bm.Score;
                existing.CombinedScore += rrfScore;
            }
            else
            {
                merged[key] = new HybridSearchResult
                {
                    FilePath = bm.FilePath,
                    ModuleName = bm.ModuleName,
                    Content = bm.Content,
                    Language = bm.Language,
                    StartLine = bm.StartLine,
                    EndLine = bm.EndLine,
                    Bm25Score = bm.Score,
                    CombinedScore = rrfScore
                };
            }
        }

        // V7: 向量结果 RRF 评分
        if (vectorResults is { Count: > 0 })
        {
            for (var i = 0; i < vectorResults.Count; i++)
            {
                var vec = vectorResults[i];
                var key = $"{vec.FilePath}:{vec.StartLine}";
                var rrfScore = 1.0 / (RrfK + i + 1);

                if (merged.TryGetValue(key, out var existing))
                {
                    existing.VectorScore = vec.CosineSimilarity;
                    existing.CombinedScore += rrfScore;
                }
                else
                {
                    merged[key] = new HybridSearchResult
                    {
                        FilePath = vec.FilePath,
                        ModuleName = vec.ModuleName,
                        Content = vec.Content,
                        Language = vec.Language,
                        StartLine = vec.StartLine,
                        EndLine = vec.EndLine,
                        VectorScore = vec.CosineSimilarity,
                        CombinedScore = rrfScore
                    };
                }
            }
        }

        // 关键文件加分
        if (keyFilePaths is { Count: > 0 })
        {
            foreach (var entry in merged.Values)
            {
                if (keyFilePaths.Any(kf => entry.FilePath.Contains(kf, StringComparison.OrdinalIgnoreCase)))
                    entry.CombinedScore += 2.0;
            }
        }

        return merged.Values
            .OrderByDescending(r => r.CombinedScore)
            .Take(topK)
            .ToList();
    }

    private static List<HybridSearchResult> ApplyTokenBudget(List<HybridSearchResult> results, int maxTokens)
    {
        var selected = new List<HybridSearchResult>();
        var currentChars = 0;
        var maxChars = maxTokens * CharsPerToken;

        foreach (var result in results)
        {
            var charCount = result.Content.Length;
            if (currentChars + charCount > maxChars && selected.Count > 0)
                continue;

            selected.Add(result);
            currentChars += charCount;
        }

        return selected;
    }
}

/// <summary>
/// V7: 向量搜索结果。
/// </summary>
public class VectorSearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public double CosineSimilarity { get; set; }
}
