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

        return (await Context.Queryable<SystemSetting>().FirstAsync(s => s.Key == key))!;
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        return await Context.Queryable<SystemSetting>()
            .OrderBy(s => s.Key)
            .ToListAsync();
    }
}
