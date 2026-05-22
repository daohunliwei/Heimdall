using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

/// <summary>
/// 任务工件仓储实现。
/// </summary>
public class TaskArtifactRepository : ITaskArtifactRepository
{
    private readonly ISqlSugarClient _db;

    /// <summary>
    /// 初始化任务工件仓储。
    /// </summary>
    public TaskArtifactRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>
    /// 读取指定任务的全部工件。
    /// </summary>
    public async Task<List<TaskArtifact>> GetByTaskIdAsync(Guid taskId)
    {
        return await _db.Queryable<TaskArtifact>()
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.CreatedAt)
            .OrderBy(a => a.Sequence)
            .ToListAsync();
    }

    /// <summary>
    /// 按类型与键读取单个工件。
    /// </summary>
    public async Task<TaskArtifact?> GetByTypeAndKeyAsync(Guid taskId, string artifactType, string artifactKey)
    {
        return await _db.Queryable<TaskArtifact>()
            .FirstAsync(a => a.TaskId == taskId
                && a.ArtifactType == artifactType
                && a.ArtifactKey == artifactKey);
    }

    /// <summary>
    /// 按类型读取任务工件集合。
    /// </summary>
    public async Task<List<TaskArtifact>> GetByTypeAsync(Guid taskId, string artifactType)
    {
        return await _db.Queryable<TaskArtifact>()
            .Where(a => a.TaskId == taskId && a.ArtifactType == artifactType)
            .OrderBy(a => a.Sequence)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 幂等写入任务工件。
    /// </summary>
    public async Task<TaskArtifact> UpsertAsync(TaskArtifact artifact)
    {
        var existing = await _db.Queryable<TaskArtifact>()
            .FirstAsync(a => a.TaskId == artifact.TaskId
                && a.ArtifactType == artifact.ArtifactType
                && a.ArtifactKey == artifact.ArtifactKey);

        if (existing is null)
        {
            artifact.CreatedAt = DateTime.UtcNow;
            artifact.UpdatedAt = artifact.CreatedAt;
            await _db.Insertable(artifact).ExecuteCommandAsync();
            return artifact;
        }

        existing.StageName = artifact.StageName;
        existing.Status = artifact.Status;
        existing.Sequence = artifact.Sequence;
        existing.ContentHash = artifact.ContentHash;
        existing.Summary = artifact.Summary;
        existing.PayloadJson = artifact.PayloadJson;
        existing.ErrorMessage = artifact.ErrorMessage;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.Updateable(existing).ExecuteCommandAsync();
        return existing;
    }
}
