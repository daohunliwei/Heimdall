using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Repository.Repositories;

public class WikiSpaceRepository : IWikiSpaceRepository
{
    private readonly AppDbContext _context;
    public WikiSpaceRepository(AppDbContext context) => _context = context;

    public async Task<WikiSpace?> GetByRepoLangViewAsync(Guid repositoryId, string language, string viewType)
    {
        return await _context.WikiSpaces
            .FirstOrDefaultAsync(s => s.RepositoryId == repositoryId
                && s.Language == language && s.ViewType == viewType);
    }

    public async Task<WikiSpace> AddAsync(WikiSpace space)
    {
        _context.WikiSpaces.Add(space);
        await _context.SaveChangesAsync();
        return space;
    }

    public async Task<WikiSpace> UpdateAsync(WikiSpace space)
    {
        _context.WikiSpaces.Update(space);
        await _context.SaveChangesAsync();
        return space;
    }
}
