using System.Text;
using System.Text.Json;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 版本化知识上下文服务。
/// 该服务负责为 Ask、Slides、Workshop 三类派生任务统一解析显式版本锚点、版本化页面与任务工件。
/// </summary>
public sealed class VersionedKnowledgeService : IVersionedKnowledgeService
{
    private static readonly string[] PreferredArtifactTypes =
    [
        "planning_artifact",
        "quality_report_artifact",
        "render_postprocess_artifact",
        "render_artifact",
        "relation_artifact"
    ];

    private readonly IRepositoryConfigRepository _repositoryRepository;
    private readonly IRepositoryVersionRepository _repositoryVersionRepository;
    private readonly IWikiVersionRepository _wikiVersionRepository;
    private readonly IWikiPageRepository _wikiPageRepository;
    private readonly IWikiSpaceRepository _wikiSpaceRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<VersionedKnowledgeService> _logger;

    /// <summary>
    /// 初始化版本化知识上下文服务。
    /// </summary>
    public VersionedKnowledgeService(
        IRepositoryConfigRepository repositoryRepository,
        IRepositoryVersionRepository repositoryVersionRepository,
        IWikiVersionRepository wikiVersionRepository,
        IWikiPageRepository wikiPageRepository,
        IWikiSpaceRepository wikiSpaceRepository,
        ITaskRepository taskRepository,
        ILogger<VersionedKnowledgeService> logger)
    {
        _repositoryRepository = repositoryRepository;
        _repositoryVersionRepository = repositoryVersionRepository;
        _wikiVersionRepository = wikiVersionRepository;
        _wikiPageRepository = wikiPageRepository;
        _wikiSpaceRepository = wikiSpaceRepository;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    /// <summary>
    /// 解析完整的版本化知识上下文。
    /// </summary>
    public async Task<VersionedKnowledgeContext> ResolveAsync(
        VersionedTaskExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var repository = await _repositoryRepository.GetByIdAsync(options.RepositoryId)
            ?? throw new InvalidOperationException($"仓库不存在：{options.RepositoryId}");

        var requestedLanguage = string.IsNullOrWhiteSpace(options.Language)
            ? repository.DefaultLanguage ?? "zh"
            : options.Language.Trim();

        var requestedBranch = string.IsNullOrWhiteSpace(options.Branch)
            ? repository.DefaultBranch ?? "main"
            : options.Branch.Trim();

        var repositoryVersion = await ResolveRepositoryVersionAsync(repository.Id, options.RepositoryVersionId, requestedBranch);
        var (wikiVersion, wikiSpace) = await ResolveWikiVersionAsync(
            repository.Id,
            requestedLanguage,
            options.WikiVersionId,
            repositoryVersion?.Id);

        // wikiVersion 的 RepositoryVersion 已通过 ResolveWikiVersionAsync 加载时的条件查询保证存在
        // 此处仅在未显式指定 repositoryVersion 时补查（fallback 场景，极少走至此分支）
        if (repositoryVersion is null)
        {
            repositoryVersion ??= await _repositoryVersionRepository.GetByIdAsync(wikiVersion.RepositoryVersionId)
                ?? throw new InvalidOperationException($"未找到与 WikiVersion 绑定的 RepositoryVersion：{wikiVersion.RepositoryVersionId}");
        }

        if (repositoryVersion.RepositoryId != repository.Id)
            throw new InvalidOperationException("RepositoryVersion 与请求仓库不匹配。");

        if (options.RepositoryVersionId.HasValue && wikiVersion.RepositoryVersionId != options.RepositoryVersionId.Value)
            throw new InvalidOperationException("显式指定的 RepositoryVersion 与 WikiVersion 不一致。");

        var pages = await _wikiPageRepository.GetByWikiVersionIdAsync(wikiVersion.Id);
        if (pages.Count == 0)
            throw new InvalidOperationException($"WikiVersion {wikiVersion.Id} 下不存在可消费的页面数据。");

        var artifacts = await LoadArtifactsAsync(wikiVersion, cancellationToken);

        _logger.LogInformation(
            "版本化知识上下文解析完成 RepositoryId={RepositoryId} RepositoryVersionId={RepositoryVersionId} WikiVersionId={WikiVersionId} PageCount={PageCount} ArtifactCount={ArtifactCount}",
            repository.Id,
            repositoryVersion.Id,
            wikiVersion.Id,
            pages.Count,
            artifacts.Count);

        return new VersionedKnowledgeContext
        {
            Repository = repository,
            RepositoryVersion = repositoryVersion,
            WikiVersion = wikiVersion,
            Pages = pages.OrderBy(page => page.PageOrder).ToList(),
            Artifacts = artifacts,
            EffectiveLanguage = wikiSpace.Language,
            EffectiveBranch = repositoryVersion.BranchName
        };
    }

    /// <summary>
    /// 构建页面 Markdown 上下文。
    /// </summary>
    public string BuildPageContextMarkdown(
        VersionedKnowledgeContext context,
        int maxPages,
        int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder();
        builder.AppendLine("## 版本化页面内容");
        builder.AppendLine($"- RepositoryVersionId: {context.RepositoryVersion.Id}");
        builder.AppendLine($"- WikiVersionId: {context.WikiVersion.Id}");
        builder.AppendLine();

        foreach (var page in context.Pages
                     .OrderBy(page => page.PageOrder)
                     .Take(Math.Max(1, maxPages)))
        {
            AppendPage(builder, page, maxCharacters);
            if (builder.Length >= maxCharacters)
                break;
        }

        return TrimToLength(builder.ToString(), maxCharacters);
    }

    /// <summary>
    /// 构建任务工件摘要上下文。
    /// </summary>
    public string BuildArtifactContextMarkdown(
        VersionedKnowledgeContext context,
        int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Artifacts.Count == 0)
            return "## 任务工件摘要\n当前 WikiVersion 未找到可复用的任务工件。";

        var builder = new StringBuilder();
        builder.AppendLine("## 任务工件摘要");
        builder.AppendLine($"- WikiVersionId: {context.WikiVersion.Id}");
        builder.AppendLine($"- 工件数量: {context.Artifacts.Count}");
        builder.AppendLine();

        foreach (var artifact in context.Artifacts)
        {
            builder.AppendLine($"### {artifact.ArtifactType}");
            builder.AppendLine($"- 工件键: {artifact.ArtifactKey}");
            builder.AppendLine($"- 所属阶段: {artifact.StageName}");
            if (!string.IsNullOrWhiteSpace(artifact.Summary))
                builder.AppendLine($"- 摘要: {artifact.Summary}");

            var payloadSummary = SummarizeArtifactPayload(artifact);
            if (!string.IsNullOrWhiteSpace(payloadSummary))
            {
                builder.AppendLine(payloadSummary);
            }

            builder.AppendLine();
            if (builder.Length >= maxCharacters)
                break;
        }

        return TrimToLength(builder.ToString(), maxCharacters);
    }

