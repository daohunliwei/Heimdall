using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>User management: CRUD operations and password validation.</summary>
public interface IUserService
{
    /// <summary>Create a new user.</summary>
    Task<User> CreateAsync(User user);
    /// <summary>Get a user by their unique ID.</summary>
    Task<User?> GetByIdAsync(Guid id);
    /// <summary>Get a user by their username.</summary>
    Task<User?> GetByUsernameAsync(string username);
    /// <summary>Update an existing user.</summary>
    Task<User> UpdateAsync(User user);
    /// <summary>Delete a user by ID.</summary>
    Task DeleteAsync(Guid id);
    /// <summary>Validate a username/password combination.</summary>
    Task<bool> ValidatePasswordAsync(string username, string password);
}
