using System.Text;
using System.Text.RegularExpressions;
using Heimdall.Core.Models;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Wiki 渲染后处理服务。
/// 该服务负责把页面草案转换为前端稳定消费的 Markdown 工件，
/// 包括 Frontmatter 生成、源码说明块注入、目录提纲提取与源码覆盖补齐。
/// </summary>
public sealed class WikiRenderPostProcessor
{
    /// <summary>
    /// 执行渲染后处理。
    /// </summary>
    public WikiRenderResultDto PostProcess(WikiStructureDto structure)
    {
        var renderedPageCount = 0;
        var frontMatterPageCount = 0;
        var outlineHeadingCount = 0;

        foreach (var page in structure.Pages)
        {
            page.FrontMatter ??= new();
            page.SourceCoverage ??= new();
            page.SourceCoverage.PrimaryFiles = MergeDistinct(page.FilePaths, page.SourceCoverage.PrimaryFiles);
            page.SourceCoverage.Evidence ??= new();
            page.SourceCoverage.Evidence = NormalizeEvidence(page.SourceCoverage.PrimaryFiles, page.SourceCoverage.Evidence);
            page.FrontMatter.SourceFiles = MergeDistinct(page.SourceCoverage.PrimaryFiles, page.FrontMatter.SourceFiles);
            page.FrontMatter.Description = string.IsNullOrWhiteSpace(page.FrontMatter.Description)
                ? page.Description
                : page.FrontMatter.Description.Trim();
            page.FrontMatter.Summary = string.IsNullOrWhiteSpace(page.FrontMatter.Summary)
                ? page.Description
                : page.FrontMatter.Summary.Trim();
            page.FrontMatter.Tags = NormalizeDistinct(page.FrontMatter.Tags);

            var normalizedBody = NormalizeMarkdownBody(page);
            page.Outline = ExtractOutline(page.Title, normalizedBody);
            outlineHeadingCount += page.Outline.Count;

            page.Content = BuildFinalMarkdown(page, normalizedBody);
            page.Description = string.IsNullOrWhiteSpace(page.FrontMatter.Summary)
                ? page.Description
                : page.FrontMatter.Summary;

            renderedPageCount++;
            frontMatterPageCount++;
        }

        return new WikiRenderResultDto
        {
            Structure = structure,
            RenderedPageCount = renderedPageCount,
            FrontMatterPageCount = frontMatterPageCount,
            OutlineHeadingCount = outlineHeadingCount
        };
    }

    /// <summary>
    /// 规范化页面 Markdown 正文。
    /// </summary>
    private static string NormalizeMarkdownBody(WikiPageDto page)
    {
        var normalized = WikiMarkdownNormalizer.Normalize(page.Content);
        normalized = Regex.Replace(normalized, "^---[\\s\\S]*?---\\s*", string.Empty, RegexOptions.Multiline);
        normalized = Regex.Replace(normalized, @"^\s*<details>[\s\S]*?</details>\s*", string.Empty, RegexOptions.IgnoreCase);
        normalized = normalized.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = $"## 页面概览\n\n{page.Description}";
        }

