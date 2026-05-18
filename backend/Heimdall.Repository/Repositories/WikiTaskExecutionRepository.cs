using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

/// <summary>
/// Wiki 任务执行仓储实现。
/// 该实现负责在单一事务中完成 Wiki 任务主链路的核心落库。
/// </summary>
public sealed class WikiTaskExecutionRepository : IWikiTaskExecutionRepository
{
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _context;

    /// <summary>
    /// 初始化 Wiki 任务执行仓储。
    /// </summary>
    public WikiTaskExecutionRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 在同一事务中持久化 Wiki 主数据、版本、页面、关系与渲染快照。
    /// </summary>
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
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var repositoryId = task.RepositoryId
            ?? throw new InvalidOperationException($"任务缺少 RepositoryId：{task.Id}");

        var repository = await _context.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken)
            ?? throw new InvalidOperationException($"仓库不存在：{repositoryId}");

        // V4：旧 Wiki 实体已移除，Wiki 空间和版本直接创建，不依赖 Wiki 主记录
        RepositoryVersion? repositoryVersion = null;
        if (task.ResolvedRepositoryVersionId.HasValue)
        {
            repositoryVersion = await _context.RepositoryVersions
                .FirstOrDefaultAsync(v => v.Id == task.ResolvedRepositoryVersionId.Value, cancellationToken);
        }

        repositoryVersion ??= await _context.RepositoryVersions
            .Where(v => v.RepositoryId == repositoryId && v.BranchName == branch && v.IsLatestOnBranch)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

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
            _context.RepositoryVersions.Add(repositoryVersion);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var wikiSpace = await _context.WikiSpaces
            .FirstOrDefaultAsync(s => s.RepositoryId == repositoryId
                && s.Language == language
                && s.ViewType == "default", cancellationToken);
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
            _context.WikiSpaces.Add(wikiSpace);
            await _context.SaveChangesAsync(cancellationToken);
        }

        WikiVersion? wikiVersion = null;
        if (task.ResultWikiVersionId.HasValue)
        {
            wikiVersion = await _context.WikiVersions
                .FirstOrDefaultAsync(v => v.Id == task.ResultWikiVersionId.Value, cancellationToken);
        }

        if (wikiVersion is null)
        {
            var versionNo = await _context.WikiVersions.CountAsync(v => v.WikiSpaceId == wikiSpace.Id, cancellationToken) + 1;
            wikiVersion = new WikiVersion
            {
                WikiSpaceId = wikiSpace.Id,
                RepositoryVersionId = repositoryVersion.Id,
                VersionNo = versionNo,
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
            _context.WikiVersions.Add(wikiVersion);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            wikiVersion.RepositoryVersionId = repositoryVersion.Id;
            wikiVersion.GenerationMode = task.ForceRefresh ? "rebuild" : "latest";
            wikiVersion.GenerationProfile = generationProfile;
            wikiVersion.Status = "generating";
            wikiVersion.StructureJson = structureJson;
            wikiVersion.CompletedAt = null;
            await _context.SaveChangesAsync(cancellationToken);
        }

        await _context.WikiPageRelations
            .Where(r => r.WikiVersionId == wikiVersion.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.WikiPages
            .Where(p => p.WikiVersionId == wikiVersion.Id)
            .ExecuteDeleteAsync(cancellationToken);

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

        _context.WikiPages.AddRange(persistedPages);
        await _context.SaveChangesAsync(cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

        var relations = BuildWikiPageRelations(wikiVersion.Id, structure, pageIdMapping);
        if (relations.Count > 0)
        {
            _context.WikiPageRelations.AddRange(relations);
            await _context.SaveChangesAsync(cancellationToken);
        }

        wikiVersion.PageCount = persistedPages.Count;
        wikiVersion.TocDepth = Math.Max(1, structure.Sections.Count > 0 ? 2 : 1);
        wikiVersion.SummaryMarkdown = $"由任务 {task.Id} 生成，共 {persistedPages.Count} 个页面";
        wikiVersion.Status = wikiSpace.PublishedWikiVersionId is null ? "published" : "ready";
        wikiVersion.CompletedAt = DateTime.UtcNow;

        if (wikiSpace.PublishedWikiVersionId is null)
            wikiSpace.PublishedWikiVersionId = wikiVersion.Id;

        task.ResolvedRepositoryVersionId = repositoryVersion.Id;
        task.ResultWikiVersionId = wikiVersion.Id;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

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

        await transaction.CommitAsync(cancellationToken);
        return (repositoryVersion.Id, wikiVersion.Id, persistedPages);
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
        var artifact = await _context.TaskArtifacts
            .FirstOrDefaultAsync(a => a.TaskId == task.Id
                && a.ArtifactType == artifactType
                && a.ArtifactKey == artifactKey, cancellationToken);

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
            _context.TaskArtifacts.Add(artifact);
        }

        artifact.StageName = stageName;
        artifact.Status = "completed";
        artifact.Sequence = sequence;
        artifact.ContentHash = contentHash;
        artifact.Summary = summary;
        artifact.PayloadJson = payloadJson;
        artifact.ErrorMessage = null;
        artifact.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        task.LastArtifactId = artifact.Id;
        task.LastSuccessfulStage = stageName;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
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
