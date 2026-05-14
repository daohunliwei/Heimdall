using System.Security.Cryptography;
using System.Text;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Rag;

/// <summary>Wiki 嵌入服务 — 对页面内容分块、生成嵌入向量、写入 wiki_embedding_chunks</summary>
public class WikiEmbeddingService : IWikiEmbeddingService
{
    private readonly IWikiEmbeddingRepository _wikiEmbeddingRepo;
    private readonly ProviderRegistry _providerRegistry;
    private readonly TextUtilityService _textUtility;
    private readonly ILogger<WikiEmbeddingService> _logger;

    public WikiEmbeddingService(
        IWikiEmbeddingRepository wikiEmbeddingRepo,
        ProviderRegistry providerRegistry,
        TextUtilityService textUtility,
        ILogger<WikiEmbeddingService> logger)
    {
        _wikiEmbeddingRepo = wikiEmbeddingRepo;
        _providerRegistry = providerRegistry;
        _textUtility = textUtility;
        _logger = logger;
    }

    public async Task<int> EmbedWikiPagesAsync(
        Guid wikiVersionId, List<WikiPage> pages, CancellationToken ct = default)
    {
        var embedder = _providerRegistry.ResolveEmbeddingProvider();
        var embedderType = embedder.EmbedderType;
        var chunks = new List<WikiEmbeddingChunk>();

        foreach (var page in pages)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(page.ContentMarkdown))
                continue;

            // 1. 标题向量块
            if (!string.IsNullOrWhiteSpace(page.Title))
            {
                var titleHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(page.Title))).ToLowerInvariant();

                float[]? titleVector = null;
                byte[]? titleBytes = null;
                try
                {
                    titleVector = await embedder.EmbedAsync(page.Title, ct);
                    titleBytes = TextUtilityService.ConvertFloatsToBytes(titleVector);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "标题嵌入失败 Page={Title}", page.Title);
                }

                chunks.Add(new WikiEmbeddingChunk
                {
                    WikiVersionId = wikiVersionId,
                    WikiPageId = page.Id,
                    ChunkIndex = chunks.Count,
                    ChunkType = "title",
                    ContentRaw = page.Title,
                    ContentHash = titleHash,
                    TokenCount = _textUtility.EstimateTokenCount(page.Title),
                    EmbeddingModel = embedderType,
                    EmbeddingVector = titleBytes
                });
            }

            // 2. 摘要向量块
            if (!string.IsNullOrWhiteSpace(page.Summary))
            {
                var summaryHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(page.Summary))).ToLowerInvariant();

                float[]? summaryVector = null;
                byte[]? summaryBytes = null;
                try
                {
                    summaryVector = await embedder.EmbedAsync(page.Summary, ct);
                    summaryBytes = TextUtilityService.ConvertFloatsToBytes(summaryVector);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "摘要嵌入失败 Page={Title}", page.Title);
                }

                chunks.Add(new WikiEmbeddingChunk
                {
                    WikiVersionId = wikiVersionId,
                    WikiPageId = page.Id,
                    ChunkIndex = chunks.Count,
                    ChunkType = "summary",
                    ContentRaw = page.Summary,
                    ContentHash = summaryHash,
                    TokenCount = _textUtility.EstimateTokenCount(page.Summary),
                    EmbeddingModel = embedderType,
                    EmbeddingVector = summaryBytes
                });
            }

            // 3. 正文分块嵌入
            var textChunks = _textUtility.SplitByCharacters(page.ContentMarkdown, 500, 100);
            for (var i = 0; i < textChunks.Count; i++)
            {
                var chunkText = textChunks[i];
                if (string.IsNullOrWhiteSpace(chunkText)) continue;

                var contentHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(chunkText))).ToLowerInvariant();

                float[]? vector = null;
                byte[]? vectorBytes = null;
                try
                {
                    vector = await embedder.EmbedAsync(chunkText, ct);
                    vectorBytes = TextUtilityService.ConvertFloatsToBytes(vector);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "正文嵌入失败 Page={Title} Chunk={Index}", page.Title, i);
                }

                chunks.Add(new WikiEmbeddingChunk
                {
                    WikiVersionId = wikiVersionId,
                    WikiPageId = page.Id,
                    ChunkIndex = chunks.Count,
                    ChunkType = "section",
                    ContentRaw = chunkText,
                    ContentHash = contentHash,
                    TokenCount = _textUtility.EstimateTokenCount(chunkText),
                    EmbeddingModel = embedderType,
                    EmbeddingVector = vectorBytes
                });
            }
        }

        if (chunks.Count > 0)
        {
            await _wikiEmbeddingRepo.DeleteByVersionIdAsync(wikiVersionId);
            await _wikiEmbeddingRepo.AddRangeAsync(chunks);
        }

        _logger.LogInformation(
            "Wiki 嵌入完成 VersionId={VersionId} Pages={PageCount} Chunks={ChunkCount}",
            wikiVersionId, pages.Count, chunks.Count);

        return chunks.Count;
    }

    public async Task<int> GetChunkCountAsync(Guid wikiVersionId, CancellationToken ct = default)
    {
        var chunks = await _wikiEmbeddingRepo.GetByVersionIdAsync(wikiVersionId);
        return chunks.Count;
    }
}
