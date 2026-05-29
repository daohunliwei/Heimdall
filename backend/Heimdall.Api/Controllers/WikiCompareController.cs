using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId:guid}/wiki")]
public class WikiCompareController : ControllerBase
{
    private readonly IWikiVersionRepository _versionRepo;
    private readonly IWikiPageRepository _pageRepo;
    private readonly IWikiSpaceRepository _spaceRepo;
    private readonly IRepositoryConfigRepository _repoRepo;
    private readonly WorkspaceService _workspace;
    private readonly ILogger<WikiCompareController> _logger;

    public WikiCompareController(
        IWikiVersionRepository versionRepo,
        IWikiPageRepository pageRepo,
        IWikiSpaceRepository spaceRepo,
        IRepositoryConfigRepository repoRepo,
        WorkspaceService workspace,
        ILogger<WikiCompareController> logger)
    {
        _versionRepo = versionRepo;
        _pageRepo = pageRepo;
        _spaceRepo = spaceRepo;
        _repoRepo = repoRepo;
        _workspace = workspace;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/repositories/{repositoryId}/wiki/compare — 比较两个 Wiki 版本的差异
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> Compare(Guid repositoryId, [FromBody] CompareRequest request)
    {
        if (request.VersionIdA == Guid.Empty || request.VersionIdB == Guid.Empty)
            return BadRequest(new { error = "需要提供两个版本 ID (version_id_a 和 version_id_b)" });

        var versionA = await _versionRepo.GetByIdAsync(request.VersionIdA);
        var versionB = await _versionRepo.GetByIdAsync(request.VersionIdB);

        if (versionA is null || versionB is null)
            return NotFound(new { error = "一个或多个版本不存在" });

        // 获取两个版本的页面
        var pagesByVersion = new Dictionary<Guid, List<Core.Entities.WikiPage>>();
        foreach (var versionId in new[] { request.VersionIdA, request.VersionIdB })
        {
            var version = versionId == request.VersionIdA ? versionA : versionB;
            var space = await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "zh", "default")
                        ?? await _spaceRepo.GetByRepoLangViewAsync(repositoryId, "en", "default");

            if (space is not null && version is not null && version.WikiSpaceId == space.Id)
            {
                pagesByVersion[versionId] = await _pageRepo.GetByWikiVersionIdAsync(versionId);
            }
        }

        var pagesA = pagesByVersion.GetValueOrDefault(request.VersionIdA, new());
        var pagesB = pagesByVersion.GetValueOrDefault(request.VersionIdB, new());

        var titlesA = pagesA.Select(p => p.Title).ToHashSet();
        var titlesB = pagesB.Select(p => p.Title).ToHashSet();

        // 新增页面：在 B 中存在但在 A 中不存在
        var addedPages = titlesB.Except(titlesA).Select(title => new
        {
            title,
            page_id = pagesB.First(p => p.Title == title).Id.ToString()
        }).ToList();

        // 删除页面：在 A 中存在但在 B 中不存在
        var removedPages = titlesA.Except(titlesB).Select(title => new
        {
            title,
            page_id = pagesA.First(p => p.Title == title).Id.ToString()
        }).ToList();

        // 标题变化（通过页面 ID 匹配）
        var pageDictA = pagesA.ToDictionary(p => p.Id);
        var pageDictB = pagesB.ToDictionary(p => p.Id);
        var commonIds = pageDictA.Keys.Intersect(pageDictB.Keys).ToHashSet();

        var titleChanges = new List<object>();
        foreach (var id in commonIds)
        {
            if (pageDictA[id].Title != pageDictB[id].Title)
            {
                titleChanges.Add(new
                {
                    page_id = id.ToString(),
                    old_title = pageDictA[id].Title,
                    new_title = pageDictB[id].Title
                });
            }
        }

        // 内容变化：比较内容哈希
        var contentChanges = new List<object>();
        var significantChanges = new List<object>();
        foreach (var id in commonIds)
        {
            var contentA = ResolvePageContent(pageDictA[id]);
            var contentB = ResolvePageContent(pageDictB[id]);

            if (contentA != contentB)
            {
                var hashA = ComputeHash(contentA);
                var hashB = ComputeHash(contentB);
                var sizeDiff = contentB.Length - contentA.Length;
                var isSignificant = Math.Abs(sizeDiff) > 500 || contentA.Length == 0 || contentB.Length == 0;

                var change = new
                {
                    page_id = id.ToString(),
                    page_title = pageDictB[id].Title,
                    old_hash = hashA,
                    new_hash = hashB,
                    size_diff = sizeDiff,
                    old_size = contentA.Length,
                    new_size = contentB.Length
                };

                contentChanges.Add(change);

                if (isSignificant)
                {
                    significantChanges.Add(change);
                }
            }
        }

        return Ok(new
        {
            repository_id = repositoryId.ToString(),
            version_a = new
            {
                id = versionA.Id.ToString(),
                version_no = versionA.VersionNo,
                page_count = versionA.PageCount,
                created_at = versionA.CreatedAt
            },
            version_b = new
            {
                id = versionB.Id.ToString(),
                version_no = versionB.VersionNo,
                page_count = versionB.PageCount,
                created_at = versionB.CreatedAt
            },
            compare_type = "wiki_version",
            summary = new
            {
                added_pages = addedPages,
                removed_pages = removedPages,
                title_changes = titleChanges,
                content_changes = contentChanges,
                significant_changes = significantChanges,
                total_added = addedPages.Count,
                total_removed = removedPages.Count,
                total_title_changes = titleChanges.Count,
                total_content_changes = contentChanges.Count,
                total_significant_changes = significantChanges.Count
            }
        });
    }

    private static string ResolvePageContent(Heimdall.Core.Entities.WikiPage page)
    {
        if (!string.IsNullOrEmpty(page.ContentFilePath) && System.IO.File.Exists(page.ContentFilePath))
        {
            return System.IO.File.ReadAllText(page.ContentFilePath);
        }
        return page.ContentMarkdown ?? "";
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..12];
    }
}

public class CompareRequest
{
    [JsonPropertyName("version_id_a")]
    public Guid VersionIdA { get; set; }

    [JsonPropertyName("version_id_b")]
    public Guid VersionIdB { get; set; }
}
