namespace Heimdall.Core.Entities;

public class Wiki
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid SourceRepositoryId { get; set; }
    public Repository SourceRepository { get; set; } = null!;
    public string SourceBranch { get; set; } = "main";
    public string Language { get; set; } = "zh";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<WikiPage> Pages { get; set; } = new List<WikiPage>();
}
