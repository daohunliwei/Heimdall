using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class ProviderMetadataRepository : IProviderMetadataRepository
{
    private readonly ISqlSugarClient _db;

    public ProviderMetadataRepository(ISqlSugarClient db) => _db = db;

    public async Task<List<ProviderModelMetadataEntity>> GetAllAsync(CancellationToken ct = default)
        => await _db.Queryable<ProviderModelMetadataEntity>()
            .OrderBy(x => x.ProviderKey)
            .OrderBy(x => x.ModelName)
            .ToListAsync(ct);

    public async Task<ProviderModelMetadataEntity?> GetAsync(string providerKey, string modelName, CancellationToken ct = default)
        => await _db.Queryable<ProviderModelMetadataEntity>()
            .FirstAsync(x => x.ProviderKey == providerKey && x.ModelName == modelName);

    public async Task UpsertAsync(ProviderModelMetadataEntity entity, CancellationToken ct = default)
    {
        var existing = await _db.Queryable<ProviderModelMetadataEntity>()
            .FirstAsync(x => x.ProviderKey == entity.ProviderKey && x.ModelName == entity.ModelName);

        if (existing != null)
        {
            existing.BillingType = entity.BillingType;
            existing.MaxContextTokens = entity.MaxContextTokens;
            existing.MaxOutputTokens = entity.MaxOutputTokens;
            existing.RateLimitPerMinute = entity.RateLimitPerMinute;
            existing.InputTokenPrice = entity.InputTokenPrice;
            existing.OutputTokenPrice = entity.OutputTokenPrice;
            existing.CallPrice = entity.CallPrice;
            existing.SupportsCaching = entity.SupportsCaching;
            existing.ContextFillRatio = entity.ContextFillRatio;
            existing.ContextWarningThreshold = entity.ContextWarningThreshold;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.Updateable(existing).ExecuteCommandAsync(ct);
        }
        else
        {
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.Insertable(entity).ExecuteCommandAsync(ct);
        }
    }

    public async Task DeleteAsync(string providerKey, string modelName, CancellationToken ct = default)
    {
        var record = await _db.Queryable<ProviderModelMetadataEntity>()
            .FirstAsync(x => x.ProviderKey == providerKey && x.ModelName == modelName);

        if (record != null)
        {
            await _db.Deleteable(record).ExecuteCommandAsync(ct);
        }
    }

    public async Task SeedDefaultsAsync(Dictionary<string, (string provider, string model, object metadata)> defaults, CancellationToken ct = default)
    {
        foreach (var (_, (provider, model, _)) in defaults)
        {
            var exists = await _db.Queryable<ProviderModelMetadataEntity>()
                .AnyAsync(x => x.ProviderKey == provider && x.ModelName == model);
            if (!exists) continue;
        }
    }
}
