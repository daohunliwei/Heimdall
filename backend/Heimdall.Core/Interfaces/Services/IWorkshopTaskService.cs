using Heimdall.Core.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// Workshop 派生任务服务接口。
/// 该接口负责基于版本化页面与任务工件派生训练营内容。
/// </summary>
public interface IWorkshopTaskService
{
    /// <summary>
    /// 执行一次 Workshop 生成请求。
    /// </summary>
    Task<WorkshopTaskExecutionResult> GenerateAsync(
        WorkshopTaskExecutionRequest request,
        CancellationToken cancellationToken = default);
}
