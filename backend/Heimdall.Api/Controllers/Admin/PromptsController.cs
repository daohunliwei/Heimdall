using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers.Admin;

/// <summary>
/// 已废弃 — V5 已将提示词管理迁移至 PromptTemplatesController。
/// 所有请求返回 410 Gone 并指示新端点。
/// </summary>
[ApiController]
[Route("admin/prompts")]
[Authorize(Policy = "AdminOnly")]
public class PromptsController : ControllerBase
{
    private const string MigrationMessage =
        "此 API 已废弃 (V5)。提示词管理已迁移至 /api/admin/prompt-templates，支持 CRUD、版本历史、回滚和仓库级覆盖。";

    [HttpGet]
    public IActionResult GetAll() => StatusCode(410, new { error = MigrationMessage });

    [HttpPost]
    public IActionResult Create() => StatusCode(410, new { error = MigrationMessage });

    [HttpPut("{id}")]
    public IActionResult Update() => StatusCode(410, new { error = MigrationMessage });

    [HttpDelete("{id}")]
    public IActionResult Delete() => StatusCode(410, new { error = MigrationMessage });

    [HttpGet("repository/{repoId}")]
    public IActionResult GetRepoOverrides() => StatusCode(410, new { error = MigrationMessage });

    [HttpPut("repository/{repoId}")]
    public IActionResult UpdateRepoOverrides() => StatusCode(410, new { error = MigrationMessage });
}
