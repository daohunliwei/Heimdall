using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;

namespace Heimdall.Core.Services.Auth;

public sealed class UserService
{
    private readonly IUserRepository _userRepo;

    public UserService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<User> CreateAsync(string username, string password, string? email = null, string role = "Viewer")
    {
        var existing = await _userRepo.GetByUsernameAsync(username);
        if (existing is not null)
            throw new InvalidOperationException("用户名已存在。");

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Source = UserSource.Local,
            Role = role
        };

        await _userRepo.AddAsync(user);
        return user;
    }

    public Task<User?> GetByIdAsync(Guid id) => _userRepo.GetByIdAsync(id);
    public Task<User?> GetByUsernameAsync(string username) => _userRepo.GetByUsernameAsync(username);

    public async Task UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);
    }

    public Task DeleteAsync(Guid id) => _userRepo.DeleteAsync(id);

    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var (valid, _) = await ValidateAndGetUserAsync(username, password);
        return valid;
    }

    /// <summary>验证密码并返回用户对象，避免调用方重复查询。</summary>
    public async Task<(bool Valid, User? User)> ValidateAndGetUserAsync(string username, string password)
    {
        var user = await _userRepo.GetByUsernameAsync(username);
        if (user is null || user.PasswordHash is null)
            return (false, null);

        var valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        return (valid, valid ? user : null);
    }
}
