using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Heimdall.Infrastructure.Services;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

/// <summary>
/// Wiki 任务执行仓储实现。
/// 该实现负责在单一事务中完成 Wiki 任务主链路的核心落库。
/// </summary>
public sealed partial class WikiTaskExecutionRepository : IWikiTaskExecutionRepository
{
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly JsonSerializerOptions PrettyJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly ISqlSugarClient _db;
    private readonly WorkspaceService _workspace;

    /// <summary>
    /// 初始化 Wiki 任务执行仓储。
    /// </summary>
    public WikiTaskExecutionRepository(ISqlSugarClient db, WorkspaceService workspace)
    {
        _db = db;
        _workspace = workspace;
    }

    /// <summary>
    /// 在同一事务中持久化 Wiki 版本、页面、关系与渲染快照。
    /// V4：已移除旧 Wiki 实体依赖，Wiki 数据直接归属 WikiVersion。
    /// </summary>
    public async Task<(Guid RepositoryVersionId, Guid WikiVersionId, List<WikiPage> Pages)> PersistWikiProjectionAsync(
        TaskRecord task,
        WikiStructureDto structure,
        string structureJson,
        string language,
        string branch,
        string generationProfile,
        Guid? astVersionId = null,
        CancellationToken cancellationToken = default)
    {
        await _db.Ado.BeginTranAsync();

        try
        {
            var repositoryId = task.RepositoryId
                ?? throw new InvalidOperationException($"任务缺少 RepositoryId：{task.Id}");

            var repository = await _db.Queryable<Heimdall.Core.Entities.Repository>().FirstAsync(r => r.Id == repositoryId)
                ?? throw new InvalidOperationException($"仓库不存在：{repositoryId}");

            // V4：旧 Wiki 实体已移除，Wiki 空间和版本直接创建，不依赖 Wiki 主记录
            RepositoryVersion? repositoryVersion = null;
            if (task.ResolvedRepositoryVersionId.HasValue)
            {
                repositoryVersion = await _db.Queryable<RepositoryVersion>()
                    .FirstAsync(v => v.Id == task.ResolvedRepositoryVersionId.Value);
            }

            repositoryVersion ??= await _db.Queryable<RepositoryVersion>()
                .Where(v => v.RepositoryId == repositoryId && v.BranchName == branch && v.IsLatestOnBranch)
                .OrderByDescending(v => v.CreatedAt)
                .FirstAsync();

            if (repositoryVersion is null)
            {
                repositoryVersion = new RepositoryVersion
                {
                    RepositoryId = repositoryId,
                    BranchName = branch,
                    CommitSha = "unknown",
                    CommitTime = DateTime.UtcNow,
                    CommitAuthor = "system",
                    CommitMessage = $"由任务 {task.Id} 触发生成",
                    SourceStatus = "active",
                    IsLatestOnBranch = true,
                    VersionSourceConfidence = "unknown"
                };
                await _db.Insertable(repositoryVersion).ExecuteCommandAsync(cancellationToken);
            }

            var wikiSpace = await _db.Queryable<WikiSpace>()
                .FirstAsync(s => s.RepositoryId == repositoryId
                    && s.Language == language
                    && s.ViewType == "default");

            if (wikiSpace is null)
            {
                wikiSpace = new WikiSpace
                {
                    RepositoryId = repositoryId,
                    Language = language,
                    ViewType = "default",
                    Title = $"{repository.DisplayName} Wiki",
                    Description = $"为 {repository.DisplayName} 生成的 Wiki"
                };
                await _db.Insertable(wikiSpace).ExecuteCommandAsync(cancellationToken);
            }

            // 始终创建新版本，不复用已有版本
            var maxVersionNo = await _db.Queryable<WikiVersion>()
                .Where(v => v.WikiSpaceId == wikiSpace.Id)
                .MaxAsync(v => (int?)v.VersionNo) ?? 0;

            var wikiVersion = new WikiVersion
            {
                WikiSpaceId = wikiSpace.Id,
                RepositoryVersionId = repositoryVersion.Id,
                AstVersionId = astVersionId,
                VersionNo = maxVersionNo + 1,
                GenerationMode = task.ForceRefresh ? "rebuild" : "latest",
                GenerationProfile = generationProfile,
                Status = "generating",
                PageCount = 0,
                TocDepth = 1,
                SummaryMarkdown = $"由任务 {task.Id} 生成",
                StructureJson = structureJson,
                CreatedByTaskId = task.Id,
                IsForceRefresh = task.ForceRefresh
            };
            await _db.Insertable(wikiVersion).ExecuteCommandAsync(cancellationToken);

            await _db.Deleteable<WikiPageRelation>()
                .Where(r => r.WikiVersionId == wikiVersion.Id)
                .ExecuteCommandAsync(cancellationToken);

            await _db.Deleteable<WikiPage>()
                .Where(p => p.WikiVersionId == wikiVersion.Id)
                .ExecuteCommandAsync(cancellationToken);

            var persistedPages = new List<WikiPage>();
            foreach (var pageWithIndex in structure.Pages.Select((page, index) => new { page, index }))
            {
                // V4：WikiPage 直接归属 WikiVersion，不再通过 Wiki 关联
                persistedPages.Add(new WikiPage
                {
                    WikiVersionId = wikiVersion.Id,
                    TaskId = task.Id,
                    PageOrder = pageWithIndex.index,
                    Title = pageWithIndex.page.Title,
                    NavTitle = string.IsNullOrWhiteSpace(pageWithIndex.page.NavTitle) ? pageWithIndex.page.Title : pageWithIndex.page.NavTitle,
                    ContentMarkdown = pageWithIndex.page.Content,
                    PageType = string.IsNullOrWhiteSpace(pageWithIndex.page.PageType)
                        ? (pageWithIndex.page.IsSection == true ? "section" : "article")
                        : pageWithIndex.page.PageType,
                    Importance = pageWithIndex.page.Importance,
                    OutlineJson = JsonSerializer.Serialize(pageWithIndex.page.Outline ?? new List<WikiPageHeadingDto>(), ArtifactJsonOptions),
                    Summary = string.IsNullOrWhiteSpace(pageWithIndex.page.FrontMatter?.Summary)
                        ? pageWithIndex.page.Description
                        : pageWithIndex.page.FrontMatter.Summary,
                    SourceCoverageJson = JsonSerializer.Serialize(pageWithIndex.page.SourceCoverage ?? new WikiPageSourceCoverageDto(), ArtifactJsonOptions),
                    FilePaths = pageWithIndex.page.FilePaths?.ToArray(),
                    TokenCount = string.IsNullOrWhiteSpace(pageWithIndex.page.Content)
                        ? 0
                        : pageWithIndex.page.Content.Length / 4,
                    Status = "ready"
                });
            }

            await _db.Insertable(persistedPages).ExecuteCommandAsync(cancellationToken);

            var pageIdMapping = structure.Pages
                .Select((page, index) => new { page.Id, PersistedId = persistedPages[index].Id })
                .ToDictionary(item => item.Id, item => item.PersistedId, StringComparer.OrdinalIgnoreCase);

            foreach (var pageWithIndex in structure.Pages.Select((page, index) => new { page, index }))
            {
                if (!string.IsNullOrWhiteSpace(pageWithIndex.page.ParentId)
                    && pageIdMapping.TryGetValue(pageWithIndex.page.ParentId, out var parentPageId))
                {
                    persistedPages[pageWithIndex.index].ParentPageId = parentPageId;
                    persistedPages[pageWithIndex.index].Depth = CalculateDepth(pageWithIndex.page, structure.Pages, pageIdMapping);
                }
                else
                {
                    persistedPages[pageWithIndex.index].Depth = 0;
                }
            }

            await _db.Updateable(persistedPages).ExecuteCommandAsync(cancellationToken);

            var relations = BuildWikiPageRelations(wikiVersion.Id, structure, pageIdMapping);
            if (relations.Count > 0)
            {
                await _db.Insertable(relations).ExecuteCommandAsync(cancellationToken);
            }

            // 双写：Workspace 文件系统
            var wikiDir = _workspace.GetWikiDir(wikiVersion.Id);
            var pagesDir = Path.Combine(wikiDir, "pages");
            Directory.CreateDirectory(pagesDir);

            // 写入 structure.json
            var structureFilePath = Path.Combine(wikiDir, "structure.json");
            await File.WriteAllTextAsync(structureFilePath, structureJson, cancellationToken);
            wikiVersion.StructureFilePath = structureFilePath;

            // 写入每个页面内容
            for (var i = 0; i < persistedPages.Count; i++)
            {
                var page = persistedPages[i];
                var slug = ToSlug(page.Title);
                var pageFileName = $"{page.PageOrder:D4}_{slug}.md";
                var pageFilePath = Path.Combine(pagesDir, pageFileName);
                await File.WriteAllTextAsync(pageFilePath, page.ContentMarkdown ?? string.Empty, cancellationToken);
                page.ContentFilePath = pageFilePath;
            }

            // 写入 relations.json
            var relationsFilePath = Path.Combine(wikiDir, "relations.json");
            var relationsJson = JsonSerializer.Serialize(relations.Select(r => new
            {
                r.SourcePageId,
                r.TargetPageId,
                r.RelationType,
                r.MetadataJson
            }), PrettyJsonOptions);
            await File.WriteAllTextAsync(relationsFilePath, relationsJson, cancellationToken);

            wikiVersion.PageCount = persistedPages.Count;
            wikiVersion.TocDepth = Math.Max(1, structure.Sections.Count > 0 ? 2 : 1);
            wikiVersion.SummaryMarkdown = $"由任务 {task.Id} 生成，共 {persistedPages.Count} 个页面";
            wikiVersion.Status = wikiSpace.PublishedWikiVersionId is null ? "published" : "ready";
            wikiVersion.CompletedAt = DateTime.UtcNow;
            await _db.Updateable(wikiVersion).ExecuteCommandAsync(cancellationToken);

            if (wikiSpace.PublishedWikiVersionId is null)
            {
                wikiSpace.PublishedWikiVersionId = wikiVersion.Id;
                await _db.Updateable(wikiSpace).ExecuteCommandAsync(cancellationToken);
            }

            task.ResolvedRepositoryVersionId = repositoryVersion.Id;
            task.ResultWikiVersionId = wikiVersion.Id;
            task.UpdatedAt = DateTime.UtcNow;
            await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);

            await UpsertArtifactAsync(
                task,
                "relation_artifact",
                "relations",
                "persistence",
                0,
                JsonSerializer.Serialize(new
                {
                    wiki_version_id = wikiVersion.Id,
                    relation_count = relations.Count,
                    page_count = persistedPages.Count
                }, ArtifactJsonOptions),
                $"页面关系已写入，共 {relations.Count} 条",
                cancellationToken);

            await UpsertArtifactAsync(
                task,
                "render_artifact",
                "render-snapshot",
                "persistence",
                0,
                JsonSerializer.Serialize(new
                {
                    wiki_version_id = wikiVersion.Id,
                    title = structure.Title,
                    description = structure.Description,
                    page_count = persistedPages.Count,
                    pages = persistedPages.Select(p => new
                    {
                        page_id = p.Id,
                        p.PageOrder,
                        p.Title,
                        p.NavTitle,
                        p.PageType,
                        p.Importance,
                        p.ContentMarkdown,
                        p.OutlineJson,
                        p.SourceCoverageJson,
                        p.Summary
                    })
                }, ArtifactJsonOptions),
                $"渲染快照已生成，共 {persistedPages.Count} 个页面",
                cancellationToken);

            await _db.Ado.CommitTranAsync();
            return (repositoryVersion.Id, wikiVersion.Id, persistedPages);
        }
        catch
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <summary>
    /// 幂等写入事务内工件，并同步回写任务恢复锚点。
    /// </summary>
    private async Task UpsertArtifactAsync(
        TaskRecord task,
        string artifactType,
        string artifactKey,
        string stageName,
        int sequence,
        string payloadJson,
        string summary,
        CancellationToken cancellationToken)
    {
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant();
        var artifact = await _db.Queryable<TaskArtifact>()
            .FirstAsync(a => a.TaskId == task.Id
                && a.ArtifactType == artifactType
                && a.ArtifactKey == artifactKey);

        if (artifact is null)
        {
            artifact = new TaskArtifact
            {
                TaskId = task.Id,
                ArtifactType = artifactType,
                ArtifactKey = artifactKey,
                StageName = stageName,
                Sequence = sequence,
                PayloadJson = payloadJson
            };
            await _db.Insertable(artifact).ExecuteCommandAsync(cancellationToken);
        }

        artifact.StageName = stageName;
        artifact.Status = "completed";
        artifact.Sequence = sequence;
        artifact.ContentHash = contentHash;
        artifact.Summary = summary;
        artifact.PayloadJson = payloadJson;
        artifact.ErrorMessage = null;
        artifact.UpdatedAt = DateTime.UtcNow;

        await _db.Updateable(artifact).ExecuteCommandAsync(cancellationToken);

        task.LastArtifactId = artifact.Id;
        task.LastSuccessfulStage = stageName;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.Updateable(task).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 构建页面关系集合。
    /// 当前阶段落地 related_to、depends_on 与 parent 三类关系。
    /// </summary>
    private static List<WikiPageRelation> BuildWikiPageRelations(
        Guid wikiVersionId,
        WikiStructureDto structure,
        Dictionary<string, Guid> pageIdMapping)
    {
        var relations = new List<WikiPageRelation>();

        foreach (var page in structure.Pages)
        {
            if (!pageIdMapping.TryGetValue(page.Id, out var sourcePageId))
                continue;

            if (page.RelatedPages is not null)
            {
                foreach (var relatedId in page.RelatedPages)
                {
                    if (!pageIdMapping.TryGetValue(relatedId, out var targetPageId))
                        continue;

                    relations.Add(new WikiPageRelation
                    {
                        WikiVersionId = wikiVersionId,
                        SourcePageId = sourcePageId,
                        TargetPageId = targetPageId,
                        RelationType = "related_to",
                        MetadataJson = JsonSerializer.Serialize(new
                        {
                            source_page_ref = page.Id,
                            target_page_ref = relatedId
                        }, ArtifactJsonOptions)
                    });
                }
            }

            if (page.PrerequisitePages is not null)
            {
                foreach (var prerequisiteId in page.PrerequisitePages)
                {
                    if (!pageIdMapping.TryGetValue(prerequisiteId, out var targetPageId))
                        continue;

                    relations.Add(new WikiPageRelation
                    {
                        WikiVersionId = wikiVersionId,
                        SourcePageId = sourcePageId,
                        TargetPageId = targetPageId,
                        RelationType = "depends_on",
                        MetadataJson = JsonSerializer.Serialize(new
                        {
                            source_page_ref = page.Id,
                            prerequisite_page_ref = prerequisiteId
                        }, ArtifactJsonOptions)
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(page.ParentId) && pageIdMapping.TryGetValue(page.ParentId, out var parentPageId))
            {
                relations.Add(new WikiPageRelation
                {
                    WikiVersionId = wikiVersionId,
                    SourcePageId = sourcePageId,
                    TargetPageId = parentPageId,
                    RelationType = "parent",
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        source_page_ref = page.Id,
                        parent_page_ref = page.ParentId
                    }, ArtifactJsonOptions)
                });
            }
        }

        return relations;
    }

    /// <summary>
    /// 将页面标题转换为文件系统安全的 slug。
    /// </summary>
    private static string ToSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "untitled";
        var slug = NonAlphaRegex().Replace(title.ToLowerInvariant(), "-");
        slug = MultiHyphenRegex().Replace(slug, "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "untitled" : slug;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[^a-z0-9一-鿿]+")]
    private static partial Regex NonAlphaRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiHyphenRegex();

    /// <summary>
    /// 计算页面深度。
    /// </summary>
    private static int CalculateDepth(
        WikiPageDto page,
        IReadOnlyList<WikiPageDto> allPages,
        IReadOnlyDictionary<string, Guid> pageIdMapping)
    {
        var lookup = allPages.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var current = page;
        var depth = 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrWhiteSpace(current.ParentId)
            && pageIdMapping.ContainsKey(current.ParentId)
            && lookup.TryGetValue(current.ParentId, out var parent)
            && visited.Add(parent.Id))
        {
            depth++;
            current = parent;
        }

        return depth;
    }
}
