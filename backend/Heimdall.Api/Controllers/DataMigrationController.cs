using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Repository.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Api.Controllers;

/// <summary>数据回填控制器 — 将旧数据迁移到 V2 版本模型</summary>
[ApiController]
[Route("api/admin/migration")]
public class DataMigrationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<DataMigrationController> _logger;

    public DataMigrationController(AppDbContext db, ILogger<DataMigrationController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>POST /api/admin/migration/backfill — 执行 V2 数据回填</summary>
    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill(CancellationToken cancellationToken)
    {
        var report = new MigrationReport();

        try
        {
            report.WikiSpacesCreated = await BackfillWikiSpacesAsync(cancellationToken);
            report.RepositoriesProcessed = await _db.Repositories.CountAsync(cancellationToken);
            report.RepositoryVersionsCreated = await BackfillRepositoryVersionsAsync(cancellationToken);
            report.WikiVersionsCreated = await BackfillWikiVersionsAsync(cancellationToken);
            report.WikiPagesUpdated = await BackfillWikiPagesAsync(cancellationToken);
            await BackfillPublishedVersionsAsync(cancellationToken);

            _logger.LogInformation("V2 数据回填完成：仓库 {Repos}，空间 +{Spaces}，版本 +{Versions}，Wiki版本 +{WikiVersions}，页面更新 {Pages}",
                report.RepositoriesProcessed, report.WikiSpacesCreated,
                report.RepositoryVersionsCreated, report.WikiVersionsCreated, report.WikiPagesUpdated);

            return Ok(new
            {
                success = true,
                report = new
                {
                    repositories_processed = report.RepositoriesProcessed,
                    wiki_spaces_created = report.WikiSpacesCreated,
                    repository_versions_created = report.RepositoryVersionsCreated,
                    wiki_versions_created = report.WikiVersionsCreated,
                    wiki_pages_updated = report.WikiPagesUpdated,
                    errors = report.Errors,
                    warnings = report.Warnings
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据回填失败");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>GET /api/admin/migration/status — 查看 V2 数据迁移状态</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var totalRepos = await _db.Repositories.CountAsync(cancellationToken);
        var totalSpaces = await _db.WikiSpaces.CountAsync(cancellationToken);
        var totalRepoVersions = await _db.RepositoryVersions.CountAsync(cancellationToken);
        var totalWikiVersions = await _db.WikiVersions.CountAsync(cancellationToken);
        var totalWikiPages = await _db.WikiPages.CountAsync(cancellationToken);
        var pagesWithVersion = await _db.WikiPages.CountAsync(p => p.WikiVersionId != null, cancellationToken);
        var totalRelations = await _db.WikiPageRelations.CountAsync(cancellationToken);

        return Ok(new
        {
            total_repositories = totalRepos,
            total_wiki_spaces = totalSpaces,
            total_repository_versions = totalRepoVersions,
            total_wiki_versions = totalWikiVersions,
            total_wiki_pages = totalWikiPages,
            pages_with_version_id = pagesWithVersion,
            total_page_relations = totalRelations,
            migration_complete = totalSpaces >= totalRepos && totalRepoVersions >= totalRepos
        });
    }

    private async Task<int> BackfillWikiSpacesAsync(CancellationToken ct)
    {
        var count = 0;
        var repos = await _db.Repositories.ToListAsync(ct);
        foreach (var repo in repos)
        {
            var exists = await _db.WikiSpaces.AnyAsync(s =>
                s.RepositoryId == repo.Id && s.Language == "zh" && s.ViewType == "default", ct);
            if (exists) continue;

            _db.WikiSpaces.Add(new WikiSpace
            {
                RepositoryId = repo.Id, Language = "zh", ViewType = "default",
                Title = $"{repo.DisplayName} Wiki",
                Description = $"为 {repo.DisplayName} 自动生成的 Wiki 空间"
            });
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    private async Task<int> BackfillRepositoryVersionsAsync(CancellationToken ct)
    {
        var count = 0;
        var repos = await _db.Repositories.ToListAsync(ct);
        foreach (var repo in repos)
        {
            var exists = await _db.RepositoryVersions.AnyAsync(v =>
                v.RepositoryId == repo.Id && v.BranchName == (repo.DefaultBranch ?? "main"), ct);
            if (exists) continue;

            _db.RepositoryVersions.Add(new RepositoryVersion
            {
                RepositoryId = repo.Id, BranchName = repo.DefaultBranch ?? "main",
                CommitSha = "unknown", CommitTime = repo.CreatedAt,
                CommitAuthor = "system", CommitMessage = "初始版本（数据回填）",
                SourceStatus = "active", IsLatestOnBranch = true,
                VersionSourceConfidence = "unknown"
            });
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    private async Task<int> BackfillWikiVersionsAsync(CancellationToken ct)
    {
        var count = 0;
        var wikis = await _db.Wikis.Include(w => w.Pages).ToListAsync(ct);
        foreach (var wiki in wikis)
        {
            var space = await _db.WikiSpaces.FirstOrDefaultAsync(s =>
                s.RepositoryId == wiki.SourceRepositoryId && s.Language == "zh" && s.ViewType == "default", ct);
            if (space is null) continue;

            var repoVersion = await _db.RepositoryVersions
                .FirstOrDefaultAsync(v => v.RepositoryId == wiki.SourceRepositoryId
                    && v.BranchName == wiki.SourceBranch, ct)
                ?? await _db.RepositoryVersions.FirstOrDefaultAsync(v => v.RepositoryId == wiki.SourceRepositoryId, ct);
            if (repoVersion is null) continue;

            var exists = await _db.WikiVersions.AnyAsync(v =>
                v.WikiSpaceId == space.Id && v.RepositoryVersionId == repoVersion.Id, ct);
            if (exists) continue;

            _db.WikiVersions.Add(new WikiVersion
            {
                WikiSpaceId = space.Id, RepositoryVersionId = repoVersion.Id,
                VersionNo = 1, GenerationMode = "rebuild", GenerationProfile = "comprehensive",
                Status = "ready", PageCount = wiki.Pages?.Count ?? 0, TocDepth = 1,
                SummaryMarkdown = wiki.Description, CompletedAt = wiki.UpdatedAt, CreatedAt = wiki.CreatedAt
            });
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    private async Task<int> BackfillWikiPagesAsync(CancellationToken ct)
    {
        var count = 0;
        var pages = await _db.WikiPages.Include(p => p.Wiki)
            .Where(p => p.WikiVersionId == null).ToListAsync(ct);

        foreach (var page in pages)
        {
            if (page.Wiki is null) continue;
            var space = await _db.WikiSpaces.FirstOrDefaultAsync(s =>
                s.RepositoryId == page.Wiki.SourceRepositoryId, ct);
            if (space is null) continue;
            var repoVersion = await _db.RepositoryVersions
                .FirstOrDefaultAsync(v => v.RepositoryId == page.Wiki.SourceRepositoryId, ct);
            if (repoVersion is null) continue;
            var wikiVersion = await _db.WikiVersions.FirstOrDefaultAsync(v =>
                v.WikiSpaceId == space.Id && v.RepositoryVersionId == repoVersion.Id, ct);
            if (wikiVersion is null) continue;

            page.WikiVersionId = wikiVersion.Id;
            if (string.IsNullOrEmpty(page.PageType)) page.PageType = "article";
            if (string.IsNullOrEmpty(page.Status)) page.Status = "ready";
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    private async Task BackfillPublishedVersionsAsync(CancellationToken ct)
    {
        var spaces = await _db.WikiSpaces.Where(s => s.PublishedWikiVersionId == null).ToListAsync(ct);
        foreach (var space in spaces)
        {
            var latestVersion = await _db.WikiVersions
                .Where(v => v.WikiSpaceId == space.Id && v.Status == "ready")
                .OrderByDescending(v => v.CreatedAt).FirstOrDefaultAsync(ct);

            if (latestVersion is not null)
            {
                space.PublishedWikiVersionId = latestVersion.Id;
                latestVersion.Status = "published";
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public class MigrationReport
    {
        public int RepositoriesProcessed { get; set; }
        public int WikiSpacesCreated { get; set; }
        public int RepositoryVersionsCreated { get; set; }
        public int WikiVersionsCreated { get; set; }
        public int WikiPagesUpdated { get; set; }
        public List<string> Errors { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
    }
}
