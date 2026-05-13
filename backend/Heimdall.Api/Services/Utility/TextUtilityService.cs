using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Heimdall.Api.Services.Utility;

/// <summary>
/// 文本与路径辅助服务。
/// </summary>
public sealed class TextUtilityService
{
    /// <summary>
    /// 从仓库地址中提取仓库名。
    /// </summary>
    public string ExtractRepositoryName(string? repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            return "heimdall-project";
        }

        if (Directory.Exists(repoUrl))
        {
            return new DirectoryInfo(repoUrl).Name;
        }

        var parts = repoUrl.TrimEnd('/').Split('/');
        return parts.Length == 0 ? "heimdall-project" : parts[^1].Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 估算文本 token 数量。
    /// </summary>
    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Math.Max(1, text.Length / 4);
    }

    /// <summary>
    /// 对文本进行 XML 转义。
    /// </summary>
    public string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    /// <summary>
    /// 对文本进行 HTML 转义。
    /// </summary>
    public string EscapeHtml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    /// <summary>
    /// 生成稳定的 SHA256 字符串。
    /// </summary>
    public string ToSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 将换行分隔的字符串拆分为列表。
    /// </summary>
    public List<string> ParseMultiLineList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    /// <summary>
    /// 按单词切分文本。
    /// </summary>
    public List<string> SplitByWords(string text, int chunkSize, int overlap)
    {
        var words = Regex.Split(text, @"\s+").Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        if (words.Count == 0)
        {
            return new List<string>();
        }

        var chunks = new List<string>();
        var step = Math.Max(1, chunkSize - overlap);
        for (var index = 0; index < words.Count; index += step)
        {
            var chunkWords = words.Skip(index).Take(chunkSize).ToArray();
            if (chunkWords.Length == 0)
            {
                continue;
            }

            chunks.Add(string.Join(' ', chunkWords));
            if (index + chunkSize >= words.Count)
            {
                break;
            }
        }

        return chunks;
    }

    public List<string> SplitByCharacters(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        if (chunkSize <= 0)
        {
            return new List<string> { text };
        }

        var safeOverlap = Math.Clamp(overlap, 0, Math.Max(0, chunkSize - 1));
        var step = Math.Max(1, chunkSize - safeOverlap);
        var chunks = new List<string>();

        for (var index = 0; index < text.Length; index += step)
        {
            var size = Math.Min(chunkSize, text.Length - index);
            if (size <= 0)
            {
                break;
            }

            chunks.Add(text.Substring(index, size));
            if (index + size >= text.Length)
            {
                break;
            }
        }

        return chunks;
    }

    public List<string> SplitByLines(string text, int chunkSize, int overlap)
    {
        var lines = text
            .Split('\n', StringSplitOptions.None)
            .Select(line => line.TrimEnd('\r'))
            .ToList();
        if (lines.Count == 0)
        {
            return new List<string>();
        }

        if (chunkSize <= 0)
        {
            return new List<string> { text };
        }

        var step = Math.Max(1, chunkSize - Math.Max(0, overlap));
        var chunks = new List<string>();
        for (var index = 0; index < lines.Count; index += step)
        {
            var segment = lines.Skip(index).Take(chunkSize).ToArray();
            if (segment.Length == 0)
            {
                continue;
            }

            chunks.Add(string.Join('\n', segment));
            if (index + chunkSize >= lines.Count)
            {
                break;
            }
        }

        return chunks;
    }

    /// <summary>
    /// 将完整文本拆分为固定大小的 SSE 片段。
    /// </summary>
    public IEnumerable<string> SplitIntoSseChunks(string content, int chunkSize)
    {
        for (var index = 0; index < content.Length; index += chunkSize)
        {
            yield return content.Substring(index, Math.Min(chunkSize, content.Length - index));
        }
    }
}
