using System.Text.Json.Serialization;
using Heimdall.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/repositories")]
public class RepositoriesController : ControllerBase
{
    private readonly IRepositoryService _repoService;
    private readonly IDualVectorSearchService _dualSearch;
    private readonly IVersionDiscoveryService _versionDiscovery;

    public RepositoriesController(
        IRepositoryService repoService,
        IDualVectorSearchService dualSearch,
        IVersionDiscoveryService versionDiscovery)
    {
        _repoService = repoService;
        _dualSearch = dualSearch;
        _versionDiscovery = versionDiscovery;
    }

    /// <summary>
    /// POST /api/repositories/import — 根据仓库 URL 导入仓库，返回 repositoryId
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepoUrl))
            return BadRequest(new { error = "repo_url 是必填字段" });

        try
        {
            var repository = await _repoService.ImportAsync(request.RepoUrl);
            return Ok(new
            {
                repository_id = repository.Id.ToString(),
                display_name = repository.DisplayName,
                provider_type = repository.ProviderType,
                message = "仓库已就绪"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"导入仓库失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// GET /api/repositories — 获取所有仓库列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var repos = await _repoService.GetAllAsync();
        var result = repos.Select(r => new
        {
            repository_id = r.Id.ToString(),
            display_name = r.DisplayName,
            owner = r.Owner,
            repo_name = r.RepoName,
            provider_type = r.ProviderType,
            repo_type = r.RepoType,
            repo_url = r.RepoUrl,
            default_branch = r.DefaultBranch,
            default_language = r.DefaultLanguage,
            is_archived = r.IsArchived,
            created_at = r.CreatedAt,
            updated_at = r.UpdatedAt
        });
        return Ok(result);
    }

    /// <summary>
    /// GET /api/repositories/{repositoryId} — 获取单个仓库详情
    /// </summary>
    [HttpGet("{repositoryId:guid}")]
    public async Task<IActionResult> GetById(Guid repositoryId)
    {
        var repo = await _repoService.GetByIdAsync(repositoryId);
        if (repo is null)
            return NotFound(new { error = "仓库不存在" });

        return Ok(new
        {
            repository_id = repo.Id.ToString(),
            display_name = repo.DisplayName,
            owner = repo.Owner,
            repo_name = repo.RepoName,
            provider_type = repo.ProviderType,
            provider_repository_key = repo.ProviderRepositoryKey,
            repo_type = repo.RepoType,
            repo_url = repo.RepoUrl,
            clone_url = repo.CloneUrl,
            default_branch = repo.DefaultBranch,
            default_language = repo.DefaultLanguage,
            description = repo.Description,
            is_archived = repo.IsArchived,
            created_at = repo.CreatedAt,
            updated_at = repo.UpdatedAt
        });
    }

    /// <summary>
    /// PATCH /api/repositories/{repositoryId} — 更新仓库元数据
    /// </summary>
    [HttpPatch("{repositoryId:guid}")]
    public async Task<IActionResult> Update(Guid repositoryId, [FromBody] UpdateRepositoryRequest request)
    {
        var repo = await _repoService.UpdateAsync(repositoryId, entity =>
        {
            if (request.DisplayName is not null) entity.DisplayName = request.DisplayName;
            if (request.DefaultBranch is not null) entity.DefaultBranch = request.DefaultBranch;
            if (request.Description is not null) entity.Description = request.Description;
            if (request.IsArchived.HasValue) entity.IsArchived = request.IsArchived.Value;
        });

        if (repo is null) return NotFound(new { error = "仓库不存在" });
        return Ok(new { repository_id = repo.Id.ToString(), message = "仓库已更新" });
    }

    /// <summary>
    /// DELETE /api/repositories/{repositoryId} — 删除仓库及其关联数据
    /// </summary>
    [HttpDelete("{repositoryId:guid}")]
    public async Task<IActionResult> Delete(Guid repositoryId)
    {
        var deleted = await _repoService.DeleteAsync(repositoryId);
        if (!deleted) return NotFound(new { error = "仓库不存在" });
        return Ok(new { message = "仓库已删除" });
    }

    /// <summary>
    /// DELETE /api/repositories/{repositoryId}/vectors/code — 删除指定仓库版本的代码向量
    /// </summary>
    [HttpDelete("{repositoryId:guid}/vectors/code")]
    public async Task<IActionResult> DeleteCodeVectors(Guid repositoryId, [FromQuery] string branch = "main")
    {
        var version = await _versionDiscovery.GetLatestVersionAsync(repositoryId, branch);
        if (version is null)
            return NotFound(new { error = "未找到仓库版本" });

        var count = await _dualSearch.DeleteCodeVectorsAsync(version.Id);
        return Ok(new { message = $"已删除 {count} 条代码向量", deleted_count = count });
    }

    /// <summary>
    /// DELETE /api/repositories/{repositoryId}/vectors/wiki — 删除指定 Wiki 版本的向量
    /// </summary>
    [HttpDelete("{repositoryId:guid}/vectors/wiki")]
    public async Task<IActionResult> DeleteWikiVectors(Guid repositoryId,
        [FromQuery] string wikiVersionId = "")
    {
        if (string.IsNullOrWhiteSpace(wikiVersionId) || !Guid.TryParse(wikiVersionId, out var wvId))
            return BadRequest(new { error = "需要提供有效的 wikiVersionId" });

        var count = await _dualSearch.DeleteWikiVectorsAsync(wvId);
        return Ok(new { message = $"已删除 {count} 条 Wiki 向量", deleted_count = count });
    }
}

public class ImportRequest
{
    [JsonPropertyName("repo_url")]
    public string RepoUrl { get; set; } = string.Empty;
}

public class UpdateRepositoryRequest
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
    [JsonPropertyName("default_branch")]
    public string? DefaultBranch { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("is_archived")]
    public bool? IsArchived { get; set; }
}