    /// <summary>
    /// 解析显式指定或默认继承的 RepositoryVersion。
    /// </summary>
    private async Task<RepositoryVersion?> ResolveRepositoryVersionAsync(
        Guid repositoryId,
        Guid? repositoryVersionId,
        string requestedBranch)
    {
        if (!repositoryVersionId.HasValue)
            return await _repositoryVersionRepository.GetLatestByRepoBranchAsync(repositoryId, requestedBranch);

        var repositoryVersion = await _repositoryVersionRepository.GetByIdAsync(repositoryVersionId.Value)
            ?? throw new InvalidOperationException($"RepositoryVersion 不存在：{repositoryVersionId.Value}");

        if (repositoryVersion.RepositoryId != repositoryId)
            throw new InvalidOperationException("显式指定的 RepositoryVersion 与仓库不匹配。");

        return repositoryVersion;
    }

    /// <summary>
    /// 解析显式指定或默认继承的 WikiVersion。
    /// </summary>
    private async Task<(WikiVersion WikiVersion, WikiSpace WikiSpace)> ResolveWikiVersionAsync(
        Guid repositoryId,
        string language,
        Guid? wikiVersionId,
        Guid? repositoryVersionId)
    {
        if (wikiVersionId.HasValue)
        {
            var explicitWikiVersion = await _wikiVersionRepository.GetByIdAsync(wikiVersionId.Value)
                ?? throw new InvalidOperationException($"WikiVersion 不存在：{wikiVersionId.Value}");

            var explicitWikiSpace = await _wikiSpaceRepository.GetByIdAsync(explicitWikiVersion.WikiSpaceId)
                ?? throw new InvalidOperationException($"WikiVersion {wikiVersionId.Value} 绑定的 WikiSpace 不存在。");

            if (explicitWikiSpace.RepositoryId != repositoryId)
                throw new InvalidOperationException("显式指定的 WikiVersion 与仓库不匹配。");

            return (explicitWikiVersion, explicitWikiSpace);
        }

        var wikiSpace = await _wikiSpaceRepository.GetByRepoLangViewAsync(repositoryId, language, "default")
            ?? throw new InvalidOperationException($"仓库 {repositoryId} 在语言 {language} 下不存在可用的 WikiSpace。");

        WikiVersion? selectedVersion = null;

        if (repositoryVersionId.HasValue)
        {
            selectedVersion = (await _wikiVersionRepository.GetBySpaceIdAsync(wikiSpace.Id))
                .Where(version => version.RepositoryVersionId == repositoryVersionId.Value)
                .OrderByDescending(version => version.VersionNo)
                .FirstOrDefault();
        }

        if (selectedVersion is null && wikiSpace.PublishedWikiVersionId.HasValue)
        {
            selectedVersion = await _wikiVersionRepository.GetByIdAsync(wikiSpace.PublishedWikiVersionId.Value);
        }

        selectedVersion ??= await _wikiVersionRepository.GetLatestBySpaceIdAsync(wikiSpace.Id);

        if (selectedVersion is null)
            throw new InvalidOperationException($"仓库 {repositoryId} 在语言 {language} 下尚未生成 WikiVersion。");

        return (selectedVersion, wikiSpace);
    }

