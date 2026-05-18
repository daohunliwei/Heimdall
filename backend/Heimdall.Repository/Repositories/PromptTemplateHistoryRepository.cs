using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class PromptTemplateHistoryRepository : IPromptTemplateHistoryRepository
{
    private readonly AppDbContext _context;

    public PromptTemplateHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PromptTemplateHistory>> GetByTemplateIdAsync(Guid templateId)
    {
        return await _context.PromptTemplateHistories
            .AsNoTracking()
            .Where(h => h.PromptTemplateId == templateId)
            .OrderByDescending(h => h.Version)
            .ToListAsync();
    }

    public async Task<PromptTemplateHistory?> GetByTemplateAndVersionAsync(Guid templateId, int version)
    {
        return await _context.PromptTemplateHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.PromptTemplateId == templateId && h.Version == version);
    }

    public async Task<PromptTemplateHistory> AddAsync(PromptTemplateHistory history)
    {
        history.ChangedAt = DateTime.UtcNow;
        _context.PromptTemplateHistories.Add(history);
        await _context.SaveChangesAsync();
        return history;
    }
}
