using System.Text;
using System.Text.RegularExpressions;
using Heimdall.Api.Models;

namespace Heimdall.Api.Services.Tasks;

internal static class WikiMarkdownNormalizer
{
    private static readonly HashSet<string> BareFenceLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "mermaid",
        "typescript",
        "ts",
        "javascript",
        "js",
        "json",
        "bash",
        "shell",
        "sh",
        "powershell",
        "ps1",
        "yaml",
        "yml",
        "python",
        "py",
        "csharp",
        "cs",
        "go",
        "java",
        "xml",
        "html",
        "css",
        "sql"
    };

    public static string Normalize(string content)
    {
        var normalized = StripThinkTags(content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim());

        normalized = UnwrapOuterMarkdownFence(normalized);
        normalized = RepairBareCodeFences(normalized);
        normalized = EscapeUppercaseAngleTags(normalized);
        return normalized.Trim();
    }

    public static Dictionary<string, WikiPage> NormalizePages(Dictionary<string, WikiPage> pages)
    {
        var normalized = new Dictionary<string, WikiPage>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, page) in pages)
        {
            normalized[key] = new WikiPage
            {
                Id = page.Id,
                Title = page.Title,
                Description = page.Description,
                Content = Normalize(page.Content),
                FilePaths = page.FilePaths,
                Importance = page.Importance,
                RelatedPages = page.RelatedPages,
                ParentId = page.ParentId,
                IsSection = page.IsSection,
                Children = page.Children
            };
        }

        return normalized;
    }

    private static string StripThinkTags(string content)
    {
        return Regex.Replace(content, "<think>[\\s\\S]*?</think>", string.Empty, RegexOptions.IgnoreCase).Trim();
    }

    private static string UnwrapOuterMarkdownFence(string content)
    {
        var match = Regex.Match(
            content,
            "^\\s*```(?:markdown|md)\\s*\\n([\\s\\S]*?)\\n```\\s*$",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : content;
    }

    private static string RepairBareCodeFences(string content)
    {
        var lines = content.Split('\n');
        var builder = new StringBuilder(content.Length + 256);
        var inFence = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmedLine = line.TrimStart();
            if (trimmedLine.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                builder.AppendLine(line);
                continue;
            }

            if (!inFence)
            {
                var language = line.Trim();
                if (BareFenceLanguages.Contains(language) &&
                    index + 1 < lines.Length &&
                    !string.IsNullOrWhiteSpace(lines[index + 1]))
                {
                    var nextLine = lines[index + 1];
                    var shouldFence = language.Equals("mermaid", StringComparison.OrdinalIgnoreCase)
                        ? IsLikelyMermaid(nextLine)
                        : IsLikelyCode(nextLine);

                    if (shouldFence)
                    {
                        builder.Append("```").Append(language).AppendLine();
                        index++;
                        for (; index < lines.Length; index++)
                        {
                            var blockLine = lines[index];
                            if (index + 1 < lines.Length && IsLikelyMarkdownTableHeader(blockLine, lines[index + 1]))
                            {
                                index--;
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(blockLine))
                            {
                                index--;
                                break;
                            }

                            if (blockLine.TrimStart().StartsWith("```", StringComparison.Ordinal))
                            {
                                index--;
                                break;
                            }

                            builder.AppendLine(blockLine);
                        }

                        builder.AppendLine("```");
                        continue;
                    }
                }
            }

            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }

    private static bool IsLikelyMarkdownTableHeader(string headerLine, string separatorLine)
    {
        if (string.IsNullOrWhiteSpace(headerLine) || string.IsNullOrWhiteSpace(separatorLine))
        {
            return false;
        }

        var header = headerLine.Trim();
        if (!header.Contains('|', StringComparison.Ordinal))
        {
            return false;
        }

        var separator = separatorLine.Trim();
        return Regex.IsMatch(separator, "^\\|?\\s*:?-{3,}:?\\s*(\\|\\s*:?-{3,}:?\\s*)+\\|?\\s*$");
    }

    private static string EscapeUppercaseAngleTags(string content)
    {
        var lines = content.Split('\n');
        var builder = new StringBuilder(content.Length + 128);
        var inFence = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();
            if (trimmedLine.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                builder.AppendLine(line);
                continue;
            }

            if (inFence)
            {
                builder.AppendLine(line);
                continue;
            }

            var escaped = Regex.Replace(line, "<(/?)([A-Z][A-Za-z0-9_]*)>", "&lt;$1$2&gt;");
            builder.AppendLine(escaped);
        }

        return builder.ToString().TrimEnd();
    }

    private static bool IsLikelyMermaid(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("graph ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("flowchart ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("classDiagram", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("erDiagram", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("journey", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("gantt", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("pie", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("mindmap", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("timeline", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyCode(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal) ||
               trimmed.StartsWith("/*", StringComparison.Ordinal) ||
               trimmed.StartsWith("*", StringComparison.Ordinal) ||
               trimmed.StartsWith("import ", StringComparison.Ordinal) ||
               trimmed.StartsWith("export ", StringComparison.Ordinal) ||
               trimmed.StartsWith("const ", StringComparison.Ordinal) ||
               trimmed.StartsWith("let ", StringComparison.Ordinal) ||
               trimmed.StartsWith("var ", StringComparison.Ordinal) ||
               trimmed.StartsWith("interface ", StringComparison.Ordinal) ||
               trimmed.StartsWith("class ", StringComparison.Ordinal) ||
               trimmed.StartsWith("function ", StringComparison.Ordinal) ||
               trimmed.StartsWith("async ", StringComparison.Ordinal) ||
               trimmed.StartsWith("{", StringComparison.Ordinal) ||
               trimmed.StartsWith("[", StringComparison.Ordinal) ||
               trimmed.StartsWith("<", StringComparison.Ordinal) ||
               trimmed.StartsWith("#!", StringComparison.Ordinal) ||
               trimmed.StartsWith("$", StringComparison.Ordinal);
    }
}
