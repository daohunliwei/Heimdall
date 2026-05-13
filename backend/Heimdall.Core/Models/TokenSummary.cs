namespace Heimdall.Core.Models;

public class TokenSummary
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int CallCount { get; set; }
    public decimal TotalCost { get; set; }
}
