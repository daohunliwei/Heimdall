using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class PromptTemplateRepository : BaseRepository<PromptTemplate>, IPromptTemplateRepository
{
    public PromptTemplateRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<PromptTemplate>> GetAllAsync()
    {
        return await Context.Queryable<PromptTemplate>()
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<PromptTemplate?> GetByIdAsync(Guid id)
    {
        return await Context.Queryable<PromptTemplate>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<PromptTemplate?> GetBySlugAsync(string slug)
    {
        return await Context.Queryable<PromptTemplate>()
            .FirstAsync(p => p.Slug == slug && p.IsActive);
    }

    public async Task<List<PromptTemplate>> GetByLayerAsync(string layer)
    {
        return await Context.Queryable<PromptTemplate>()
            .Where(p => p.Layer == layer && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<PromptTemplate>> GetByCategoryAsync(string category)
    {
        return await Context.Queryable<PromptTemplate>()
            .Where(p => p.Category == category && p.IsActive)
            .OrderBy(p => p.Priority)
            .ToListAsync();
    }

    public async Task<List<PromptTemplate>> GetBySlugAsync(IEnumerable<string> slugs)
    {
        return await Context.Queryable<PromptTemplate>()
            .Where(p => slugs.Contains(p.Slug) && p.IsActive)
            .OrderBy(p => p.Priority)
            .ToListAsync();
    }

    public async Task<PromptTemplate> AddAsync(PromptTemplate template)
    {
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        await Context.Insertable(template).ExecuteCommandAsync();
        return template;
    }

    public async Task<PromptTemplate> UpdateAsync(PromptTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        await Context.Updateable(template).ExecuteCommandAsync();
        return template;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var template = await Context.Queryable<PromptTemplate>()
            .FirstAsync(x => x.Id == id);
        if (template is null) return false;
        await Context.Deleteable(template).ExecuteCommandAsync();
        return true;
    }
}
