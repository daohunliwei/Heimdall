namespace Heimdall.Core.Entities;

public class WikiPage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid WikiId { get; set; }
    public Wiki Wiki { get; set; } = null!;
    public Guid? TaskId { get; set; }
    public TaskRecord? Task { get; set; }
    public int PageOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContentMarkdown { get; set; }
    public Guid? ParentPageId { get; set; }
    public WikiPage? ParentPage { get; set; }
    public string Importance { get; set; } = "medium";
    public string[]? FilePaths { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<WikiPage> Children { get; set; } = new List<WikiPage>();
}
