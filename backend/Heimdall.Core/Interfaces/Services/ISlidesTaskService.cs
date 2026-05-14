using Heimdall.Core.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// Slides 派生任务服务接口。
/// 该接口负责基于版本化页面与任务工件派生演示文稿内容。
/// </summary>
public interface ISlidesTaskService
{
    /// <summary>
    /// 执行一次 Slides 生成请求。
    /// </summary>
    Task<SlidesTaskExecutionResult> GenerateAsync(
        SlidesTaskExecutionRequest request,
        CancellationToken cancellationToken = default);
}
