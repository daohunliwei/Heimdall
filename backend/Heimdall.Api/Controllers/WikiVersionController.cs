using System.Text.Json.Serialization;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId:guid}/wiki")]
public class WikiVersionController : ControllerBase
{
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IWikiVersionRepository _versionRepo;
    private readonly IWikiPageRepository _pageRepo;
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly IWikiTaskSubmissionService _wikiTaskSubmissionService;
    private readonly ILogger<WikiVersionController> _logger;

    public WikiVersionController(
        IWikiSpaceRepository spaceRepo,
        IWikiVersionRepository versionRepo,
        IWikiPageRepository pageRepo,
        IRepositoryConfigRepository repoRepo,
        IWikiTaskSubmissionService wikiTaskSubmissionService,
        ILogger<WikiVersionController> logger)
    {
        _spaceRepo = spaceRepo;
        _versionRepo = versionRepo;
        _pageRepo = pageRepo;
        _repoRepo = repoRepo;
        _wikiTaskSubmissionService = wikiTaskSubmissionService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/repositories/{repositoryId}/wiki/versions — 获取 Wiki 版本列表
    /// </summary>
    [HttpGet("versions")]
    public async Task<IActionResult> GetVersions(
        Guid repositoryId, [FromQuery] string language = "zh", [FromQuery] string viewType = "default")
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId);
        if (repo is null) return NotFound(new { error = "仓库不存在" });

        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, language, viewType);
        if (space is null) return Ok(new List<object>());

        var versions = await _versionRepo.GetBySpaceIdAsync(space.Id);
        var result = versions.OrderByDescending(v => v.VersionNo).Select(v => new
        {
            wiki_version_id = v.Id.ToString(),
            wiki_space_id = v.WikiSpaceId.ToString(),
            repository_version_id = v.RepositoryVersionId.ToString(),
            version_no = v.VersionNo,
            generation_mode = v.GenerationMode,
            generation_profile = v.GenerationProfile,
            status = v.Status,
            page_count = v.PageCount,
            toc_depth = v.TocDepth,
            summary_markdown = v.SummaryMarkdown,
            created_at = v.CreatedAt,
            completed_at = v.CompletedAt
        });

        return Ok(result);
    }

    /// <summary>
    /// GET /api/repositories/{repositoryId}/wiki/versions/{wikiVersionId} — 获取指定版本详情
    /// </summary>
    [HttpGet("versions/{wikiVersionId:guid}")]
    public async Task<IActionResult> GetVersionById(Guid repositoryId, Guid wikiVersionId)
    {
        var version = await _versionRepo.GetByIdAsync(wikiVersionId);
        if (version is null) return NotFound(new { error = "Wiki 版本不存在" });

        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "zh", "default")
                    ?? await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "en", "default");
        if (space is null || space.Id != version.WikiSpaceId)
            return NotFound(new { error = "版本不属于该仓库" });

        var versionPages = (await _pageRepo.GetByWikiVersionIdAsync(wikiVersionId)).Select(p => new
        {
            id = p.Id.ToString(),
            title = p.Title,
            page_type = p.PageType,
            importance = p.Importance,
            page_order = p.PageOrder,
            status = p.Status,
            token_count = p.TokenCount
        });

        return Ok(new
        {
            wiki_version_id = version.Id.ToString(),
            wiki_space_id = version.WikiSpaceId.ToString(),
            repository_version_id = version.RepositoryVersionId.ToString(),
            version_no = version.VersionNo,
            generation_mode = version.GenerationMode,
            generation_profile = version.GenerationProfile,
            status = version.Status,
            page_count = version.PageCount,
            toc_depth = version.TocDepth,
            summary_markdown = version.SummaryMarkdown,
            created_at = version.CreatedAt,
            completed_at = version.CompletedAt,
            pages = versionPages
        });
    }

    /// <summary>
    /// POST /api/repositories/{repositoryId}/wiki/refresh — 触发 Wiki 刷新
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(Guid repositoryId, [FromBody] WikiRefreshRequest request)
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId);
        if (repo is null) return NotFound(new { error = "仓库不存在" });

        var branch = !string.IsNullOrWhiteSpace(request.Branch) ? request.Branch : repo.DefaultBranch ?? "main";
        var strategy = !string.IsNullOrWhiteSpace(request.RefreshStrategy) ? request.RefreshStrategy : "latest";

        try
        {
            var result = await _wikiTaskSubmissionService.SubmitRefreshAsync(new WikiTaskSubmissionRequest
            {
                RepositoryId = repositoryId,
                Branch = branch,
                RefreshStrategy = strategy,
                ForceRefresh = request.ForceRefresh,
                Provider = request.Provider,
                Model = request.Model,
                Language = request.Language,
                GenerationProfile = request.GenerationProfile
            }, HttpContext.RequestAborted);

            return Ok(new
            {
                task_id = result.TaskId?.ToString(),
                repository_version_id = result.RepositoryVersionId?.ToString(),
                wiki_version_id = result.WikiVersionId?.ToString(),
                result_type = result.ResultType,
                change_status = result.ChangeStatus,
                status = result.TaskStatus,
                message = result.Message ?? result.ResultType switch
                {
                    "queued" => "刷新任务已排队",
                    "reused" => "复用已有版本",
                    _ => "刷新完成"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki 刷新失败 RepositoryId={RepoId}", repositoryId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/repositories/{repositoryId}/wiki/versions/{wikiVersionId}/publish — 发布指定版本
    /// </summary>
    [HttpPost("versions/{wikiVersionId:guid}/publish")]
    public async Task<IActionResult> PublishVersion(Guid repositoryId, Guid wikiVersionId)
    {
        var version = await _versionRepo.GetByIdAsync(wikiVersionId);
        if (version is null) return NotFound(new { error = "Wiki 版本不存在" });

        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "zh", "default")
                    ?? await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "en", "default");
        if (space is null || space.Id != version.WikiSpaceId)
            return NotFound(new { error = "Wiki 空间不属于该仓库或版本不匹配" });

        // 将当前发布版本标记为 ready
        if (space.PublishedWikiVersionId.HasValue && space.PublishedWikiVersionId != wikiVersionId)
        {
            var currentPublished = await _versionRepo.GetByIdAsync(space.PublishedWikiVersionId.Value);
            if (currentPublished is not null)
            {
                currentPublished.Status = "ready";
                await _versionRepo.UpdateAsync(currentPublished);
            }
        }

        // 设置新发布版本
        space.PublishedWikiVersionId = wikiVersionId;
        version.Status = "published";
        await _spaceRepo.UpdateAsync(space);
        await _versionRepo.UpdateAsync(version);

        _logger.LogInformation("Wiki 版本已发布 VersionId={VersionId} SpaceId={SpaceId}", wikiVersionId, space.Id);

        return Ok(new
        {
            wiki_version_id = wikiVersionId.ToString(),
            status = "published",
            message = "版本已发布"
        });
    }

    /// <summary>
    /// GET /api/repositories/{repositoryId}/wiki/published — 获取当前发布版本
    /// </summary>
    [HttpGet("published")]
    public async Task<IActionResult> GetPublished(Guid repositoryId, [FromQuery] string language = "zh")
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId);
        if (repo is null) return NotFound(new { error = "仓库不存在" });

        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, language, "default");
        if (space?.PublishedWikiVersionId is null)
            return NotFound(new { error = "没有已发布的版本" });

        var version = await _versionRepo.GetByIdAsync(space.PublishedWikiVersionId.Value);
        if (version is null) return NotFound(new { error = "发布版本不存在" });

        return Ok(new
        {
            wiki_version_id = version.Id.ToString(),
            version_no = version.VersionNo,
            status = version.Status,
            page_count = version.PageCount,
            created_at = version.CreatedAt,
            completed_at = version.CompletedAt
        });
    }

    /// <summary>
    /// GET /api/repositories/{repositoryId}/wiki/pages — 获取指定版本的页面内容
    /// </summary>
    [HttpGet("pages")]
    public async Task<IActionResult> GetPages(Guid repositoryId,
        [FromQuery] Guid? wikiVersionId = null,
        [FromQuery] string language = "zh",
        [FromQuery] string viewType = "default")
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId);
        if (repo is null) return NotFound(new { error = "仓库不存在" });

        var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, language, viewType);
        if (space is null) return NotFound(new { error = "Wiki 空间不存在" });

        var effectiveVersionId = wikiVersionId ?? space.PublishedWikiVersionId;
        if (effectiveVersionId is null)
        {
            var versions = await _versionRepo.GetBySpaceIdAsync(space.Id);
            var latest = versions.OrderByDescending(v => v.VersionNo).FirstOrDefault();
            effectiveVersionId = latest?.Id;
        }

        if (effectiveVersionId is null)
            return NotFound(new { error = "没有可用的版本" });

        var version = await _versionRepo.GetByIdAsync(effectiveVersionId.Value);
        if (version is null) return NotFound(new { error = "版本不存在" });

        var pages = await _pageRepo.GetByWikiVersionIdAsync(effectiveVersionId.Value);

        return Ok(pages.Select(p => new
        {
            id = p.Id.ToString(),
            title = p.Title,
            content = p.ContentMarkdown ?? "",
            page_type = p.PageType,
            importance = p.Importance,
            page_order = p.PageOrder,
            file_paths = p.FilePaths ?? Array.Empty<string>(),
            nav_title = p.NavTitle,
            parent_page_id = p.ParentPageId?.ToString(),
            depth = p.Depth,
            token_count = p.TokenCount,
            status = p.Status,
            created_at = p.CreatedAt
        }));
    }

}

public class WikiRefreshRequest
{
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    [JsonPropertyName("refresh_strategy")]
    public string? RefreshStrategy { get; set; }

    [JsonPropertyName("force_refresh")]
    public bool ForceRefresh { get; set; }

    [JsonPropertyName("generation_profile")]
    public string? GenerationProfile { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}
