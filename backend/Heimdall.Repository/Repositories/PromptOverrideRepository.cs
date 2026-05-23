using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class PromptOverrideRepository : IPromptOverrideRepository
{
    private readonly ISqlSugarClient _db;

    public PromptOverrideRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<List<RepositoryPromptOverride>> GetByRepositoryAsync(Guid repositoryId)
    {
        return await _db.Queryable<RepositoryPromptOverride>()
            .Where(o => o.RepositoryId == repositoryId && o.IsEnabled)
            .OrderByDescending(o => o.Priority)
            .ToListAsync();
    }

    public async Task<List<RepositoryPromptOverride>> GetByTemplateAsync(Guid templateId)
    {
        return await _db.Queryable<RepositoryPromptOverride>()
            .Where(o => o.PromptTemplateId == templateId && o.IsEnabled)
            .ToListAsync();
    }

    public async Task<RepositoryPromptOverride?> GetByRepoAndTemplateAsync(Guid repositoryId, Guid templateId)
    {
        return await _db.Queryable<RepositoryPromptOverride>()
            .FirstAsync(o => o.RepositoryId == repositoryId && o.PromptTemplateId == templateId);
    }

    public async Task<RepositoryPromptOverride> AddAsync(RepositoryPromptOverride override_)
    {
        override_.CreatedAt = DateTime.UtcNow;
        await _db.Insertable(override_).ExecuteCommandAsync();
        return override_;
    }

    public async Task<RepositoryPromptOverride> UpdateAsync(RepositoryPromptOverride override_)
    {
        await _db.Updateable(override_).ExecuteCommandAsync();
        return override_;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var override_ = await _db.Queryable<RepositoryPromptOverride>()
            .FirstAsync(x => x.Id == id);
        if (override_ is null) return false;
        await _db.Deleteable(override_).ExecuteCommandAsync();
        return true;
    }
}
