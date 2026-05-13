using Heimdall.Api.Models;
using Heimdall.Api.Services.Repository;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供本地仓库结构读取接口。
/// </summary>
[ApiController]
[Route("local_repo")]
public sealed class RepositoryController : ControllerBase
{
    private readonly RepositoryAccessService _repositoryAccessService;

    /// <summary>
    /// 初始化仓库控制器。
    /// </summary>
    public RepositoryController(RepositoryAccessService repositoryAccessService)
    {
        _repositoryAccessService = repositoryAccessService;
    }

    /// <summary>
    /// 获取本地仓库目录结构与 README 内容。
    /// </summary>
    [HttpGet("structure")]
    public ActionResult<LocalRepoStructureResponse> GetStructure([FromQuery] string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "缺少 path 参数。" });
        }

        if (!Directory.Exists(path))
        {
            return NotFound(new { error = $"目录不存在：{path}" });
        }

        return Ok(_repositoryAccessService.GetLocalStructure(path));
    }
}
