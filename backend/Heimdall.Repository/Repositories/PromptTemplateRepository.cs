using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class PromptTemplateRepository : IPromptTemplateRepository
{
    private readonly AppDbContext _context;

    public PromptTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PromptTemplate>> GetAllAsync()
    {
        return await _context.PromptTemplates
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<PromptTemplate?> GetByIdAsync(Guid id)
    {
        return await _context.PromptTemplates.FindAsync(id);
    }

    public async Task<List<PromptTemplate>> GetByLayerAsync(string layer)
    {
        return await _context.PromptTemplates
            .AsNoTracking()
            .Where(p => p.Layer == layer && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<PromptTemplate> AddAsync(PromptTemplate template)
    {
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        _context.PromptTemplates.Add(template);
        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<PromptTemplate> UpdateAsync(PromptTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _context.PromptTemplates.Update(template);
        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var template = await _context.PromptTemplates.FindAsync(id);
        if (template is null) return false;
        _context.PromptTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }
}
