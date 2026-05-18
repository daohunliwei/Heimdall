using System.Text.RegularExpressions;

namespace Heimdall.Core.Services.Tasks;

public static class WikiMarkdownNormalizer
{
    public static string Normalize(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        // 去除 think 标签
        var result = Regex.Replace(markdown, "<think>[\\s\\S]*?</think>", "", RegexOptions.IgnoreCase);
        // 移除 Markdown 代码围栏（```json, ```xml, ``` 等），但不移除 JSON 内部的 ```
        result = Regex.Replace(result, @"^```[\w-]*\s*", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"\s*```\s*$", "", RegexOptions.Multiline);
        result = result.Trim();
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
