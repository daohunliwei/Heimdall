using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces;

public interface IProviderMetadataRepository
{
    Task<List<ProviderModelMetadataEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ProviderModelMetadataEntity?> GetAsync(string providerKey, string modelName, CancellationToken ct = default);
    Task UpsertAsync(ProviderModelMetadataEntity entity, CancellationToken ct = default);
    Task DeleteAsync(string providerKey, string modelName, CancellationToken ct = default);
    Task SeedDefaultsAsync(Dictionary<string, (string provider, string model, object metadata)> defaults, CancellationToken ct = default);
}
