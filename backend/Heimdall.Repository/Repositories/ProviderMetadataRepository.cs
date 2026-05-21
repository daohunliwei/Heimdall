using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class ProviderMetadataRepository : IProviderMetadataRepository
{
    private readonly AppDbContext _db;

    public ProviderMetadataRepository(AppDbContext db) => _db = db;

    public async Task<List<ProviderModelMetadataEntity>> GetAllAsync(CancellationToken ct = default)
        => await _db.ProviderModelMetadata.AsNoTracking().OrderBy(x => x.ProviderKey).ThenBy(x => x.ModelName).ToListAsync(ct);

    public async Task<ProviderModelMetadataEntity?> GetAsync(string providerKey, string modelName, CancellationToken ct = default)
        => await _db.ProviderModelMetadata.FirstOrDefaultAsync(x => x.ProviderKey == providerKey && x.ModelName == modelName, ct);

    public async Task UpsertAsync(ProviderModelMetadataEntity entity, CancellationToken ct = default)
    {
        var existing = await _db.ProviderModelMetadata.FirstOrDefaultAsync(
            x => x.ProviderKey == entity.ProviderKey && x.ModelName == entity.ModelName, ct);
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
        }
        else
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _db.ProviderModelMetadata.Add(entity);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string providerKey, string modelName, CancellationToken ct = default)
    {
        var record = await _db.ProviderModelMetadata.FirstOrDefaultAsync(
            x => x.ProviderKey == providerKey && x.ModelName == modelName, ct);
        if (record != null)
        {
            _db.ProviderModelMetadata.Remove(record);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task SeedDefaultsAsync(Dictionary<string, (string provider, string model, object metadata)> defaults, CancellationToken ct = default)
    {
        foreach (var (_, (provider, model, _)) in defaults)
        {
            var exists = await _db.ProviderModelMetadata.AnyAsync(
                x => x.ProviderKey == provider && x.ModelName == model, ct);
            if (!exists) continue;
        }
    }
}
