using Heimdall.Core.Models;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>
/// 版本化知识上下文服务接口。
/// 该接口负责为 Ask、Slides、Workshop 统一解析 RepositoryVersion、WikiVersion、页面与任务工件。
/// </summary>
public interface IVersionedKnowledgeService
{
    /// <summary>
    /// 解析一次派生任务所需的完整版本化知识上下文。
    /// </summary>
    Task<VersionedKnowledgeContext> ResolveAsync(
        VersionedTaskExecutionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将版本化页面集合整理为适合大模型消费的 Markdown 上下文。
    /// </summary>
    string BuildPageContextMarkdown(
        VersionedKnowledgeContext context,
        int maxPages,
        int maxCharacters);

    /// <summary>
    /// 将同源任务工件整理为适合大模型消费的文本摘要。
    /// </summary>
    string BuildArtifactContextMarkdown(
        VersionedKnowledgeContext context,
        int maxCharacters);
}
