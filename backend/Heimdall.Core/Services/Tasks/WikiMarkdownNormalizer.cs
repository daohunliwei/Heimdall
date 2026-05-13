using System.Text.RegularExpressions;

namespace Heimdall.Core.Services.Tasks;

public static class WikiMarkdownNormalizer
{
    public static string Normalize(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        // 去除 think 标签
        var result = Regex.Replace(markdown, "<think>[\\s\\S]*?</think>", "", RegexOptions.IgnoreCase);
        result = result.Replace("```xml", "").Replace("```", "").Trim();
        return result;
    }

    public static Dictionary<string, string> NormalizePages(Dictionary<string, string> pages)
    {
        return pages.ToDictionary(
            kv => kv.Key,
            kv => Normalize(kv.Value),
            StringComparer.OrdinalIgnoreCase);
    }
}
