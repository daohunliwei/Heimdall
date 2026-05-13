using Heimdall.Api.Models;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers.Admin;

[ApiController]
[Route("admin/settings")]
[Authorize(Policy = "AdminOnly")]
public class SettingsController : ControllerBase
{
    private readonly ISystemSettingRepository _settingRepo;

    public SettingsController(ISystemSettingRepository settingRepo)
    {
        _settingRepo = settingRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await _settingRepo.GetAllAsync();
        return Ok(settings);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] List<SystemSettingRequest> requests)
    {
        foreach (var req in requests)
        {
            await _settingRepo.SetAsync(req.Key, req.Value);
        }

        var settings = await _settingRepo.GetAllAsync();
        return Ok(settings);
    }
}
