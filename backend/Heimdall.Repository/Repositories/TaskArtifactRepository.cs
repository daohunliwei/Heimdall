using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

/// <summary>
/// 任务工件仓储实现。
/// </summary>
public class TaskArtifactRepository : BaseRepository<TaskArtifact>, ITaskArtifactRepository
{
    /// <summary>
    /// 初始化任务工件仓储。
    /// </summary>
    public TaskArtifactRepository(ISqlSugarClient db) : base(db)
    {
    }

    /// <summary>
    /// 读取指定任务的全部工件。
    /// </summary>
    public async Task<List<TaskArtifact>> GetByTaskIdAsync(Guid taskId)
    {
        return await Context.Queryable<TaskArtifact>()
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => new { a.CreatedAt, a.Sequence })
            .ToListAsync();
    }

    /// <summary>
    /// 按类型与键读取单个工件。
    /// </summary>
    public async Task<TaskArtifact?> GetByTypeAndKeyAsync(Guid taskId, string artifactType, string artifactKey)
    {
        return await Context.Queryable<TaskArtifact>()
            .FirstAsync(a => a.TaskId == taskId
                && a.ArtifactType == artifactType
                && a.ArtifactKey == artifactKey);
    }

    /// <summary>
    /// 按类型读取任务工件集合。
    /// </summary>
    public async Task<List<TaskArtifact>> GetByTypeAsync(Guid taskId, string artifactType)
    {
        return await Context.Queryable<TaskArtifact>()
            .Where(a => a.TaskId == taskId && a.ArtifactType == artifactType)
            .OrderBy(a => new { a.Sequence, a.CreatedAt })
            .ToListAsync();
    }

    /// <summary>
    /// 幂等写入任务工件。
    /// </summary>
    public async Task<TaskArtifact> UpsertAsync(TaskArtifact artifact)
    {
        artifact.UpdatedAt = DateTime.UtcNow;
        await Context.Storageable(artifact)
            .WhereColumns(it => new { it.TaskId, it.ArtifactType, it.ArtifactKey })
            .ExecuteCommandAsync();

        return artifact;
    }
}
