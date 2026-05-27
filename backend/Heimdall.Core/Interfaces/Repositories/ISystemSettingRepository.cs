using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="SystemSetting"/> key/value pairs.</summary>
public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByKeyAsync(string key);

    /// <summary>Inserts or updates the value for the given key.</summary>
    Task<SystemSetting> SetAsync(string key, string value);

    /// <summary>批量插入或更新设置项。</summary>
    Task SetBatchAsync(Dictionary<string, string> keyValues);

    /// <summary>按多个 Key 批量查询设置。</summary>
    Task<Dictionary<string, SystemSetting?>> GetByKeysAsync(IEnumerable<string> keys);

    Task<List<SystemSetting>> GetAllAsync();
}
