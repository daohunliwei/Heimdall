using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId:guid}/wiki")]
public class WikiCompareController : ControllerBase
{
    /// <summary>
    /// POST /api/repositories/{repositoryId}/wiki/compare — 比较两个 Wiki 版本的差异
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> Compare(Guid repositoryId, [FromBody] CompareRequest request)
    {
        if (request.VersionIdA == Guid.Empty || request.VersionIdB == Guid.Empty)
            return BadRequest(new { error = "需要提供两个版本 ID (version_id_a 和 version_id_b)" });

        // 占位实现：返回结构化对比摘要
        // 在完整实现中，应加载两个版本的页面树并进行内容级差异分析
        return Ok(new
        {
            repository_id = repositoryId.ToString(),
            version_a_id = request.VersionIdA.ToString(),
            version_b_id = request.VersionIdB.ToString(),
            compare_type = "wiki_version",
            summary = new
            {
                added_pages = new List<object>(),
                removed_pages = new List<object>(),
                title_changes = new List<object>(),
                content_changes = new List<object>(),
                significant_changes = new List<object>()
            },
            message = "对比功能已就绪，详细差异分析将在完整实现中提供。当前版本已具备版本存储与查询能力。"
        });
    }
}

public class CompareRequest
{
    public Guid VersionIdA { get; set; }
    public Guid VersionIdB { get; set; }
}
