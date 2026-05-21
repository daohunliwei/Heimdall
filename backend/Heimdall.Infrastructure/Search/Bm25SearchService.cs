using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Search;

/// <summary>
/// 轻量级 BM25 文本检索引擎——支持精确符号匹配。
/// 纯内存实现，不依赖 Lucene.NET。
/// </summary>
public sealed class Bm25SearchService
{
    private readonly ILogger<Bm25SearchService> _logger;
    private readonly ConcurrentDictionary<string, Bm25Index> _indexes = new();

    private const double K1 = 1.5;
    private const double B = 0.75;

    public Bm25SearchService(ILogger<Bm25SearchService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 构建 BM25 索引。
    /// </summary>
    public Bm25Index BuildIndex(string indexKey, List<Bm25Document> documents)
    {
        _logger.LogInformation("构建 BM25 索引：{Key}, {Count} 文档", indexKey, documents.Count);

        var index = new Bm25Index(indexKey);
        foreach (var doc in documents)
        {
            var tokens = Tokenize(doc.Content).Concat(Tokenize(doc.Title))
                .Concat(Tokenize(doc.Symbols ?? string.Empty))
                .Distinct()
                .ToList();
            index.AddDocument(doc, tokens);
        }
        index.FinishIndexing();

        _indexes[indexKey] = index;
        _logger.LogInformation("BM25 索引构建完成：{Key}, {DocCount} 文档, {AvgLen:F1} 平均长度",
            indexKey, index.DocumentCount, index.AverageDocLength);

        return index;
    }

    /// <summary>
    /// 执行 BM25 搜索。
    /// </summary>
    public List<Bm25Result> Search(string indexKey, string query, int topK = 20,
        string? filterModule = null, string? filterPath = null)
    {
        if (!_indexes.TryGetValue(indexKey, out var index))
        {
            _logger.LogWarning("BM25 索引未找到：{Key}", indexKey);
            return new List<Bm25Result>();
        }

        var queryTokens = Tokenize(query).Distinct().ToList();
        if (queryTokens.Count == 0) return new List<Bm25Result>();

        var results = new List<Bm25Result>();

        foreach (var doc in index.Documents.Values)
        {
            if (filterModule is not null && !doc.ModuleName.Equals(filterModule, StringComparison.OrdinalIgnoreCase))
                continue;
            if (filterPath is not null && !doc.FilePath.Contains(filterPath, StringComparison.OrdinalIgnoreCase))
                continue;

            var score = ScoreDocument(index, doc, queryTokens);
            if (score > 0)
            {
                results.Add(new Bm25Result
                {
                    FilePath = doc.FilePath,
                    ModuleName = doc.ModuleName,
                    Content = doc.Content,
                    Language = doc.Language,
                    StartLine = doc.StartLine,
                    EndLine = doc.EndLine,
                    Score = score
                });
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// 删除指定索引。
    /// </summary>
    public void RemoveIndex(string indexKey)
    {
        _indexes.TryRemove(indexKey, out _);
    }

    // ── BM25 评分 ──

    private static double ScoreDocument(Bm25Index index, Bm25Document doc, List<string> queryTokens)
    {
        var docLength = doc.TokenCount > 0 ? doc.TokenCount : 1;
        var score = 0.0;

        foreach (var token in queryTokens)
        {
            if (!index.TermDocumentFrequency.TryGetValue(token, out var df) || df == 0)
                continue;

            var tf = doc.TermFrequencies.GetValueOrDefault(token, 0);
            if (tf == 0) continue;

            var idf = Math.Log((index.DocumentCount - df + 0.5) / (df + 0.5) + 1.0);
            var numerator = tf * (K1 + 1);
            var denominator = tf + K1 * (1 - B + B * docLength / index.AverageDocLength);
            score += idf * numerator / denominator;
        }

        return score;
    }

    // ── 分词 ──

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var tokens = new List<string>();
        var lowerText = text.ToLowerInvariant();

        // 按空格/标点分割
        var parts = Regex.Split(lowerText, @"[\s.,;:!?()\[\]{}<>""'`/\\|@#$%^&*+=~-]+");
        foreach (var part in parts)
        {
            if (part.Length >= 2)
                tokens.Add(part);
        }

        // 驼峰分词：HelloWorld -> hello, world, helloworld
        var camelTokens = new List<string>();
        foreach (var part in parts.Where(p => p.Length > 2))
        {
            var subTokens = Regex.Replace(part, @"([a-z])([A-Z])", "$1 $2").Split(' ');
            foreach (var st in subTokens)
            {
                var lower = st.ToLowerInvariant();
                if (lower.Length >= 2)
                    camelTokens.Add(lower);
            }
        }
        tokens.AddRange(camelTokens);

        // V7: snake_case 变体展开：hello_world -> hello, world
        foreach (var part in parts.Where(p => p.Contains('_')))
        {
            var snakeParts = part.Split('_', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sp in snakeParts)
            {
                if (sp.Length >= 2)
                    tokens.Add(sp);
            }
        }

        // V7: 中文 bigram 索引
        var chineseChars = Regex.Matches(lowerText, @"[\u4e00-\u9fff]");
        if (chineseChars.Count >= 2)
        {
            var chars = chineseChars.Select(m => m.Value).ToList();
            for (var i = 0; i < chars.Count - 1; i++)
            {
                tokens.Add(chars[i] + chars[i + 1]);
            }
            // 单字也加入（用于精确匹配）
            foreach (var c in chars)
            {
                tokens.Add(c);
            }
        }

        return tokens.Distinct().ToList();
    }
}

// ── 数据模型 ──

public class Bm25Document
{
    public string FilePath { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Symbols { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }

    // 内部使用
    internal int TokenCount { get; set; }
    internal Dictionary<string, int> TermFrequencies { get; set; } = new();
}

public class Bm25Index
{
    public string IndexKey { get; }
    public Dictionary<string, Bm25Document> Documents { get; } = new();
    public Dictionary<string, int> TermDocumentFrequency { get; } = new();
    public int DocumentCount { get; private set; }
    public double AverageDocLength { get; private set; }

    public Bm25Index(string indexKey) => IndexKey = indexKey;

    public void AddDocument(Bm25Document doc, List<string> tokens)
    {
        Documents[doc.FilePath] = doc;
        doc.TokenCount = tokens.Count;
        doc.TermFrequencies = tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
    }

    public void FinishIndexing()
    {
        DocumentCount = Documents.Count;
        AverageDocLength = DocumentCount > 0 ? Documents.Values.Average(d => d.TokenCount) : 0;

        TermDocumentFrequency.Clear();
        foreach (var doc in Documents.Values)
        {
            foreach (var term in doc.TermFrequencies.Keys)
            {
                TermDocumentFrequency[term] = TermDocumentFrequency.GetValueOrDefault(term) + 1;
            }
        }
    }
}

public class Bm25Result
{
    public string FilePath { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public double Score { get; init; }
}
