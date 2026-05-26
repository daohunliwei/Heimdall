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
        foreach (var (_, (provider, model, _)) in defaults)
        {
            var exists = await Context.Queryable<ProviderModelMetadataEntity>()
                .AnyAsync(x => x.ProviderKey == provider && x.ModelName == model);
            if (!exists) continue;
        }
    }
}
