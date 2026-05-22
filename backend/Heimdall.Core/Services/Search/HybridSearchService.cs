using System.Collections.Concurrent;
using System.Text;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Search;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Search;

/// <summary>
/// 检索引擎——基于 BM25 精确匹配检索代码片段。
/// 在页面生成时按需检索真实代码。
/// </summary>
public sealed class HybridSearchService : IHybridSearchService
{
    private readonly Bm25SearchService _bm25;
    private readonly ILogger<HybridSearchService> _logger;

    private readonly ConcurrentDictionary<string, List<HybridSearchResult>> _searchCache = new();
    private const int CharsPerToken = 4;

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

        _logger.LogInformation("检索引擎索引构建完成：{Key}, {Count} 文档", indexKey, documents.Count);
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
        var cacheKey = $"{indexKey}:{query}:{topK}";
        if (_searchCache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogDebug("从缓存返回检索结果：{Key}", cacheKey);
            return cached;
        }

        var bm25Results = _bm25.Search(indexKey, query, topK * 2);
        _logger.LogDebug("BM25 命中 {Count} 条：{Query}", bm25Results.Count, query);

        var scored = ScoreResults(bm25Results, keyFilePaths, topK);
        var final = ApplyTokenBudget(scored, maxTotalTokens);

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
            foreach (var result in group)
            {
                var lang = string.IsNullOrWhiteSpace(result.Language) ? "text" : result.Language.ToLowerInvariant();
                sb.AppendLine($"**文件**: `{result.FilePath}` (行 {result.StartLine}-{result.EndLine})  [{lang}]");
                sb.AppendLine();
                sb.AppendLine("```" + lang);
                sb.AppendLine(result.Content);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static List<HybridSearchResult> ScoreResults(
        List<Bm25Result> bm25Results,
        List<string>? keyFilePaths,
        int topK)
    {
        var scored = bm25Results.Select(r => new HybridSearchResult
        {
            FilePath = r.FilePath,
            ModuleName = r.ModuleName,
            Content = r.Content,
            Language = r.Language,
            StartLine = r.StartLine,
            EndLine = r.EndLine,
            Bm25Score = r.Score,
            CombinedScore = r.Score
        }).ToList();

        if (keyFilePaths is { Count: > 0 })
        {
            foreach (var entry in scored)
            {
                if (keyFilePaths.Any(kf => entry.FilePath.Contains(kf, StringComparison.OrdinalIgnoreCase)))
                    entry.CombinedScore += 2.0;
            }
        }

        return scored
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
