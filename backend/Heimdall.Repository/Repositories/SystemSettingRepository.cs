using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly ISqlSugarClient _db;

    public SystemSettingRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        return await _db.Queryable<SystemSetting>()
            .FirstAsync(s => s.Key == key);
    }

    public async Task<SystemSetting> SetAsync(string key, string value)
    {
        var existing = await _db.Queryable<SystemSetting>()
            .FirstAsync(s => s.Key == key);

        if (existing is not null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.Updateable(existing).ExecuteCommandAsync();
        }
        else
        {
            existing = new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            };
            await _db.Insertable(existing).ExecuteCommandAsync();
        }

        return existing;
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        return await _db.Queryable<SystemSetting>()
            .OrderBy(s => s.Key)
            .ToListAsync();
    }
}
