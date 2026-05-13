using Heimdall.Api.Mappings;
using Heimdall.Api.Models;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers.Admin;

[ApiController]
[Route("admin/users")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IUserRepository _userRepo;

    public UsersController(UserService userService, IUserRepository userRepo)
    {
        _userService = userService;
        _userRepo = userRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userRepo.GetAllAsync();
        return Ok(users.Select(u => u.ToUserInfoResponse()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminUserRequest request)
    {
        try
        {
            var user = await _userService.CreateAsync(request.Username, request.Password ?? "changeme123", request.Email, request.Role);
            return Ok(user.ToUserInfoResponse());
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminUserRequest request)
    {
        var user = await _userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();
        user.Username = request.Username;
        user.Email = request.Email;
        user.Role = request.Role;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
        await _userService.UpdateAsync(user);
        return Ok(user.ToUserInfoResponse());
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _userRepo.DeleteAsync(id);
        return deleted ? Ok() : NotFound();
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var user = await _userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();
        user.IsActive = !user.IsActive;
        await _userService.UpdateAsync(user);
        return Ok(user.ToUserInfoResponse());
    }
}