    /// <summary>
    /// 读取与当前 WikiVersion 同源的任务工件。
    /// </summary>
    private async Task<IReadOnlyList<KnowledgeArtifactSnapshot>> LoadArtifactsAsync(
        WikiVersion wikiVersion,
        CancellationToken cancellationToken)
    {
        if (!wikiVersion.CreatedByTaskId.HasValue)
            return [];

        var task = await _taskRepository.GetByIdAsync(wikiVersion.CreatedByTaskId.Value);
        if (task is null)
        {
            _logger.LogWarning("WikiVersion 缺少可追溯任务记录 WikiVersionId={WikiVersionId} TaskId={TaskId}",
                wikiVersion.Id,
                wikiVersion.CreatedByTaskId.Value);
            return [];
        }

        var artifacts = task.Artifacts
            .Where(artifact => string.Equals(artifact.Status, "completed", StringComparison.OrdinalIgnoreCase))
            .Where(artifact => PreferredArtifactTypes.Contains(artifact.ArtifactType, StringComparer.OrdinalIgnoreCase))
            .OrderBy(artifact => Array.IndexOf(PreferredArtifactTypes, artifact.ArtifactType))
            .ThenBy(artifact => artifact.Sequence)
            .Select(artifact => new KnowledgeArtifactSnapshot
            {
                ArtifactType = artifact.ArtifactType,
                ArtifactKey = artifact.ArtifactKey,
                StageName = artifact.StageName,
                Summary = artifact.Summary,
                PayloadJson = artifact.PayloadJson
            })
            .ToList();

        return artifacts;
    }

    /// <summary>
    /// 追加单页内容到上下文构建器。
    /// </summary>
    private static void AppendPage(StringBuilder builder, WikiPage page, int maxCharacters)
    {
        builder.AppendLine($"### {page.Title}");
        builder.AppendLine($"- PageOrder: {page.PageOrder}");
        builder.AppendLine($"- PageType: {page.PageType}");
        if (!string.IsNullOrWhiteSpace(page.Summary))
            builder.AppendLine($"- Summary: {page.Summary}");
        if (page.FilePaths is { Length: > 0 })
            builder.AppendLine($"- SourceFiles: {string.Join(", ", page.FilePaths.Take(6))}");
        builder.AppendLine();
        builder.AppendLine(page.ContentMarkdown ?? "当前页面没有正文。");
        builder.AppendLine();

        if (builder.Length > maxCharacters)
            builder.Length = maxCharacters;
    }

    /// <summary>
    /// 将工件原始 JSON 压缩为适合提示词消费的摘要。
    /// </summary>
    private static string SummarizeArtifactPayload(KnowledgeArtifactSnapshot artifact)
    {
        try
        {
            using var document = JsonDocument.Parse(artifact.PayloadJson);
            var root = document.RootElement;
            var builder = new StringBuilder();

            if (root.TryGetProperty("page_count", out var pageCount))
                builder.AppendLine($"- 页面数: {pageCount}");
            if (root.TryGetProperty("relation_count", out var relationCount))
                builder.AppendLine($"- 关系数: {relationCount}");
            if (root.TryGetProperty("rendered_page_count", out var renderedPageCount))
                builder.AppendLine($"- 渲染页数: {renderedPageCount}");
            if (root.TryGetProperty("chunk_count", out var chunkCount))
                builder.AppendLine($"- 向量分块数: {chunkCount}");

            if (root.TryGetProperty("pages", out var pagesElement) && pagesElement.ValueKind == JsonValueKind.Array)
            {
                var titles = pagesElement.EnumerateArray()
                    .Select(page => page.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null)
                    .Where(title => !string.IsNullOrWhiteSpace(title))
                    .Take(6)
                    .ToList();

                if (titles.Count > 0)
                    builder.AppendLine($"- 代表页面: {string.Join("、", titles)}");
            }

            if (root.TryGetProperty("report", out var reportElement))
            {
                builder.AppendLine("- 质量报告摘要:");
                builder.AppendLine($"```json\n{TrimToLength(reportElement.GetRawText(), 800)}\n```");
            }

            if (root.TryGetProperty("structure_json", out var structureJsonElement) && structureJsonElement.ValueKind == JsonValueKind.String)
            {
                builder.AppendLine("- 结构规划片段:");
                builder.AppendLine($"```json\n{TrimToLength(structureJsonElement.GetString() ?? string.Empty, 1200)}\n```");
            }

            return builder.ToString().Trim();
        }
        catch
        {
            return $"```json\n{TrimToLength(artifact.PayloadJson, 1200)}\n```";
        }
    }

    /// <summary>
    /// 按最大长度截断文本。
    /// </summary>
    private static string TrimToLength(string value, int maxCharacters)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            return value;

        return value[..maxCharacters] + "\n\n... (内容已截断)";
    }
}
