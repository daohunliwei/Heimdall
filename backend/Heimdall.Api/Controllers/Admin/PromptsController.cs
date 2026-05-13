using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Services.Prompt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers.Admin;

[ApiController]
[Route("admin/prompts")]
[Authorize(Policy = "AdminOnly")]
public class PromptsController : ControllerBase
{
    private readonly PromptTemplateService _promptService;
    private readonly IPromptTemplateRepository _promptRepo;

    public PromptsController(PromptTemplateService promptService, IPromptTemplateRepository promptRepo)
    {
        _promptService = promptService;
        _promptRepo = promptRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await _promptRepo.GetAllAsync();
        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PromptTemplate template)
    {
        await _promptRepo.AddAsync(template);
        return Ok(template);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PromptTemplate template)
    {
        template.Id = id;
        await _promptRepo.UpdateAsync(template);
        return Ok(template);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _promptRepo.DeleteAsync(id);
        return Ok();
    }

    [HttpGet("repository/{repoId}")]
    public async Task<IActionResult> GetRepoOverrides(Guid repoId)
    {
        var templates = await _promptRepo.GetAllAsync();
        return Ok(templates.Where(t => t.ScopeValue == repoId.ToString()));
    }

    [HttpPut("repository/{repoId}")]
    public async Task<IActionResult> UpdateRepoOverrides(Guid repoId, [FromBody] List<PromptTemplate> overrides)
    {
        foreach (var item in overrides)
        {
            item.ScopeType = "repository";
            item.ScopeValue = repoId.ToString();
            await _promptRepo.AddAsync(item);
        }

        return Ok(overrides);
    }
}
