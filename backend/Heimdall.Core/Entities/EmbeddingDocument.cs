namespace Heimdall.Core.Entities;

public class EmbeddingDocument
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public byte[]? Embedding { get; set; }
    public int? TokenCount { get; set; }
    public bool IsCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
