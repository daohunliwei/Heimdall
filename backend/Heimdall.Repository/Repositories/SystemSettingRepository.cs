using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class SystemSettingRepository : BaseRepository<SystemSetting>, ISystemSettingRepository
{
    public SystemSettingRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        return await Context.Queryable<SystemSetting>()
            .FirstAsync(s => s.Key == key);
    }

    public async Task<SystemSetting> SetAsync(string key, string value)
    {
        var entity = new SystemSetting
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        };
        await Context.Storageable(entity)
            .WhereColumns(it => new { it.Key })
            .ExecuteCommandAsync();

        return entity;
    }

    public async Task SetBatchAsync(Dictionary<string, string> keyValues)
    {
        var entities = keyValues.Select(kv => new SystemSetting
        {
            Key = kv.Key,
            Value = kv.Value,
            UpdatedAt = DateTime.UtcNow
        }).ToList();
        await Context.Storageable(entities).WhereColumns(it => new { it.Key }).ExecuteCommandAsync();
    }

    public async Task<Dictionary<string, SystemSetting?>> GetByKeysAsync(IEnumerable<string> keys)
    {
        var keyList = keys.ToList();
        if (keyList.Count == 0) return new Dictionary<string, SystemSetting?>();
        var settings = await Context.Queryable<SystemSetting>()
            .Where(s => keyList.Contains(s.Key))
            .ToListAsync();
        return keyList.ToDictionary(k => k, k => settings.FirstOrDefault(s => s.Key == k));
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        return await Context.Queryable<SystemSetting>()
            .OrderBy(s => s.Key)
            .ToListAsync();
    }
}
