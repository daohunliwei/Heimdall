using System.Text.RegularExpressions;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Repository;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

public class DeterministicStructurePlanner
{
    private readonly ILogger<DeterministicStructurePlanner> _logger;

    public DeterministicStructurePlanner(ILogger<DeterministicStructurePlanner> logger)
    {
        _logger = logger;
    }

    public WikiStructureDto BuildStructure(CodeIndexResult indexResult, string language = "zh")
    {
        var entries = indexResult.Entries;
        var modules = indexResult.ModuleNames;
        var sections = new List<WikiSectionDto>();
        var pages = new List<WikiPageDto>();

        _logger.LogInformation("Deterministic 结构规划: {Modules} 模块, {Files} 文件", modules.Count, entries.Count);

        // 1. Overview Section
        if (indexResult.EntryPointFiles.Count > 0)
        {
            var overviewPages = new List<string>();
            pages.Add(new WikiPageDto
            {
                Id = "page-overview",
                Title = language == "zh" ? "项目概览" : "Project Overview",
                PageType = "overview", Importance = "high", Depth = 0,
                ContentDepthLevel = "overview",
                FilePaths = indexResult.EntryPointFiles.Take(5).ToList()
            });
            overviewPages.Add("page-overview");
            sections.Add(new WikiSectionDto
            {
                Id = "section-overview",
                Title = language == "zh" ? "概览" : "Overview",
                Depth = 0, Pages = overviewPages
            });
        }

        // 2. Module → Section
        foreach (var module in modules)
        {
            var moduleEntries = entries
                .Where(e => e.ModuleName == module && e.FileType is "source" or "config")
                .OrderBy(e => DepthOf(e.FilePath)).ThenBy(e => e.FilePath)
                .ToList();
            if (moduleEntries.Count == 0) continue;

            var modulePages = new List<string>();
            foreach (var entry in moduleEntries)
            {
                var pageId = SanitizeId(Path.GetFileNameWithoutExtension(entry.FilePath));
                pages.Add(new WikiPageDto
                {
                    Id = pageId,
                    Title = HumanizeIdentifier(Path.GetFileNameWithoutExtension(entry.FilePath)),
                    PageType = entry.ImportanceScore >= 8 ? "article" : "section",
                    Importance = entry.ImportanceScore >= 8 ? "high" : "medium",
                    Depth = DepthOf(entry.FilePath),
                    ContentDepthLevel = entry.ImportanceScore >= 10 ? "detailed" : "standard",
                    FilePaths = new List<string> { entry.FilePath }
                });
                modulePages.Add(pageId);
            }
            sections.Add(new WikiSectionDto
            {
                Id = SanitizeId($"section-{module}"),
                Title = module, Depth = 0, Pages = modulePages
            });
        }

        // 3. Architecture Section
        var seen = new HashSet<string>(pages.Select(p => p.Id));
        var topEntries = entries
            .Where(e => e.FileType is "source" && e.ImportanceScore >= 7
                && !seen.Contains(SanitizeId(Path.GetFileNameWithoutExtension(e.FilePath))))
            .OrderByDescending(e => e.ImportanceScore).Take(10).ToList();

        if (topEntries.Count > 0)
        {
            var archPages = new List<string>();
            foreach (var entry in topEntries)
            {
                var pid = SanitizeId(Path.GetFileNameWithoutExtension(entry.FilePath));
                pages.Add(new WikiPageDto
                {
                    Id = pid,
                    Title = HumanizeIdentifier(Path.GetFileNameWithoutExtension(entry.FilePath)),
                    PageType = "article", Importance = "high",
                    Depth = DepthOf(entry.FilePath), ContentDepthLevel = "detailed",
                    FilePaths = new List<string> { entry.FilePath }
                });
                archPages.Add(pid);
            }
            sections.Add(new WikiSectionDto
            {
                Id = "section-architecture",
                Title = language == "zh" ? "核心架构" : "Architecture",
                Depth = 0, Pages = archPages
            });
        }

        return new WikiStructureDto
        {
            Id = "wiki",
            Title = language == "zh" ? "代码文档" : "Code Documentation",
            Description = language == "zh"
                ? $"共 {modules.Count} 个模块、{entries.Count(e => e.FileType is "source" or "config")} 个源文件"
                : $"{modules.Count} modules",
            RootSections = sections.Select(s => s.Id).ToList(),
            Sections = sections, Pages = pages
        };
    }

    public WikiStructureDto BuildSkeleton(CodeIndexResult indexResult, string language = "zh")
    {
        var dto = BuildStructure(indexResult, language);
        foreach (var s in dto.Sections) s.Title = $"[SKELETON] {s.Title}";
        foreach (var p in dto.Pages) p.Title = $"[SKELETON] {p.Title}";
        return dto;
    }

    internal static string HumanizeIdentifier(string name)
    {
        var spaced = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        return System.Text.RegularExpressions.Regex.Replace(spaced, "([A-Z]+)([A-Z][a-z])", "$1 $2");
    }

    private static int DepthOf(string path) => path.Count(c => c == '/' || c == '\\');

    private static string SanitizeId(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}
