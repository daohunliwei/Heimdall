namespace Heimdall.Core.Entities;

/// <summary>Wiki 内容向量块 — 服务于内容检索、语义导航</summary>
public class WikiEmbeddingChunk
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>所属 Wiki 版本</summary>
    public Guid WikiVersionId { get; set; }
    public WikiVersion WikiVersion { get; set; } = null!;
    /// <summary>所属页面</summary>
    public Guid WikiPageId { get; set; }
    public WikiPage WikiPage { get; set; } = null!;
    /// <summary>块序号</summary>
    public int ChunkIndex { get; set; }
    /// <summary>块类型：title / summary / section / faq / table_text</summary>
    public string ChunkType { get; set; } = "section";
    /// <summary>原始文本</summary>
    public string ContentRaw { get; set; } = string.Empty;
    /// <summary>内容哈希</summary>
    public string ContentHash { get; set; } = string.Empty;
    /// <summary>Token 数</summary>
    public int? TokenCount { get; set; }
    /// <summary>使用的嵌入模型</summary>
    public string? EmbeddingModel { get; set; }
    /// <summary>向量（byte[] 格式，与现有 embedding_documents 表一致）</summary>
    public byte[]? EmbeddingVector { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
