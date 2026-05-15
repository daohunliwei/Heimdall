using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>
/// 任务工件仓储接口。
/// 该接口用于按任务读取恢复点、按类型读取工件并执行幂等写入。
/// </summary>
public interface ITaskArtifactRepository
{
    /// <summary>
    /// 读取任务的全部工件，按创建时间升序返回。
    /// </summary>
    Task<List<TaskArtifact>> GetByTaskIdAsync(Guid taskId);

    /// <summary>
    /// 按任务、工件类型与工件键读取单个工件。
    /// </summary>
    Task<TaskArtifact?> GetByTypeAndKeyAsync(Guid taskId, string artifactType, string artifactKey);

    /// <summary>
    /// 按任务与工件类型读取全部工件。
    /// </summary>
    Task<List<TaskArtifact>> GetByTypeAsync(Guid taskId, string artifactType);

    /// <summary>
    /// 新增或更新工件。
    /// 若同一任务下已存在相同类型与工件键，则执行更新。
    /// </summary>
    Task<TaskArtifact> UpsertAsync(TaskArtifact artifact);
}
