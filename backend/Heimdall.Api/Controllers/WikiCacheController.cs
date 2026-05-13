using Heimdall.Api.Models;
using Heimdall.Api.Services.Auth;
using Heimdall.Api.Services.Cache;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供 Wiki 缓存的查询、保存与删除接口。
/// </summary>
[ApiController]
[Route("api/wiki_cache")]
public sealed class WikiCacheController : ControllerBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly WikiCacheService _wikiCacheService;

    /// <summary>
    /// 初始化缓存控制器。
    /// </summary>
    public WikiCacheController(AuthorizationService authorizationService, WikiCacheService wikiCacheService)
    {
        _authorizationService = authorizationService;
        _wikiCacheService = wikiCacheService;
    }

    /// <summary>
    /// 获取指定仓库的 Wiki 缓存。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<WikiCacheData?>> GetWikiCacheAsync(
        [FromQuery] string owner,
        [FromQuery] string repo,
        [FromQuery(Name = "repo_type")] string repoType,
        [FromQuery] string language)
    {
        var cacheData = await _wikiCacheService.GetAsync(owner, repo, repoType, language);
        return Ok(cacheData);
    }

    /// <summary>
    /// 保存 Wiki 缓存。
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> SaveWikiCacheAsync([FromBody] WikiCacheSaveRequest request)
    {
        await _wikiCacheService.SaveAsync(request);
        return Ok(new { message = "缓存已保存" });
    }

    /// <summary>
    /// 删除指定仓库的 Wiki 缓存。
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> DeleteWikiCacheAsync(
        [FromQuery] string owner,
        [FromQuery] string repo,
        [FromQuery(Name = "repo_type")] string repoType,
        [FromQuery] string language,
        [FromQuery(Name = "authorization_code")] string? authorizationCode)
    {
        try
        {
            _authorizationService.EnsureAuthorized(authorizationCode);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }

        var deleted = await _wikiCacheService.DeleteAsync(owner, repo, repoType, language);
        if (!deleted)
        {
            return NotFound(new { error = "缓存不存在。" });
        }

        return Ok(new { message = "缓存已删除" });
    }
}
