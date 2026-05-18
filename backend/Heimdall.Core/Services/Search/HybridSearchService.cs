using System.Collections.Concurrent;
using System.Text;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Search;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Search;

/// <summary>
/// 混合检索引擎——组合 BM25 精确匹配 + 向量语义搜索。
/// 在页面生成时按需检索真实代码片段。
/// </summary>
public sealed class HybridSearchService : IHybridSearchService
{
    private readonly Bm25SearchService _bm25;
    private readonly ILogger<HybridSearchService> _logger;

    // 任务级缓存：同一 Wiki 生成任务内复用结果
    private readonly ConcurrentDictionary<string, List<HybridSearchResult>> _searchCache = new();
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

        _logger.LogInformation("混合检索引擎索引构建完成：{Key}, {Count} 文档", indexKey, documents.Count);
        await Task.CompletedTask;
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

        // 合并结果（当前仅 BM25；向量搜索后续集成）
        var merged = MergeResults(bm25Results, keyFilePaths, topK);

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

    // ── 结果融合 ──

    private List<HybridSearchResult> MergeResults(
        List<Bm25Result> bm25Results,
        List<string>? keyFilePaths,
        int topK)
    {
        var merged = new Dictionary<string, HybridSearchResult>();

        // BM25 结果
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
