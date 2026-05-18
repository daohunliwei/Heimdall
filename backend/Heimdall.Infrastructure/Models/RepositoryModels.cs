namespace Heimdall.Infrastructure.Models;

public class LocalRepoStructureResponse
{
    public string FileTree { get; set; } = string.Empty;
    public string Readme { get; set; } = string.Empty;
}

public class RepoInfo
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string Type { get; set; } = "github";
    public string? RepoUrl { get; set; }
    public string? Token { get; set; }
    public string? LocalPath { get; set; }
}

public class EmbeddedDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public bool IsCode { get; set; }
    public bool IsImplementation { get; set; }
    public int TokenCount { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}
