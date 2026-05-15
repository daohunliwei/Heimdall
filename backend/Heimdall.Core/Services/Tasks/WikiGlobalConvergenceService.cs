using Heimdall.Core.Models;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Wiki 全局收敛服务。
/// 该服务负责在页面批次生成完成后统一处理导航命名、双向关联、父子层级与基础质量校验，
/// 使结构工件可以稳定驱动后续渲染与持久化。
/// </summary>
public sealed class WikiGlobalConvergenceService
{
    /// <summary>
    /// 执行全局收敛。
    /// </summary>
    public WikiConvergenceResultDto Converge(WikiStructureDto structure)
    {
        structure.Pages ??= new();
        structure.Sections ??= new();
        structure.RootSections ??= new();

        var pageLookup = structure.Pages.ToDictionary(page => page.Id, StringComparer.OrdinalIgnoreCase);
        var duplicateCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedNavTitleCount = 0;
        var reciprocalRelationCount = 0;
        var childLinkCount = 0;
        var issues = new List<string>();

        foreach (var page in structure.Pages)
        {
            page.Warnings ??= new();
            page.NavTitle = string.IsNullOrWhiteSpace(page.NavTitle) ? page.Title : page.NavTitle.Trim();
            page.RelatedPages = page.RelatedPages
                .Where(relatedId => !string.Equals(relatedId, page.Id, StringComparison.OrdinalIgnoreCase) && pageLookup.ContainsKey(relatedId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            page.PrerequisitePages = page.PrerequisitePages
                .Where(prerequisiteId => !string.Equals(prerequisiteId, page.Id, StringComparison.OrdinalIgnoreCase) && pageLookup.ContainsKey(prerequisiteId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            page.Children ??= new();

            if (duplicateCounter.TryGetValue(page.NavTitle, out var existingCount))
            {
                duplicateCounter[page.NavTitle] = existingCount + 1;
                page.NavTitle = $"{page.NavTitle}（{existingCount + 1}）";
                normalizedNavTitleCount++;
            }
            else
            {
                duplicateCounter[page.NavTitle] = 1;
            }

            if (string.IsNullOrWhiteSpace(page.Content))
            {
                issues.Add($"页面“{page.Title}”正文为空。");
            }
        }

        foreach (var page in structure.Pages)
        {
            foreach (var relatedId in page.RelatedPages.ToList())
            {
                var relatedPage = pageLookup[relatedId];
                if (!relatedPage.RelatedPages.Contains(page.Id, StringComparer.OrdinalIgnoreCase))
                {
                    relatedPage.RelatedPages.Add(page.Id);
                    reciprocalRelationCount++;
                }
            }

            if (!string.IsNullOrWhiteSpace(page.ParentId) && pageLookup.TryGetValue(page.ParentId, out var parentPage))
            {
                parentPage.Children ??= new();
                if (!parentPage.Children.Contains(page.Id, StringComparer.OrdinalIgnoreCase))
                {
                    parentPage.Children.Add(page.Id);
                    childLinkCount++;
                }
            }
        }

        foreach (var section in structure.Sections)
        {
            section.Pages = section.Pages
                .Where(pageLookup.ContainsKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var emptyContentPageCount = structure.Pages.Count(page => string.IsNullOrWhiteSpace(page.Content));
        var fallbackPageCount = structure.Pages.Count(page => page.IsFallbackDraft);

        // V4: 对每页计算质量评分并识别弱页面
        var pageQualityScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var weakPageIds = new List<string>();
        const int qualityThreshold = 60;

        foreach (var page in structure.Pages)
        {
            var score = CalculatePageQualityScore(page, pageLookup);
            pageQualityScores[page.Id] = score;
            if (score < qualityThreshold && !page.IsFallbackDraft)
            {
                weakPageIds.Add(page.Id);
            }
        }

        return new WikiConvergenceResultDto
        {
            Structure = structure,
            QualityReport = new WikiQualityReportDto
            {
                PageCount = structure.Pages.Count,
                FallbackPageCount = fallbackPageCount,
                EmptyContentPageCount = emptyContentPageCount,
                NormalizedNavTitleCount = normalizedNavTitleCount,
                AddedReciprocalRelationCount = reciprocalRelationCount,
                AddedChildLinkCount = childLinkCount,
                Issues = issues,
                PageQualityScores = pageQualityScores,
                WeakPageIds = weakPageIds
            }
        };
    }

    /// <summary>
    /// V4 计算单页质量评分（0-100），评估维度：内容覆盖度、技术深度、可读性、相关性。
    /// </summary>
    /// <param name="page">被评估的页面。</param>
    /// <param name="allPages">所有页面查找表。</param>
    /// <returns>0-100 的质量评分。</returns>
    private static int CalculatePageQualityScore(WikiPageDto page, Dictionary<string, WikiPageDto> allPages)
    {
        var score = 50; // 基础分

        // 内容覆盖度：正文长度
        if (!string.IsNullOrWhiteSpace(page.Content))
        {
            var contentLen = page.Content.Length;
            if (contentLen > 3000) score += 15;
            else if (contentLen > 1000) score += 10;
            else if (contentLen > 300) score += 5;
            else score -= 10; // 内容过短
        }
        else
        {
            score -= 30; // 无内容
        }

        // 技术深度：包含代码块、表格等技术元素
        if (page.Content?.Contains("```") == true) score += 10;
        if (page.Content?.Contains("|") == true && page.Content?.Contains("---") == true) score += 5;
        if (page.Content?.Contains("##") == true) score += 5; // 有结构化标题

        // 关联性：有相关页面引用
        if (page.RelatedPages.Count >= 3) score += 8;
        else if (page.RelatedPages.Count >= 1) score += 4;

        // 源文件覆盖：有足够的源文件关联
        if (page.FilePaths.Count >= 8) score += 7;
        else if (page.FilePaths.Count >= 3) score += 3;

        // 兜底草案扣分
        if (page.IsFallbackDraft) score -= 20;

        return Math.Clamp(score, 0, 100);
    }
}
