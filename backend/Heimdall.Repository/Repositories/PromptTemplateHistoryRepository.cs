using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class PromptTemplateHistoryRepository : IPromptTemplateHistoryRepository
{
    private readonly ISqlSugarClient _db;

    public PromptTemplateHistoryRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<List<PromptTemplateHistory>> GetByTemplateIdAsync(Guid templateId)
    {
        return await _db.Queryable<PromptTemplateHistory>()
            .Where(h => h.PromptTemplateId == templateId)
            .OrderByDescending(h => h.Version)
            .ToListAsync();
    }

    public async Task<PromptTemplateHistory?> GetByTemplateAndVersionAsync(Guid templateId, int version)
    {
        return await _db.Queryable<PromptTemplateHistory>()
            .FirstAsync(h => h.PromptTemplateId == templateId && h.Version == version);
    }

    public async Task<PromptTemplateHistory> AddAsync(PromptTemplateHistory history)
    {
        history.ChangedAt = DateTime.UtcNow;
        await _db.Insertable(history).ExecuteCommandAsync();
        return history;
    }
}
