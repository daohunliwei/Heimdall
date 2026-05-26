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

    public WikiStructureDto BuildStructure(CodeIndexResult indexResult, string language = "zh", int? maxPages = null)
    {
        var entries = indexResult.Entries;
        var modules = indexResult.ModuleNames;
        var sections = new List<WikiSectionDto>();
        var pages = new List<WikiPageDto>();

        var effectiveMaxPages = maxPages ?? 120; // 默认上限 120 页（= recommendedPageCount 80 × 1.5）

        _logger.LogInformation("Deterministic 结构规划: {Modules} 模块, {Files} 文件, 目标 ≤ {MaxPages} 页",
            modules.Count, entries.Count, effectiveMaxPages);

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

        // 2. Module → Section，按目录聚合
        foreach (var module in modules)
        {
            var moduleEntries = entries
                .Where(e => e.ModuleName == module && e.FileType is "source" or "config")
                .OrderBy(e => DepthOf(e.FilePath)).ThenBy(e => e.FilePath)
                .ToList();
            if (moduleEntries.Count == 0) continue;

            // 按目录分组
            var dirGroups = moduleEntries
                .GroupBy(e => Path.GetDirectoryName(e.FilePath) ?? "")
                .OrderBy(g => g.Key)
                .ToList();

            var modulePages = new List<string>();

            foreach (var dirGroup in dirGroups)
            {
                var dirFiles = dirGroup.OrderByDescending(e => e.ImportanceScore).ToList();
                var dirName = dirGroup.Key;
                var isTestDir = dirName.Contains("Tests") || dirName.Contains("test") || dirName.Contains("Test");

                // 测试目录：整个子目录合并为一页
                if (isTestDir)
                {
                    var testPageId = SanitizeId($"test-{dirName.Replace('/', '-').Replace('\\', '-')}");
                    var testPageTitle = language == "zh"
                        ? $"{Path.GetFileName(dirName)} 测试"
                        : $"{Path.GetFileName(dirName)} Tests";
                    pages.Add(new WikiPageDto
                    {
                        Id = testPageId,
                        Title = testPageTitle,
                        PageType = "section", Importance = "low", Depth = DepthOf(dirFiles[0].FilePath),
                        ContentDepthLevel = "standard",
                        FilePaths = dirFiles.Select(f => f.FilePath).ToList()
                    });
                    modulePages.Add(testPageId);
                    continue;
                }

                // 跳过纯配置文件
                var sourceFiles = dirFiles
                    .Where(f => !IsConfigFile(f.FilePath))
                    .ToList();
                if (sourceFiles.Count == 0) continue;

                // ≤3 个源文件 → 合并为一页
                if (sourceFiles.Count <= 3)
                {
                    var mergedId = SanitizeId($"group-{dirName.Replace('/', '-').Replace('\\', '-')}");
                    var topFile = sourceFiles[0];
                    var mergedTitle = sourceFiles.Count == 1
                        ? HumanizeIdentifier(Path.GetFileNameWithoutExtension(topFile.FilePath))
                        : (language == "zh" ? $"{Path.GetFileName(dirName)} 目录工具集" : $"{Path.GetFileName(dirName)} Utilities");
                    pages.Add(new WikiPageDto
                    {
                        Id = mergedId,
                        Title = mergedTitle,
                        PageType = sourceFiles.Count == 1 && topFile.ImportanceScore >= 8 ? "article" : "section",
                        Importance = sourceFiles.Any(f => f.ImportanceScore >= 8) ? "high" : "medium",
                        Depth = DepthOf(sourceFiles[0].FilePath),
                        ContentDepthLevel = "standard",
                        FilePaths = sourceFiles.Select(f => f.FilePath).ToList()
                    });
                    modulePages.Add(mergedId);
                }
                else
                {
                    // >3 个文件：top-3 独立成页，其余合并
                    var top3 = sourceFiles.Take(3).ToList();
                    var rest = sourceFiles.Skip(3).ToList();

                    foreach (var entry in top3)
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

                    if (rest.Count > 0)
                    {
                        var restId = SanitizeId($"more-{dirName.Replace('/', '-').Replace('\\', '-')}");
                        pages.Add(new WikiPageDto
                        {
                            Id = restId,
                            Title = language == "zh"
                                ? $"{Path.GetFileName(dirName)} 其他文件"
                                : $"{Path.GetFileName(dirName)} Other Files",
                            PageType = "section", Importance = "low", Depth = DepthOf(rest[0].FilePath),
                            ContentDepthLevel = "standard",
                            FilePaths = rest.Select(f => f.FilePath).ToList()
                        });
                        modulePages.Add(restId);
                    }
                }
            }

            if (modulePages.Count > 0)
            {
                sections.Add(new WikiSectionDto
                {
                    Id = SanitizeId($"section-{module}"),
                    Title = module, Depth = 0, Pages = modulePages
                });
            }
        }

        // 3. Architecture Section（高重要性文件，去重）
        var seen = new HashSet<string>(pages.Select(p => p.Id));
        var topEntries = entries
            .Where(e => e.FileType is "source" && e.ImportanceScore >= 7
                && !seen.Contains(SanitizeId(Path.GetFileNameWithoutExtension(e.FilePath)))
                && !IsConfigFile(e.FilePath))
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

        // 4. 页数上限保护
        if (pages.Count > effectiveMaxPages)
        {
            _logger.LogWarning("Deterministic 页数 {Actual} 超过上限 {Max}，合并低重要性页面",
                pages.Count, effectiveMaxPages);
            MergeLowImportancePages(pages, sections, effectiveMaxPages);
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

    /// <summary>将低重要性页面合并到其所属 Section 的"其他"页面中，使总页数降至 target 以内。</summary>
    private static void MergeLowImportancePages(List<WikiPageDto> pages, List<WikiSectionDto> sections, int target)
    {
        foreach (var section in sections)
        {
            if (pages.Count <= target) break;

            var sectionPages = pages.Where(p => section.Pages.Contains(p.Id)).ToList();
            var lowPages = sectionPages
                .Where(p => p.Importance == "low" && (p.FilePaths?.Count ?? 0) > 0)
                .ToList();

            if (lowPages.Count <= 1) continue;

            // 合并低重要性页面：收集所有文件路径到第一个低优先级页
            var merged = lowPages[0];
            var allFiles = lowPages.SelectMany(p => p.FilePaths ?? new List<string>()).Distinct().ToList();
            merged.FilePaths = allFiles;
            merged.Title += "（含其他文件）";
            merged.Importance = "low";

            // 从 pages 和 section 中移除被合并的其余低优先级页
            var toRemove = lowPages.Skip(1).Select(p => p.Id).ToHashSet();
            pages.RemoveAll(p => toRemove.Contains(p.Id));
            section.Pages = section.Pages.Where(id => !toRemove.Contains(id)).ToList();

            if (pages.Count <= target) break;
        }
    }

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".xml", ".config", ".csproj", ".sln",
        ".props", ".targets", ".md", ".txt", ".yml", ".yaml",
        ".gitignore", ".gitattributes", ".editorconfig"
    };

    private static bool IsConfigFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ConfigExtensions.Contains(ext);
    }

    public WikiStructureDto BuildSkeleton(CodeIndexResult indexResult, string language = "zh", int? maxPages = null)
    {
        var dto = BuildStructure(indexResult, language, maxPages);
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
