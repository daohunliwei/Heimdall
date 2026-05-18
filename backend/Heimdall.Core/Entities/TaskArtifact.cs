namespace Heimdall.Core.Entities;

/// <summary>
/// 任务工件实体。
/// 该实体用于持久化长任务在各阶段产出的规划结果、页面批次结果、关系结果、渲染快照与向量写入摘要，
/// 从而支持失败恢复、阶段审计与结果一致性校验。
/// </summary>
public class TaskArtifact
{
    /// <summary>
    /// 工件主标识。
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// 所属任务标识。
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// 所属任务实体。
    /// </summary>
    public TaskRecord Task { get; set; } = null!;

    /// <summary>
    /// 工件类型。
    /// 例如 planning_artifact、page_batch_artifact、relation_artifact、quality_report_artifact、render_artifact。
    /// </summary>
    public string ArtifactType { get; set; } = string.Empty;

    /// <summary>
    /// 工件键。
    /// 用于区分同类型下的不同批次或不同子结果，例如 batch-0001。
    /// </summary>
    public string ArtifactKey { get; set; } = string.Empty;

    /// <summary>
    /// 对应的执行阶段名称。
    /// </summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>
    /// 工件状态。
    /// 常见值包括 completed、failed。
    /// </summary>
    public string Status { get; set; } = "completed";

    /// <summary>
    /// 顺序号。
    /// 对批次类工件可表示批次序号，对单例工件通常为 0。
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 工件内容哈希。
    /// 用于辅助幂等更新与一致性比对。
    /// </summary>
    public string? ContentHash { get; set; }

    /// <summary>
    /// 工件摘要说明。
    /// 用于快速浏览，而无需展开完整 JSON。
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 工件完整载荷 JSON。
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// 当工件写入失败时记录错误信息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 工件创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 工件最近更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
