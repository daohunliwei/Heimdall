using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>Wiki 嵌入服务接口 — 页面内容分块、嵌入生成、批量写入 wiki_embedding_chunks</summary>
public interface IWikiEmbeddingService
{
    /// <summary>对 Wiki 页面内容进行分块、嵌入并写入 wiki_embedding_chunks</summary>
    Task<int> EmbedWikiPagesAsync(
        Guid wikiVersionId, List<WikiPage> pages, CancellationToken ct = default);

    /// <summary>获取指定 Wiki 版本的嵌入块数</summary>
    Task<int> GetChunkCountAsync(Guid wikiVersionId, CancellationToken ct = default);
}
