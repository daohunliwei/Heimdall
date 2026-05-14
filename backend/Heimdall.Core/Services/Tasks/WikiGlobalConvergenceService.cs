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
                Issues = issues
            }
        };
    }
}
