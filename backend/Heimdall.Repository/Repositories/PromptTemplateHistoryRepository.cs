using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class PromptTemplateHistoryRepository : BaseRepository<PromptTemplateHistory>, IPromptTemplateHistoryRepository
{
    public PromptTemplateHistoryRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<PromptTemplateHistory>> GetByTemplateIdAsync(Guid templateId)
    {
        return await Context.Queryable<PromptTemplateHistory>()
            .Where(h => h.PromptTemplateId == templateId)
            .OrderByDescending(h => h.Version)
            .ToListAsync();
    }

    public async Task<PromptTemplateHistory?> GetByTemplateAndVersionAsync(Guid templateId, int version)
    {
        return await Context.Queryable<PromptTemplateHistory>()
            .FirstAsync(h => h.PromptTemplateId == templateId && h.Version == version);
    }

    public async Task<PromptTemplateHistory> AddAsync(PromptTemplateHistory history)
    {
        history.ChangedAt = DateTime.UtcNow;
        await Context.Insertable(history).ExecuteCommandAsync();
        return history;
    }
}
