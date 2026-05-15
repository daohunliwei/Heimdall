using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class PromptOverrideRepository : IPromptOverrideRepository
{
    private readonly AppDbContext _context;

    public PromptOverrideRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<RepositoryPromptOverride>> GetByRepositoryAsync(Guid repositoryId)
    {
        return await _context.RepositoryPromptOverrides
            .AsNoTracking()
            .Include(o => o.PromptTemplate)
            .Where(o => o.RepositoryId == repositoryId && o.IsEnabled)
            .OrderByDescending(o => o.Priority)
            .ToListAsync();
    }

    public async Task<List<RepositoryPromptOverride>> GetByTemplateAsync(Guid templateId)
    {
        return await _context.RepositoryPromptOverrides
            .AsNoTracking()
            .Where(o => o.PromptTemplateId == templateId && o.IsEnabled)
            .ToListAsync();
    }

    public async Task<RepositoryPromptOverride?> GetByRepoAndTemplateAsync(Guid repositoryId, Guid templateId)
    {
        return await _context.RepositoryPromptOverrides
            .FirstOrDefaultAsync(o => o.RepositoryId == repositoryId && o.PromptTemplateId == templateId);
    }

    public async Task<RepositoryPromptOverride> AddAsync(RepositoryPromptOverride override_)
    {
        override_.CreatedAt = DateTime.UtcNow;
        _context.RepositoryPromptOverrides.Add(override_);
        await _context.SaveChangesAsync();
        return override_;
    }

    public async Task<RepositoryPromptOverride> UpdateAsync(RepositoryPromptOverride override_)
    {
        _context.RepositoryPromptOverrides.Update(override_);
        await _context.SaveChangesAsync();
        return override_;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var override_ = await _context.RepositoryPromptOverrides.FindAsync(id);
        if (override_ is null) return false;
        _context.RepositoryPromptOverrides.Remove(override_);
        await _context.SaveChangesAsync();
        return true;
    }
}
