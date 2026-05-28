using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Wiki 生成解析服务。
/// 该服务负责把模型输出解析为结构规划 DTO 与页面草案 DTO，
/// 并在 JSON 严格结构化输出失败时兼容旧版 XML 结果，保证 V3 阶段 2 可以平滑灰度。
/// </summary>
public sealed class WikiGenerationParserService
{
    /// <summary>
    /// 结构化 JSON 反序列化配置。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly ILogger<WikiGenerationParserService> _logger;

    /// <summary>
    /// 初始化 Wiki 生成解析服务。
    /// </summary>
    public WikiGenerationParserService(ILogger<WikiGenerationParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析结构规划结果。
    /// 优先尝试 JSON DTO，失败后回退到旧版 XML 兼容逻辑。
    /// </summary>
    public WikiStructureDto ParseStructure(string response, bool comprehensive)
    {
        if (TryParseStructureJson(response, out var structure))
        {
            return NormalizeStructure(structure!, comprehensive);
        }

        _logger.LogWarning("结构规划 JSON 解析失败，回退到 XML 兼容解析");
        return ParseStructureFromXml(response, comprehensive);
    }

    /// <summary>
    /// 解析页面草案结果。
    /// 优先尝试严格结构化 JSON 页面 DTO，失败时回退为 Markdown 兜底草案。
    /// </summary>
    public WikiPageDto ParsePageDraft(WikiPageDto requestedPage, string response)
    {
        // 优先尝试 YAML frontmatter 格式
        if (TryParsePageDraftFrontmatter(response, out var fmDraft))
        {
            return NormalizePageDraft(requestedPage, fmDraft!);
        }

        // 回退到 JSON 格式
        if (TryParsePageDraftJson(response, out var draft))
        {
            return NormalizePageDraft(requestedPage, draft!);
        }

        _logger.LogWarning("页面草案解析失败（frontmatter/JSON 均不匹配），使用 Markdown 兜底草案 PageId={PageId}", requestedPage.Id);
        return BuildFallbackPageDraft(requestedPage, response);
    }

    /// <summary>
    /// 将结构 DTO 序列化为稳定 JSON。
    /// </summary>
    public string SerializeStructure(WikiStructureDto structure)
    {
        return JsonSerializer.Serialize(NormalizeStructure(structure, comprehensive: true), JsonOptions);
    }

    /// <summary>
    /// 尝试将模型输出解析为结构规划 JSON。
    /// </summary>
    private bool TryParseStructureJson(string response, out WikiStructureDto? structure)
    {
        structure = null;
        var jsonBlock = TryExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(jsonBlock))
        {
            return false;
        }

        try
        {
            structure = JsonSerializer.Deserialize<WikiStructureDto>(jsonBlock, JsonOptions);
            return structure is not null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "结构规划 JSON 反序列化失败，回退到 XML/正则解析");
            return false;
        }
    }

    /// <summary>
    /// 尝试将模型输出解析为页面草案 JSON。
    /// </summary>
    private bool TryParsePageDraftJson(string response, out WikiPageDto? page)
    {
        page = null;
        var jsonBlock = TryExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(jsonBlock))
        {
            return false;
        }

        try
        {
            page = JsonSerializer.Deserialize<WikiPageDto>(jsonBlock, JsonOptions);
            return page is not null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "页面草案 JSON 反序列化失败，回退到 Markdown 兜底解析 ResponseLen={ResponseLen}", response.Length);
            return false;
        }
    }

    /// <summary>
    /// 尝试将模型输出解析为 YAML frontmatter + Markdown 格式。
    /// 格式：---\nkey: value\n---\nMarkdown正文
    /// </summary>
    private bool TryParsePageDraftFrontmatter(string response, out WikiPageDto? page)
    {
        page = null;
        // 剥离 MiniMax 的 <think>...</think> 思考块
        var cleaned = Regex.Replace(response, @"<think>[\s\S]*?</think>\s*", "");
        var trimmed = cleaned.TrimStart();
        if (!trimmed.StartsWith("---")) return false;

        // 找到第二个 ---（frontmatter 结束标记）
        var endIdx = trimmed.IndexOf("\n---", 3);
        if (endIdx < 0) endIdx = trimmed.IndexOf("---\n", 3);
        if (endIdx < 0) return false;

        var yamlBlock = trimmed[3..endIdx].Trim();
        var markdownContent = trimmed[(endIdx + 4)..].Trim(); // 跳过 \n---

        try
        {
            page = new WikiPageDto();
            var lines = yamlBlock.Split('\n');
            string? currentListKey = null;
            var listValues = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                // YAML 列表项: "  - value"
                if (line.TrimStart().StartsWith("- ") && currentListKey != null)
                {
                    listValues.Add(line.TrimStart()[2..].Trim().Trim('"'));
                    continue;
                }

                // 保存之前的列表
                if (currentListKey != null && listValues.Count > 0)
                {
                    ApplyFrontmatterList(page, currentListKey, listValues);
                    listValues.Clear();
                    currentListKey = null;
                }

                // YAML 键值对: "key: value"
                var colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;

                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..].Trim().Trim('"');

                switch (key.ToLowerInvariant())
                {
                    case "id": page.Id = value; break;
                    case "title": page.Title = value; break;
                    case "navtitle": page.NavTitle = value; break;
                    case "pagetype": page.PageType = value; break;
                    case "importance": page.Importance = value; break;
                    case "description": page.Description = value; break;
                    case "parentid": page.ParentId = value; break;
                    case "depth": if (int.TryParse(value, out var d)) page.Depth = d; break;
                    case "contentdepthlevel": page.ContentDepthLevel = value; break;
                    case "summary":
                        if (page.FrontMatter is null) page.FrontMatter = new();
                        page.FrontMatter.Summary = value;
                        break;
                    case "sourcefiles":
                    case "tags":
                    case "relatedpages":
                    case "prerequisitepages":
                    case "searchkeywords":
                    case "filepaths":
                        currentListKey = key;
                        break;
                    // 跳过未知键
                }
            }

            // 保存最后的列表
            if (currentListKey != null && listValues.Count > 0)
            {
                ApplyFrontmatterList(page, currentListKey, listValues);
            }

            if (string.IsNullOrWhiteSpace(page.Title) && !string.IsNullOrWhiteSpace(markdownContent))
            {
                // 从 Markdown 的 # 标题提取标题
                var h1Match = Regex.Match(markdownContent, @"^#\s+(.+)$", RegexOptions.Multiline);
                if (h1Match.Success) page.Title = h1Match.Groups[1].Value.Trim();
            }

            page.Content = markdownContent;
            return !string.IsNullOrWhiteSpace(page.Content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Frontmatter 解析异常");
            return false;
        }
    }

    private static void ApplyFrontmatterList(WikiPageDto page, string key, List<string> values)
    {
        var arr = values.ToArray();
        switch (key.ToLowerInvariant())
        {
            case "sourcefiles":
            case "filepaths":
                page.FilePaths = values;
                if (page.FrontMatter is null) page.FrontMatter = new();
                page.FrontMatter.SourceFiles = values;
                break;
            case "tags":
                if (page.FrontMatter is null) page.FrontMatter = new();
                page.FrontMatter.Tags = values;
                break;
            case "relatedpages":
                page.RelatedPages = values;
                break;
            case "prerequisitepages":
                page.PrerequisitePages = values;
                break;
            case "searchkeywords":
                page.SearchKeywords = values;
                break;
        }
    }

    /// <summary>
    /// 提取模型输出中的 JSON 块。
    /// 支持 ```json 代码块与纯文本混排两类场景。
    /// </summary>
    private static string? TryExtractJsonBlock(string response)
    {
        var normalized = WikiMarkdownNormalizer.Normalize(response);

        // 推理模型（如 MiniMax M2.7）可能在推理文本中包含代码块，取最后一个 ```json 块
        var fencedMatches = Regex.Matches(normalized, "```json\\s*(?<json>[\\s\\S]*?)```", RegexOptions.IgnoreCase);
        if (fencedMatches.Count > 0)
        {
            var lastMatch = fencedMatches[fencedMatches.Count - 1];
            return TryRepairJson(lastMatch.Groups["json"].Value.Trim());
        }

        // 尝试 ``` 围栏代码块（无语言标记）
        var anyFence = Regex.Matches(normalized, "```\\s*(?<json>[\\s\\S]*?)```", RegexOptions.IgnoreCase);
        foreach (Match m in anyFence)
        {
            var content = m.Groups["json"].Value.Trim();
            if (content.StartsWith('{') || content.StartsWith('['))
            {
                var result = TryRepairJson(content);
                if (result is not null) return result;
            }
        }

        // 移除残留的 "json" 前缀
        if (normalized.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = normalized[4..].TrimStart();
            if (trimmed.StartsWith('{'))
                normalized = trimmed;
        }

        var firstBrace = normalized.IndexOf('{');
        var lastBrace = normalized.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace)
        {
            return null;
        }

        return TryRepairJson(normalized[firstBrace..(lastBrace + 1)].Trim());
    }

    /// <summary>
    /// 尝试修复常见的小模型 JSON 语法错误（如 bracket 类型错误）。
    /// </summary>
    private static string? TryRepairJson(string json)
    {
        // 先尝试直接解析
        try
        {
            using var doc = JsonDocument.Parse(json);
            return json;
        }
        catch { }

        // 修复 1: 闭合 bracket 类型错误（} 应为 ]）
        var repaired = FixBracketTypeErrors(json);
        if (repaired is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(repaired);
                return repaired;
            }
            catch { }
        }

        // 修复 2: 截断结尾（移除末尾不完整内容）
        repaired = FixTruncatedEnd(json);
        if (repaired is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(repaired);
                return repaired;
            }
            catch { }
        }

        // 修复 3: 组合修复——先截断再修正 bracket 错误
        repaired = FixTruncatedEnd(json);
        if (repaired is not null)
        {
            var combined = FixBracketTypeErrors(repaired);
            if (combined is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(combined);
                    return combined;
                }
                catch { }
            }
        }

        // 修复 4: 激进截断——从后向前逐步移除字符直到找到可解析的 JSON
        repaired = AggressiveTruncateRepair(json);
        if (repaired is not null)
        {
            return repaired;
        }

        return null;
    }

    /// <summary>
    /// 激进截断修复：从最后一个 "}" 向前查找，尝试在每个 "}" 位置补全括号使 JSON 可解析。
    /// </summary>
    private static string? AggressiveTruncateRepair(string json)
    {
        // 找到所有可能的截断点（"}" 位置），从后向前尝试
        var candidates = new List<int>();
        var inString = false;
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }
            if (!inString && c == '}')
            {
                candidates.Add(i);
            }
        }

        // 从后向前，尝试在每个 } 之后截断并补全
        for (var ci = candidates.Count - 1; ci >= 0; ci--)
        {
            var cutPoint = candidates[ci] + 1;
            var truncated = json[..cutPoint];

            // 计算未闭合括号
            var stack = new Stack<char>();
            inString = false;
            for (var i = 0; i < truncated.Length; i++)
            {
                var ch = truncated[i];
                if (ch == '"' && (i == 0 || truncated[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;
                switch (ch)
                {
                    case '{': stack.Push('}'); break;
                    case '[': stack.Push(']'); break;
                    case '}' or ']' when stack.Count > 0: stack.Pop(); break;
                }
            }

            if (stack.Count > 0 && stack.Count <= 3) // 只差少量括号
            {
                var sb = new StringBuilder(truncated);
                while (stack.Count > 0) sb.Append(stack.Pop());
                var attempt = sb.ToString();
                try
                {
                    using var doc = JsonDocument.Parse(attempt);
                    // 验证至少有 pages 属性
                    if (doc.RootElement.TryGetProperty("pages", out _))
                        return attempt;
                }
                catch { }
            }
            else if (stack.Count == 0)
            {
                try
                {
                    using var doc = JsonDocument.Parse(truncated);
                    if (doc.RootElement.TryGetProperty("pages", out _))
                        return truncated;
                }
                catch { }
            }
        }

        return null;
    }

    /// <summary>
    /// 修复数组闭合使用了 } 而非 ] 的错误。
    /// 检测嵌套的数组/对象闭合错误，将错误的 } 替换为 ]。
    /// </summary>
    private static string? FixBracketTypeErrors(string json)
    {
        var chars = json.ToCharArray();
        var stack = new Stack<(int pos, char expected)>();
        var fixedCount = 0;

        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            // 跳过字符串内容
            if (c == '"' && (i == 0 || chars[i - 1] != '\\'))
            {
                // 简单跳过字符串（不处理转义引号的复杂情况）
                continue;
            }

            switch (c)
            {
                case '{':
                    stack.Push((i, '}'));
                    break;
                case '[':
                    stack.Push((i, ']'));
                    break;
                case '}' when stack.Count > 0:
                    var (pos, expected) = stack.Pop();
                    if (expected == ']')
                    {
                        // 错误: 数组用了 } 闭合，应改为 ]
                        chars[i] = ']';
                        fixedCount++;
                    }
                    break;
                case ']' when stack.Count > 0:
                    var (pos2, expected2) = stack.Pop();
                    if (expected2 == '}')
                    {
                        // 错误: 对象用了 ] 闭合，应改为 }
                        chars[i] = '}';
                        fixedCount++;
                    }
                    break;
                case '}' or ']':
                    // 没有对应开括号，忽略
                    break;
            }
        }

        return fixedCount > 0 ? new string(chars) : null;
    }

    /// <summary>
    /// 修复被截断的 JSON 结尾。
    /// 策略：找到最后一个完整的对象/数组边界，截断到该位置后补全所有未闭合的括号。
    /// </summary>
    private static string? FixTruncatedEnd(string json)
    {
        var trimmed = json.TrimEnd();

        // 策略 1：尝试回溯到最后一个完整的顶层数组元素
        // 在 "pages" 数组中，找到最后一个完整的 "}," 或 "}" 边界
        var repaired = TryTruncateToLastCompleteObject(trimmed);
        if (repaired is not null) return repaired;

        // 策略 2：移除尾部逗号后补全
        if (trimmed.EndsWith(','))
        {
            trimmed = trimmed.TrimEnd(',');
        }

        // 策略 3：计算未闭合的括号并补全
        var openArrays = 0;
        var openObjects = 0;
        var inString = false;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '"' && (i == 0 || trimmed[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }
            if (inString) continue;

            switch (c)
            {
                case '[': openArrays++; break;
                case ']': openArrays--; break;
                case '{': openObjects++; break;
                case '}': openObjects--; break;
            }
        }

        if (openArrays > 0 || openObjects > 0)
        {
            // 按照嵌套顺序补全：先闭合对象，再闭合数组，最后闭合根对象
            var suffix = new string('}', Math.Max(0, openObjects > openArrays ? openObjects - openArrays : 0))
                + new string(']', Math.Max(0, openArrays))
                + new string('}', Math.Max(0, openObjects > openArrays ? openArrays : openObjects));

            // 更精确：直接按 open 数逐个闭合
            var sb = new StringBuilder(trimmed);
            // 需要 openObjects 个 } 和 openArrays 个 ]，顺序从内到外
            // 简化：先补对象再补数组再补剩余对象
            for (var i = 0; i < openObjects; i++) sb.Append('}');
            for (var i = 0; i < openArrays; i++) sb.Append(']');
            // 上面的简化可能不对，使用扫描法重新计算
            sb.Clear();
            sb.Append(trimmed);
            // 重新用栈法精确补全
            var stack = new Stack<char>();
            inString = false;
            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                if (c == '"' && (i == 0 || trimmed[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;
                switch (c)
                {
                    case '{': stack.Push('}'); break;
                    case '[': stack.Push(']'); break;
                    case '}' or ']' when stack.Count > 0: stack.Pop(); break;
                }
            }
            // 如果在字符串中截断，先加引号闭合
            if (inString) sb.Append('"');
            while (stack.Count > 0) sb.Append(stack.Pop());
            return sb.ToString();
        }

        return null;
    }

    /// <summary>
    /// 尝试回溯到 JSON 中最后一个完整的对象边界，截断不完整部分后补全括号。
    /// 这对于 "pages" 数组被截断的情况特别有效。
    /// </summary>
    private static string? TryTruncateToLastCompleteObject(string json)
    {
        // 从后往前找最后一个 "}," 或 "}" 后跟 "]" 的位置
        // 这表示数组中最后一个完整对象的结束
        var lastCompleteEnd = -1;
        var depth = 0;
        var inString = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }
            if (inString) continue;

            switch (c)
            {
                case '{' or '[': depth++; break;
                case '}' or ']': depth--; break;
            }

            // 当深度回到 2 且当前字符是 }，说明完成了一个 pages 数组内的对象
            if (c == '}' && depth == 2)
            {
                lastCompleteEnd = i;
            }
        }

        if (lastCompleteEnd <= 0 || lastCompleteEnd >= json.Length - 1)
            return null;

        // 检查是否确实被截断（最后部分不完整）
        var remainder = json[(lastCompleteEnd + 1)..].TrimStart();
        if (remainder.StartsWith(','))
        {
            // 后面还有不完整的对象
            var truncated = json[..(lastCompleteEnd + 1)];
            // 用栈法补全
            var stack = new Stack<char>();
            inString = false;
            for (var i = 0; i < truncated.Length; i++)
            {
                var ch = truncated[i];
                if (ch == '"' && (i == 0 || truncated[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;
                switch (ch)
                {
                    case '{': stack.Push('}'); break;
                    case '[': stack.Push(']'); break;
                    case '}' or ']' when stack.Count > 0: stack.Pop(); break;
                }
            }
            if (stack.Count > 0)
            {
                var sb = new StringBuilder(truncated);
                while (stack.Count > 0) sb.Append(stack.Pop());
                return sb.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// 规范化结构 DTO，确保页面标识、目录分组与引用关系满足持久化要求。
    /// </summary>
    private WikiStructureDto NormalizeStructure(WikiStructureDto structure, bool comprehensive)
    {
        structure.Id = string.IsNullOrWhiteSpace(structure.Id) ? "wiki" : structure.Id.Trim();
        structure.Title = string.IsNullOrWhiteSpace(structure.Title) ? "Repository Wiki" : structure.Title.Trim();
        structure.Description = structure.Description?.Trim() ?? string.Empty;
        structure.Pages ??= new();
        structure.Sections ??= new();
        structure.RootSections ??= new();

        var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < structure.Pages.Count; index++)
        {
            var page = structure.Pages[index] ?? new WikiPageDto();
            page.Id = NormalizeIdentifier(page.Id, "page", index + 1, existingIds);
            page.Title = string.IsNullOrWhiteSpace(page.Title) ? $"页面 {index + 1}" : page.Title.Trim();
            page.NavTitle = string.IsNullOrWhiteSpace(page.NavTitle) ? page.Title : page.NavTitle.Trim();
            page.Description = page.Description?.Trim() ?? string.Empty;
            page.PageType = NormalizePageType(page.PageType, page.IsSection);
            page.Importance = NormalizeImportance(page.Importance);
            page.FilePaths = NormalizeDistinctList(page.FilePaths);
            page.RelatedPages = NormalizeDistinctList(page.RelatedPages);
            page.PrerequisitePages = NormalizeDistinctList(page.PrerequisitePages);
            page.Children = page.Children is null ? null : NormalizeDistinctList(page.Children);
            page.ParentId = NormalizeOptionalValue(page.ParentId);
            page.FrontMatter ??= new();
            page.Outline ??= new();
            page.SourceCoverage ??= new();
            page.SourceCoverage.PrimaryFiles = NormalizeDistinctList(page.SourceCoverage.PrimaryFiles);
            page.SourceCoverage.Evidence ??= new();
            foreach (var evidence in page.SourceCoverage.Evidence)
            {
                evidence.FilePath = NormalizeOptionalValue(evidence.FilePath) ?? string.Empty;
                evidence.Reason = evidence.Reason?.Trim() ?? string.Empty;
                evidence.Symbols = NormalizeDistinctList(evidence.Symbols);
            }

            structure.Pages[index] = page;
        }

        if (structure.Pages.Count == 0)
        {
            return BuildFallbackStructure("未解析到有效页面结构");
        }

        var pageIdSet = structure.Pages.Select(page => page.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var page in structure.Pages)
        {
            page.RelatedPages = page.RelatedPages
                .Where(related => !string.Equals(related, page.Id, StringComparison.OrdinalIgnoreCase) && pageIdSet.Contains(related))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            page.PrerequisitePages = page.PrerequisitePages
                .Where(prerequisite => !string.Equals(prerequisite, page.Id, StringComparison.OrdinalIgnoreCase) && pageIdSet.Contains(prerequisite))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(page.ParentId) && !pageIdSet.Contains(page.ParentId))
            {
                _logger.LogWarning("结构规划 parentId 无效引用 PageId={PageId} ParentId={ParentId}，已提升为根节点", page.Id, page.ParentId);
                page.ParentId = null;
            }
        }

        // 层级修复：depth > 1 且无 parentId 的页面尝试推断父页面
        ValidateAndFixHierarchy(structure, pageIdSet);

        if (structure.Sections.Count == 0)
        {
            structure.Sections = new List<WikiSectionDto>
            {
                new()
                {
                    Id = "root",
                    Title = comprehensive ? "核心结构" : "页面目录",
                    Pages = structure.Pages.Select(page => page.Id).ToList(),
                    Subsections = new()
                }
            };
        }

        var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < structure.Sections.Count; index++)
        {
            var section = structure.Sections[index] ?? new WikiSectionDto();
            section.Id = NormalizeIdentifier(section.Id, "section", index + 1, sectionIds);
            section.Title = string.IsNullOrWhiteSpace(section.Title) ? $"分组 {index + 1}" : section.Title.Trim();
            section.Pages = section.Pages.Where(pageIdSet.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            section.Subsections = section.Subsections?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new();
            structure.Sections[index] = section;
        }

        structure.RootSections = structure.RootSections
            .Where(rootSectionId => structure.Sections.Any(section => string.Equals(section.Id, rootSectionId, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (structure.RootSections.Count == 0)
        {
            structure.RootSections = structure.Sections.Select(section => section.Id).ToList();
        }

        return structure;
    }

    /// <summary>
    /// 验证并修复页面层级结构：缺失 parentId 的深层页面尝试推断父页面，
    /// 检测循环引用并断开。
    /// </summary>
    private void ValidateAndFixHierarchy(WikiStructureDto structure, HashSet<string> pageIdSet)
    {
        var depthGroups = structure.Pages
            .Where(p => p.Depth > 1 && string.IsNullOrWhiteSpace(p.ParentId))
            .GroupBy(p => p.Depth)
            .OrderBy(g => g.Key);

        foreach (var group in depthGroups)
        {
            foreach (var page in group)
            {
                // 查找同 depth-1 的页面作为候选父页面
                var candidateParents = structure.Pages
                    .Where(p => p.Depth == page.Depth - 1)
                    .ToList();

                if (candidateParents.Count > 0)
                {
                    // 优先选择描述或标题与当前页面主题相关的父页面
                    var bestParent = candidateParents
                        .OrderByDescending(p =>
                        {
                            var score = 0;
                            if (!string.IsNullOrWhiteSpace(page.Description)
                                && !string.IsNullOrWhiteSpace(p.Title)
                                && page.Description.Contains(p.Title[..Math.Min(p.Title.Length, 10)]))
                                score += 10;
                            if (page.FilePaths?.Count > 0 && p.FilePaths?.Count > 0
                                && page.FilePaths.Any(f => p.FilePaths.Any(pf => f.Contains(pf[..Math.Min(pf.Length, 5)]))))
                                score += 5;
                            return score;
                        })
                        .First();

                    page.ParentId = bestParent.Id;
                    _logger.LogInformation(
                        "层级修复：页面 {PageId} (depth={Depth}) 自动指定父页面 {ParentId} ({ParentTitle})",
                        page.Id, page.Depth, bestParent.Id, bestParent.Title);
                }
                else
                {
                    page.Depth = 1;
                    _logger.LogWarning(
                        "层级修复：页面 {PageId} depth={Depth} 无候选父页面，已降为根节点",
                        page.Id, page.Depth);
                }
            }
        }

        // 检测并断开循环引用
        foreach (var page in structure.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.ParentId)) continue;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { page.Id };
            var current = page.ParentId;
            var hasCycle = false;

            while (!string.IsNullOrWhiteSpace(current) && pageIdSet.Contains(current))
            {
                if (!visited.Add(current))
                {
                    hasCycle = true;
                    break;
                }
                var parent = structure.Pages.FirstOrDefault(p => p.Id == current);
                current = parent?.ParentId;
            }

            if (hasCycle)
            {
                _logger.LogWarning("层级修复：页面 {PageId} 存在循环引用（ParentId={ParentId}），已断开", page.Id, page.ParentId);
                page.ParentId = null;
            }
        }
    }

    /// <summary>
    /// 规范化页面草案对象，并继承结构规划阶段的稳定字段。
    /// </summary>
    private static WikiPageDto NormalizePageDraft(WikiPageDto requestedPage, WikiPageDto draft)
    {
        draft.Id = requestedPage.Id;
        draft.Title = string.IsNullOrWhiteSpace(draft.Title) ? requestedPage.Title : draft.Title.Trim();
        draft.NavTitle = string.IsNullOrWhiteSpace(draft.NavTitle) ? draft.Title : draft.NavTitle.Trim();
        draft.Description = string.IsNullOrWhiteSpace(draft.Description) ? requestedPage.Description : draft.Description.Trim();
        draft.Content = WikiMarkdownNormalizer.Normalize(draft.Content);
        draft.Importance = NormalizeImportance(string.IsNullOrWhiteSpace(draft.Importance) ? requestedPage.Importance : draft.Importance);
        draft.PageType = NormalizePageType(string.IsNullOrWhiteSpace(draft.PageType) ? requestedPage.PageType : draft.PageType, requestedPage.IsSection ?? draft.IsSection);
        draft.FilePaths = MergeDistinctLists(requestedPage.FilePaths, draft.FilePaths);
        draft.RelatedPages = MergeDistinctLists(requestedPage.RelatedPages, draft.RelatedPages)
            .Where(pageId => !string.Equals(pageId, draft.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        draft.PrerequisitePages = NormalizeDistinctList(draft.PrerequisitePages)
            .Where(pageId => !string.Equals(pageId, draft.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        draft.ParentId = NormalizeOptionalValue(draft.ParentId) ?? requestedPage.ParentId;
        draft.IsSection ??= requestedPage.IsSection;
        draft.Children = draft.Children is null ? requestedPage.Children : NormalizeDistinctList(draft.Children);
        draft.FrontMatter ??= new();
        draft.FrontMatter.Description = string.IsNullOrWhiteSpace(draft.FrontMatter.Description)
            ? draft.Description
            : draft.FrontMatter.Description.Trim();
        draft.FrontMatter.Summary = string.IsNullOrWhiteSpace(draft.FrontMatter.Summary)
            ? draft.Description
            : draft.FrontMatter.Summary.Trim();
        draft.FrontMatter.Tags = NormalizeDistinctList(draft.FrontMatter.Tags);
        draft.FrontMatter.SourceFiles = MergeDistinctLists(draft.FilePaths, draft.FrontMatter.SourceFiles);
        draft.Outline ??= new();
        draft.SourceCoverage ??= new();
        draft.SourceCoverage.PrimaryFiles = MergeDistinctLists(draft.FilePaths, draft.SourceCoverage.PrimaryFiles);
        draft.SourceCoverage.Evidence ??= new();
        draft.Warnings ??= new();
        draft.IsFallbackDraft = false;

        if (string.IsNullOrWhiteSpace(draft.Content))
        {
            draft.Content = $"## 概览\n\n{draft.Description}";
            draft.Warnings.Add("页面正文为空，已根据页面描述生成最小草案。");
        }

        return draft;
    }

    /// <summary>
    /// 构建页面兜底草案。
    /// </summary>
    private static WikiPageDto BuildFallbackPageDraft(WikiPageDto requestedPage, string response)
    {
        var normalizedResponse = WikiMarkdownNormalizer.Normalize(response);
        var content = string.IsNullOrWhiteSpace(normalizedResponse)
            ? $"## 页面说明\n\n{requestedPage.Description}"
            : normalizedResponse;

        return new WikiPageDto
        {
            Id = requestedPage.Id,
            Title = requestedPage.Title,
            NavTitle = string.IsNullOrWhiteSpace(requestedPage.NavTitle) ? requestedPage.Title : requestedPage.NavTitle,
            Description = requestedPage.Description,
            Content = content,
            PageType = NormalizePageType(requestedPage.PageType, requestedPage.IsSection),
            FilePaths = NormalizeDistinctList(requestedPage.FilePaths),
            Importance = NormalizeImportance(requestedPage.Importance),
            RelatedPages = NormalizeDistinctList(requestedPage.RelatedPages),
            PrerequisitePages = NormalizeDistinctList(requestedPage.PrerequisitePages),
            ParentId = requestedPage.ParentId,
            IsSection = requestedPage.IsSection,
            Children = requestedPage.Children is null ? null : NormalizeDistinctList(requestedPage.Children),
            FrontMatter = new WikiPageFrontMatterDto
            {
                Summary = requestedPage.Description,
                Description = requestedPage.Description,
                SourceFiles = NormalizeDistinctList(requestedPage.FilePaths)
            },
            SourceCoverage = new WikiPageSourceCoverageDto
            {
                PrimaryFiles = NormalizeDistinctList(requestedPage.FilePaths),
                Evidence = requestedPage.FilePaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(filePath => new WikiPageSourceEvidenceDto
                    {
                        FilePath = filePath,
                        Reason = "该文件由结构规划阶段关联到当前页面。",
                        Symbols = new()
                    })
                    .ToList()
            },
            Outline = new(),
            Warnings = new() { "页面草案使用了后端兜底逻辑，请关注模型输出质量。" },
            IsFallbackDraft = true
        };
    }

    /// <summary>
    /// 基于旧版 XML 响应解析结构结果。
    /// </summary>
    private WikiStructureDto ParseStructureFromXml(string response, bool comprehensive)
    {
        try
        {
            var cleaned = WikiMarkdownNormalizer.Normalize(response);
            var match = Regex.Match(cleaned, "<wiki_structure>[\\s\\S]*?</wiki_structure>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                _logger.LogWarning("LLM 未返回有效 Wiki XML，使用兜底结构");
                return BuildFallbackStructure(response);
            }

            var xml = SanitizeXml(match.Value);
            XDocument document;
            try
            {
                document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                xml = RepairXmlIssues(xml);
                document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }

            var root = document.Root!;
            var sections = root.Element("sections")?.Elements("section").Select(section => new WikiSectionDto
            {
                Id = section.Attribute("id")?.Value ?? string.Empty,
                Title = section.Element("title")?.Value.Trim() ?? string.Empty,
                Pages = section.Element("pages")?.Elements("page_ref")
                    .Select(page => page.Value.Trim())
                    .Where(page => !string.IsNullOrWhiteSpace(page))
                    .ToList() ?? new(),
                Subsections = section.Element("subsections")?.Elements("section_ref")
                    .Select(reference => reference.Value.Trim())
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .ToList() ?? new()
            }).Where(section => !string.IsNullOrWhiteSpace(section.Id)).ToList() ?? new();

            var pages = root.Element("pages")?.Elements("page").Select(page => new WikiPageDto
            {
                Id = page.Attribute("id")?.Value ?? string.Empty,
                Title = page.Element("title")?.Value.Trim() ?? string.Empty,
                NavTitle = page.Element("title")?.Value.Trim() ?? string.Empty,
                Description = page.Element("description")?.Value.Trim() ?? string.Empty,
                Importance = NormalizeImportance(page.Element("importance")?.Value),
                FilePaths = page.Element("relevant_files")?.Elements("file_path")
                    .Select(file => file.Value.Trim())
                    .Where(file => !string.IsNullOrWhiteSpace(file))
                    .ToList() ?? new(),
                RelatedPages = page.Element("related_pages")?.Elements("related")
                    .Select(related => related.Value.Trim())
                    .Where(related => !string.IsNullOrWhiteSpace(related))
                    .ToList() ?? new(),
                ParentId = NormalizeOptionalValue(page.Element("parent_section")?.Value),
                PageType = "article",
                FrontMatter = new(),
                Outline = new(),
                SourceCoverage = new()
            }).Where(page => !string.IsNullOrWhiteSpace(page.Id) && !string.IsNullOrWhiteSpace(page.Title)).ToList() ?? new();

            var structure = new WikiStructureDto
            {
                Id = "wiki",
                Title = root.Element("title")?.Value.Trim() ?? "Repository Wiki",
                Description = root.Element("description")?.Value.Trim() ?? string.Empty,
                Pages = pages,
                Sections = sections,
                RootSections = sections.Select(section => section.Id).ToList()
            };

            return NormalizeStructure(structure, comprehensive);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XML 解析失败，尝试正则兜底提取");
            var regexStructure = ParseStructureWithRegex(response, comprehensive);
            if (regexStructure.Pages.Count > 0)
            {
                return regexStructure;
            }

            _logger.LogWarning("Regex 提取也失败，使用硬编码兜底");
            return BuildFallbackStructure(response);
        }
    }

    /// <summary>
    /// 当 XML 解析失败时，使用正则尽量恢复结构对象。
    /// </summary>
    private WikiStructureDto ParseStructureWithRegex(string response, bool comprehensive)
    {
        try
        {
            var cleaned = WikiMarkdownNormalizer.Normalize(response);
            var blockMatch = Regex.Match(cleaned, "<wiki_structure>(.*?)</wiki_structure>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!blockMatch.Success)
            {
                return new WikiStructureDto { Pages = new() };
            }

            var block = blockMatch.Groups[1].Value;
            var titleMatch = Regex.Match(block, "<title>\\s*(.*?)\\s*</title>", RegexOptions.Singleline);
            var descriptionMatch = Regex.Match(block, "<description>\\s*(.*?)\\s*</description>", RegexOptions.Singleline);

            var pages = new List<WikiPageDto>();
            foreach (Match pageMatch in Regex.Matches(block, @"<page\s[^>]*id\s*=\s*""([^""]+)""[^>]*>(.*?)</page>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var inner = pageMatch.Groups[2].Value;
                var draft = new WikiPageDto
                {
                    Id = pageMatch.Groups[1].Value,
                    Title = Regex.Match(inner, @"<title>\s*(.*?)\s*</title>", RegexOptions.Singleline).Groups[1].Value.Trim(),
                    NavTitle = Regex.Match(inner, @"<title>\s*(.*?)\s*</title>", RegexOptions.Singleline).Groups[1].Value.Trim(),
                    Description = Regex.Match(inner, @"<description>\s*(.*?)\s*</description>", RegexOptions.Singleline).Groups[1].Value.Trim(),
                    Importance = NormalizeImportance(Regex.Match(inner, @"<importance>\s*(.*?)\s*</importance>", RegexOptions.Singleline).Groups[1].Value),
                    FilePaths = Regex.Matches(inner, @"<file_path>\s*(.*?)\s*</file_path>", RegexOptions.Singleline)
                        .Select(match => match.Groups[1].Value.Trim())
                        .Where(file => !string.IsNullOrWhiteSpace(file))
                        .ToList(),
                    RelatedPages = Regex.Matches(inner, @"<related>\s*(.*?)\s*</related>", RegexOptions.Singleline)
                        .Select(match => match.Groups[1].Value.Trim())
                        .Where(file => !string.IsNullOrWhiteSpace(file))
                        .ToList(),
                    ParentId = NormalizeOptionalValue(Regex.Match(inner, @"<parent_section>\s*(.*?)\s*</parent_section>", RegexOptions.Singleline).Groups[1].Value.Trim()),
                    FrontMatter = new(),
                    Outline = new(),
                    SourceCoverage = new()
                };

                if (!string.IsNullOrWhiteSpace(draft.Id) && !string.IsNullOrWhiteSpace(draft.Title))
                {
                    pages.Add(draft);
                }
            }

            var sections = new List<WikiSectionDto>();
            foreach (Match sectionMatch in Regex.Matches(block, @"<section\s[^>]*id\s*=\s*""([^""]+)""[^>]*>(.*?)</section>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var inner = sectionMatch.Groups[2].Value;
                sections.Add(new WikiSectionDto
                {
                    Id = sectionMatch.Groups[1].Value,
                    Title = Regex.Match(inner, @"<title>\s*(.*?)\s*</title>", RegexOptions.Singleline).Groups[1].Value.Trim(),
                    Pages = Regex.Matches(inner, @"<page_ref>\s*(.*?)\s*</(?:page_ref|[^>]+)>", RegexOptions.Singleline)
                        .Select(match => match.Groups[1].Value.Trim())
                        .Where(pageId => !string.IsNullOrWhiteSpace(pageId))
                        .ToList(),
                    Subsections = Regex.Matches(inner, @"<section_ref>\s*(.*?)\s*</(?:section_ref|[^>]+)>", RegexOptions.Singleline)
                        .Select(match => match.Groups[1].Value.Trim())
                        .Where(sectionId => !string.IsNullOrWhiteSpace(sectionId))
                        .ToList()
                });
            }

            if (pages.Count == 0)
            {
                return new WikiStructureDto { Pages = new() };
            }

            var structure = new WikiStructureDto
            {
                Id = "wiki",
                Title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "Repository Wiki",
                Description = descriptionMatch.Success ? descriptionMatch.Groups[1].Value.Trim() : string.Empty,
                Pages = pages,
                Sections = sections,
                RootSections = sections.Select(section => section.Id).ToList()
            };

            return NormalizeStructure(structure, comprehensive);
        }
        catch
        {
            return new WikiStructureDto { Pages = new() };
        }
    }

    /// <summary>
    /// 清洗 XML 中常见的非法实体写法。
    /// </summary>
    private static string SanitizeXml(string xml)
    {
        return Regex.Replace(xml, "&(?![a-zA-Z]+;|#\\d+;|#x[0-9a-fA-F]+;)", "&amp;");
    }

    /// <summary>
    /// 修复已知的 XML 结束标签错误。
    /// </summary>
    private static string RepairXmlIssues(string xml)
    {
        xml = Regex.Replace(xml,
            "(<parent_section>\\s*[^<]*?)</section>(\\s*</page>)",
            "$1</parent_section>$2", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml,
            "(<parent_section>\\s*[^<]*?)</section>(\\s*</related_pages>)",
            "$1</parent_section>$2", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, @"(<page_ref>[^<]+)</[^>]+>", "$1</page_ref>", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, @"(<section_ref>[^<]+)</[^>]+>", "$1</section_ref>", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, @"(<related>[^<]+)</[^>]+>", "$1</related>", RegexOptions.IgnoreCase);
        return xml;
    }

    /// <summary>
    /// 构建结构规划兜底结果。
    /// </summary>
    private static WikiStructureDto BuildFallbackStructure(string response)
    {
        return new WikiStructureDto
        {
            Id = "wiki",
            Title = "Repository Wiki",
            Description = $"结构规划输出未能解析为有效结果。原始响应：{response}",
            Pages = new List<WikiPageDto>
            {
                new()
                {
                    Id = "overview",
                    Title = "仓库概览",
                    NavTitle = "仓库概览",
                    Description = response.Length > 500 ? response[..500] : response,
                    Importance = "high",
                    PageType = "overview",
                    FrontMatter = new(),
                    Outline = new(),
                    SourceCoverage = new()
                }
            },
            Sections = new List<WikiSectionDto>
            {
                new()
                {
                    Id = "root",
                    Title = "仓库概览",
                    Pages = new() { "overview" },
                    Subsections = new()
                }
            },
            RootSections = new() { "root" }
        };
    }

    /// <summary>
    /// 规范化页面与分组标识。
    /// </summary>
    private static string NormalizeIdentifier(string? rawId, string prefix, int index, ISet<string> existingIds)
    {
        var candidate = Regex.Replace(rawId?.Trim() ?? string.Empty, "[^a-zA-Z0-9\\-_]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = $"{prefix}-{index:D2}";
        }

        var normalized = candidate;
        var suffix = 2;
        while (!existingIds.Add(normalized))
        {
            normalized = $"{candidate}-{suffix++}";
        }

        return normalized.ToLowerInvariant();
    }

    /// <summary>
    /// 规范化重要性字段。
    /// </summary>
    private static string NormalizeImportance(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "low" => "low",
            _ => "medium"
        };
    }

    /// <summary>
    /// 规范化页面类型字段。
    /// </summary>
    private static string NormalizePageType(string? value, bool? isSection)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized is "overview" or "section" or "article" or "appendix")
        {
            return normalized;
        }

        return isSection == true ? "section" : "article";
    }

    /// <summary>
    /// 规范化可空文本。
    /// </summary>
    private static string? NormalizeOptionalValue(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    /// <summary>
    /// 规范化去重字符串集合。
    /// </summary>
    private static List<string> NormalizeDistinctList(IEnumerable<string>? values)
    {
        return values?
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList() ?? new();
    }

    /// <summary>
    /// 合并两个去重字符串集合。
    /// </summary>
    private static List<string> MergeDistinctLists(IEnumerable<string>? primary, IEnumerable<string>? secondary)
    {
        return NormalizeDistinctList((primary ?? Enumerable.Empty<string>()).Concat(secondary ?? Enumerable.Empty<string>()));
    }
}
