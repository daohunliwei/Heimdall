namespace Heimdall.Core.Entities;

/// <summary>代码向量块 — 服务于代码理解、Ask 问答底座</summary>
public class CodeEmbeddingChunk
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>所属仓库快照版本</summary>
    public Guid RepositoryVersionId { get; set; }
    public RepositoryVersion RepositoryVersion { get; set; } = null!;
    /// <summary>文件路径</summary>
    public string FilePath { get; set; } = string.Empty;
    /// <summary>可选：类/函数/命名空间路径</summary>
    public string? SymbolPath { get; set; }
    /// <summary>块序号</summary>
    public int ChunkIndex { get; set; }
    /// <summary>块类型：file_summary / code_block / symbol_body / readme</summary>
    public string ChunkType { get; set; } = "code_block";
    /// <summary>源码语言</summary>
    public string Language { get; set; } = string.Empty;
    /// <summary>起始行</summary>
    public int StartLine { get; set; }
    /// <summary>结束行</summary>
    public int EndLine { get; set; }
    /// <summary>原始块内容</summary>
    public string ContentRaw { get; set; } = string.Empty;
    /// <summary>规范化后文本</summary>
    public string ContentNormalized { get; set; } = string.Empty;
    /// <summary>内容哈希（用于增量更新）</summary>
    public string ContentHash { get; set; } = string.Empty;
    /// <summary>Token 数</summary>
    public int? TokenCount { get; set; }
    /// <summary>使用的嵌入模型</summary>
    public string? EmbeddingModel { get; set; }
    /// <summary>向量（byte[] 格式，与现有 embedding_documents 表一致）</summary>
    public byte[]? EmbeddingVector { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
