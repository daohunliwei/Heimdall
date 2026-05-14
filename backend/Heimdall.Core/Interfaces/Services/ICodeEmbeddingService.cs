using Heimdall.Core.Entities;
using Heimdall.Infrastructure.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>代码嵌入服务接口 — 文件分块、嵌入生成、批量写入 code_embedding_chunks</summary>
public interface ICodeEmbeddingService
{
    /// <summary>对仓库文件进行分块、嵌入并写入 code_embedding_chunks</summary>
    Task<int> EmbedRepositoryAsync(
        Guid repositoryVersionId, List<EmbeddedDocument> documents, CancellationToken ct = default);

    /// <summary>获取指定仓库版本的嵌入块数</summary>
    Task<int> GetChunkCountAsync(Guid repositoryVersionId, CancellationToken ct = default);
}
