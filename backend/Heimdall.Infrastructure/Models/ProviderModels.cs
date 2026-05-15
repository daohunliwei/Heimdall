using System.Text.Json;

namespace Heimdall.Infrastructure.Models;

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatCompletionRequest
{
    public string RepoUrl { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = new();
    public string? FilePath { get; set; }
    public string? Token { get; set; }
    public string? Type { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? CustomModel { get; set; }
    public string? Language { get; set; }
    public string? ExcludedDirs { get; set; }
    public string? ExcludedFiles { get; set; }
    public string? IncludedDirs { get; set; }
    public string? IncludedFiles { get; set; }
}

public class ProviderChatRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public Dictionary<string, JsonElement>? Options { get; set; }
}
