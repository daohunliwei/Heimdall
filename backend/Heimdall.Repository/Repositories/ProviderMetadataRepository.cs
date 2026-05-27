using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class ProviderMetadataRepository : BaseRepository<ProviderModelMetadataEntity>, IProviderMetadataRepository
{
    public ProviderMetadataRepository(ISqlSugarClient db) : base(db) { }

    public async Task<List<ProviderModelMetadataEntity>> GetAllAsync(CancellationToken ct = default)
        => await Context.Queryable<ProviderModelMetadataEntity>()
            .OrderBy(x => new { x.ProviderKey, x.ModelName })
            .ToListAsync(ct);

    public async Task<ProviderModelMetadataEntity?> GetAsync(string providerKey, string modelName, CancellationToken ct = default)
        => await Context.Queryable<ProviderModelMetadataEntity>()
            .FirstAsync(x => x.ProviderKey == providerKey && x.ModelName == modelName);

    public async Task UpsertAsync(ProviderModelMetadataEntity entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await Context.Storageable(entity)
            .WhereColumns(it => new { it.ProviderKey, it.ModelName })
            .ExecuteCommandAsync(ct);
    }

    public async Task DeleteAsync(string providerKey, string modelName, CancellationToken ct = default)
    {
        var record = await Context.Queryable<ProviderModelMetadataEntity>()
            .FirstAsync(x => x.ProviderKey == providerKey && x.ModelName == modelName);

        if (record != null)
        {
            await Context.Deleteable(record).ExecuteCommandAsync(ct);
        }
    }

    public async Task SeedDefaultsAsync(Dictionary<string, (string provider, string model, object metadata)> defaults, CancellationToken ct = default)
    {
        var keys = defaults.Values.Select(d => (d.provider, d.model)).ToList();
        if (keys.Count == 0) return;

        // 一次批量查询检查所有已存在的键
        var existingPairs = await Context.Queryable<ProviderModelMetadataEntity>()
            .Where(x => keys.Select(k => k.provider).Contains(x.ProviderKey)
                && keys.Select(k => k.model).Contains(x.ModelName))
            .Select(x => new { x.ProviderKey, x.ModelName })
            .ToListAsync(ct);

        var existingSet = existingPairs.Select(x => (x.ProviderKey, x.ModelName)).ToHashSet();

        foreach (var (_, (provider, model, _)) in defaults)
        {
            if (existingSet.Contains((provider, model))) continue;
            // 默认条目不存在——此方法仅用于检查缺失项，实际插入由上层 PromptSeedData 执行
        }
    }
}