        normalized = Regex.Replace(normalized, $@"^\s*#\s+{Regex.Escape(page.Title)}\s*", string.Empty, RegexOptions.IgnoreCase);
        return normalized.Trim();
    }

    /// <summary>
    /// 构建最终 Markdown 文本。
    /// </summary>
    private static string BuildFinalMarkdown(WikiPageDto page, string markdownBody)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"title: {YamlEscape(page.Title)}");
        builder.AppendLine($"nav_title: {YamlEscape(string.IsNullOrWhiteSpace(page.NavTitle) ? page.Title : page.NavTitle)}");
        builder.AppendLine($"page_type: {YamlEscape(page.PageType)}");
        builder.AppendLine($"importance: {YamlEscape(page.Importance)}");
        builder.AppendLine($"summary: {YamlEscape(page.FrontMatter.Summary)}");
        AppendYamlArray(builder, "tags", page.FrontMatter.Tags);
        AppendYamlArray(builder, "source_files", page.FrontMatter.SourceFiles);
        AppendYamlArray(builder, "related_pages", page.RelatedPages);
        AppendYamlArray(builder, "prerequisite_pages", page.PrerequisitePages);
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("<details>");
        builder.AppendLine("<summary>Relevant source files</summary>");
        builder.AppendLine();
        builder.AppendLine("以下源文件参与了该页面草案与后处理生成：");
        builder.AppendLine();

        foreach (var filePath in page.FrontMatter.SourceFiles)
        {
            builder.AppendLine($"- {filePath}");
        }

        builder.AppendLine("</details>");
        builder.AppendLine();
        builder.AppendLine($"# {page.Title}");
        builder.AppendLine();
        builder.AppendLine(markdownBody.Trim());
        return builder.ToString().Trim();
    }

    /// <summary>
    /// 生成页面目录提纲。
    /// </summary>
    private static List<WikiPageHeadingDto> ExtractOutline(string pageTitle, string markdownBody)
    {
        var result = new List<WikiPageHeadingDto>
        {
            new()
            {
                Level = 1,
                Title = pageTitle,
                Anchor = BuildAnchor(pageTitle)
            }
        };

        foreach (Match match in Regex.Matches(markdownBody, @"^(#{2,6})\s+(.+?)\s*$", RegexOptions.Multiline))
        {
            var title = match.Groups[2].Value.Trim();
            result.Add(new WikiPageHeadingDto
            {
                Level = match.Groups[1].Value.Length,
                Title = title,
                Anchor = BuildAnchor(title)
            });
        }

        return result;
    }

    /// <summary>
    /// 规范化源码证据集合。
    /// </summary>
    private static List<WikiPageSourceEvidenceDto> NormalizeEvidence(
        IEnumerable<string> primaryFiles,
        IEnumerable<WikiPageSourceEvidenceDto> evidenceItems)
    {
        var evidenceLookup = evidenceItems
            .Where(item => !string.IsNullOrWhiteSpace(item.FilePath))
            .GroupBy(item => item.FilePath.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in primaryFiles)
        {
            if (!evidenceLookup.ContainsKey(filePath))
            {
                evidenceLookup[filePath] = new WikiPageSourceEvidenceDto
                {
                    FilePath = filePath,
                    Reason = "该文件被识别为当前页面的核心实现来源。",
                    Symbols = new()
                };
            }
        }

        return evidenceLookup.Values
            .Select(item => new WikiPageSourceEvidenceDto
            {
                FilePath = item.FilePath.Trim(),
                Reason = string.IsNullOrWhiteSpace(item.Reason) ? "该文件与当前页面内容直接相关。" : item.Reason.Trim(),
                Symbols = NormalizeDistinct(item.Symbols)
            })
            .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 生成 Markdown/YAML 锚点。
    /// </summary>
    private static string BuildAnchor(string value)
    {
        var anchor = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^\p{L}\p{N}\- ]+", string.Empty);
        anchor = Regex.Replace(anchor, @"\s+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(anchor) ? "section" : anchor;
    }

    /// <summary>
    /// 向 YAML 文本中追加数组字段。
    /// </summary>
    private static void AppendYamlArray(StringBuilder builder, string key, IEnumerable<string> values)
    {
        var normalized = NormalizeDistinct(values);
        if (normalized.Count == 0)
        {
            builder.AppendLine($"{key}: []");
            return;
        }

        builder.AppendLine($"{key}:");
        foreach (var value in normalized)
        {
            builder.AppendLine($"  - {YamlEscape(value)}");
        }
    }

    /// <summary>
    /// 转义 YAML 标量。
    /// </summary>
    private static string YamlEscape(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return $"\"{normalized.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    /// <summary>
    /// 合并两个字符串列表并去重。
    /// </summary>
    private static List<string> MergeDistinct(IEnumerable<string>? primary, IEnumerable<string>? secondary)
    {
        return NormalizeDistinct((primary ?? Enumerable.Empty<string>()).Concat(secondary ?? Enumerable.Empty<string>()));
    }

    /// <summary>
    /// 对字符串列表执行去重与空值过滤。
    /// </summary>
    private static List<string> NormalizeDistinct(IEnumerable<string>? values)
    {
        return values?
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList() ?? new();
    }
}
