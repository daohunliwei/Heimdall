namespace Heimdall.Infrastructure.Utilities;

/// <summary>
/// Token 计数器——使用基于字符和 Unicode 的启发式估算 Token 数量。
/// 对于中文内容平均 1.5 字符 ≈ 1 Token，英文 4 字符 ≈ 1 Token。
/// </summary>
public static class TokenCounter
{
    /// <summary>
    /// 估算给定文本的 Token 数量。
    /// </summary>
    public static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int cjkChars = 0;
        int otherChars = 0;

        foreach (var ch in text)
        {
            if (IsCjkCharacter(ch))
                cjkChars++;
            else
                otherChars++;
        }

        // CJK: ~1.5 字符/Token; 拉丁: ~4 字符/Token
        var cjkTokens = (int)Math.Ceiling(cjkChars / 1.5);
        var otherTokens = (int)Math.Ceiling(otherChars / 4.0);

        return cjkTokens + otherTokens;
    }

    /// <summary>
    /// 估算多段文本的总 Token 数量。
    /// </summary>
    public static int EstimateTokenCount(IEnumerable<string> texts)
    {
        return texts.Sum(EstimateTokenCount);
    }

    /// <summary>
    /// 检查给定文本是否超过指定 Token 上限。
    /// </summary>
    public static bool ExceedsTokenLimit(string? text, int maxTokens)
    {
        return EstimateTokenCount(text) > maxTokens;
    }

    /// <summary>
    /// 将文本截断到指定 Token 上限（保留前部分）。
    /// </summary>
    public static string TruncateToTokenLimit(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var estimated = EstimateTokenCount(text);
        if (estimated <= maxTokens) return text;

        // 二分查找截断点
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (EstimateTokenCount(text[..mid]) <= maxTokens)
                lo = mid;
            else
                hi = mid - 1;
        }

        return text[..lo];
    }

    /// <summary>
    /// 判断字符是否为 CJK 字符。
    /// </summary>
    private static bool IsCjkCharacter(char ch)
    {
        return ch >= 0x4E00 && ch <= 0x9FFF    // CJK 统一汉字
            || ch >= 0x3400 && ch <= 0x4DBF    // CJK 扩展 A
            || ch >= 0x3000 && ch <= 0x303F    // CJK 标点
            || ch >= 0xFF00 && ch <= 0xFFEF    // 全角符号
            || ch >= 0x3040 && ch <= 0x309F    // 平假名
            || ch >= 0x30A0 && ch <= 0x30FF;   // 片假名
    }
}
