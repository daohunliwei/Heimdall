using System.ComponentModel.DataAnnotations;
using Heimdall.Core.Entities;
using Heimdall.Core.Services.Prompt;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("admin/prompt-templates")]
public class PromptTemplatesController : ControllerBase
{
    private readonly PromptManagementService _promptService;

    public PromptTemplatesController(PromptManagementService promptService)
    {
        _promptService = promptService;
    }

    // ── 模板 CRUD ──

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await _promptService.GetAllAsync();
        return Ok(templates.Select(MapToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var template = await _promptService.GetByIdAsync(id);
        if (template is null) return NotFound();
        return Ok(MapToDto(template));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePromptTemplateRequest request)
    {
        var template = new PromptTemplate
        {
            Slug = request.Slug,
            Name = request.Name,
            Category = request.Category,
            SubCategory = request.SubCategory,
            Priority = request.Priority,
            ApplicableProviders = request.ApplicableProviders,
            TemplateContent = request.ContentTemplate,
            IsSystem = false
        };
        var created = await _promptService.CreateAsync(template);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePromptTemplateRequest request)
    {
        var updated = await _promptService.UpdateAsync(id, request.ContentTemplate);
        if (updated is null) return NotFound();
        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _promptService.DeleteAsync(id);
        if (!deleted) return Forbid("系统模板不可删除，可通过覆写进行定制");
        return NoContent();
    }

    // ── 版本历史 ──

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var history = await _promptService.GetHistoryAsync(id);
        return Ok(history.Select(h => new
        {
            h.Id,
            h.Version,
            h.TemplateContent,
            h.ChangedAt
        }));
    }

    [HttpPost("{id:guid}/rollback/{version:int}")]
    public async Task<IActionResult> Rollback(Guid id, int version)
    {
        var rolled = await _promptService.RollbackAsync(id, version);
        if (rolled is null) return NotFound();
        return Ok(MapToDto(rolled));
    }

    // ── 覆写管理 ──

    [HttpGet("overrides")]
    public async Task<IActionResult> GetOverrides([FromQuery] Guid repositoryId)
    {
        var overrides = await _promptService.GetOverridesAsync(repositoryId);
        return Ok(overrides.Select(o => new
        {
            o.Id,
            o.RepositoryId,
            o.PromptTemplateId,
            o.OverrideContent,
            o.Strategy,
            o.Priority,
            o.IsEnabled
        }));
    }

    [HttpPost("overrides")]
    public async Task<IActionResult> SaveOverride([FromBody] SaveOverrideRequest request)
    {
        var result = await _promptService.SaveOverrideAsync(
            request.RepositoryId,
            request.TemplateId,
            request.ContentOverride,
            request.Strategy ?? "override",
            request.Priority ?? 0);
        return Ok(new
        {
            result.Id,
            result.RepositoryId,
            result.PromptTemplateId,
            result.OverrideContent,
            result.Strategy,
            result.Priority,
            result.IsEnabled
        });
    }

    [HttpDelete("overrides/{overrideId:guid}")]
    public async Task<IActionResult> DeleteOverride(Guid overrideId)
    {
        await _promptService.DeleteOverrideAsync(overrideId);
        return NoContent();
    }

    // ── 运行时预览 ──

    [HttpGet("resolve/{slug}")]
    public async Task<IActionResult> ResolveTemplate(
        string slug,
        [FromQuery] Guid? repositoryId)
    {
        var result = await _promptService.ResolveTemplateAsync(slug, repositoryId);
        if (result is null) return NotFound();
        return Content(result, "text/plain; charset=utf-8");
    }

    // ── 类别查询 (V5) ──

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var templates = await _promptService.GetAllAsync();
        var categories = templates
            .Where(t => t.IsActive)
            .GroupBy(t => t.Category)
            .Select(g => new
            {
                category = g.Key,
                subCategories = g.Where(t => t.SubCategory != null)
                    .Select(t => t.SubCategory!)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList()
            })
            .OrderBy(c => c.category)
            .ToList();
        return Ok(categories);
    }

    // ── DTO 映射 ──

    private static object MapToDto(PromptTemplate t) => new
    {
        t.Id,
        t.Slug,
        t.Name,
        Category = t.Category,
        SubCategory = t.SubCategory,
        Priority = t.Priority,
        ApplicableProviders = t.ApplicableProviders,
        t.TemplateContent,
        t.IsSystem,
        t.IsActive,
        t.Version,
        t.CreatedAt,
        t.UpdatedAt
    };
}

// ── 请求 DTO ──

public class CreatePromptTemplateRequest
{
    [Required] public string Slug { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public int Priority { get; set; }
    public string[]? ApplicableProviders { get; set; }
    [Required] public string ContentTemplate { get; set; } = string.Empty;
}

public class UpdatePromptTemplateRequest
{
    [Required] public string ContentTemplate { get; set; } = string.Empty;
}

public class SaveOverrideRequest
{
    [Required] public Guid RepositoryId { get; set; }
    [Required] public Guid TemplateId { get; set; }
    [Required] public string ContentOverride { get; set; } = string.Empty;
    public string? Strategy { get; set; } = "override";
    public int? Priority { get; set; }
}
