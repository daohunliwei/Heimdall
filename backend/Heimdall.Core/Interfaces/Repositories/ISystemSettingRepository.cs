using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="SystemSetting"/> key/value pairs.</summary>
public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByKeyAsync(string key);

    /// <summary>Inserts or updates the value for the given key.</summary>
    Task<SystemSetting> SetAsync(string key, string value);

    Task<List<SystemSetting>> GetAllAsync();
}
