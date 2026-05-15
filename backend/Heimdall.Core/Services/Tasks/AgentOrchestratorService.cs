using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 分层代理协调服务——大仓库（>2000 文件）时自动启用子代理模式。
/// 主代理负责结构规划和全局一致性，子代理负责模块深度探索和页面生成。
/// </summary>
public sealed class AgentOrchestratorService
{
    private readonly ILogger<AgentOrchestratorService> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private const int DefaultMaxConcurrency = 3;

    public AgentOrchestratorService(ILogger<AgentOrchestratorService> logger)
    {
        _logger = logger;
        _concurrencySemaphore = new SemaphoreSlim(DefaultMaxConcurrency, DefaultMaxConcurrency);
    }

    /// <summary>
    /// 判断是否需要启用子代理模式。
    /// </summary>
    public bool ShouldUseSubAgents(int sourceFileCount, int threshold = 2000)
        => sourceFileCount >= threshold;

    /// <summary>
    /// 按模块将文件分组，每个子代理负责 1-2 个模块。
    /// </summary>
    public List<SubAgentAssignment> AssignModules(
        List<string> moduleNames,
        List<Models.CodeIndexEntry> entries)
    {
        var assignments = new List<SubAgentAssignment>();
        var modulesPerAgent = 2;

        for (var i = 0; i < moduleNames.Count; i += modulesPerAgent)
        {
            var assignedModules = moduleNames.Skip(i).Take(modulesPerAgent).ToList();
            var assignedFiles = entries
                .Where(e => assignedModules.Contains(e.ModuleName))
                .OrderByDescending(e => e.ImportanceScore)
                .ToList();

            assignments.Add(new SubAgentAssignment
            {
                AgentId = $"sub-agent-{assignments.Count + 1}",
                ModuleNames = assignedModules,
                KeyFiles = assignedFiles.Take(30).Select(f => f.FilePath).ToList(),
                AllFiles = assignedFiles.Select(f => f.FilePath).ToList(),
                SearchKeywords = ExtractKeywords(assignedModules, assignedFiles)
            });
        }

        _logger.LogInformation("子代理分配：{AgentCount} 个子代理, {ModuleCount} 个模块",
            assignments.Count, moduleNames.Count);

        return assignments;
    }

    /// <summary>
    /// 获取并发信号量（限制同时运行的子代理数）。
    /// </summary>
    public async Task<IDisposable> AcquireSlotAsync(CancellationToken ct)
    {
        await _concurrencySemaphore.WaitAsync(ct);
        return new SlotRelease(_concurrencySemaphore);
    }

    /// <summary>
    /// 记录子代理失败并返回降级处理后的文件列表。
    /// </summary>
    public List<string> HandleSubAgentFailure(SubAgentAssignment assignment, Exception? error)
    {
        _logger.LogWarning(error, "子代理失败 {AgentId} 模块 {Modules}，降级为单代理处理",
            assignment.AgentId, string.Join(", ", assignment.ModuleNames));

        return assignment.AllFiles;
    }

    private static List<string> ExtractKeywords(List<string> moduleNames, List<Models.CodeIndexEntry> files)
    {
        var keywords = new List<string>();
        keywords.AddRange(moduleNames);
        keywords.AddRange(files
            .SelectMany(f => f.ExportedSymbols)
            .Take(30));
        return keywords.Distinct().Take(50).ToList();
    }

    private sealed class SlotRelease : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        public SlotRelease(SemaphoreSlim semaphore) => _semaphore = semaphore;
        public void Dispose() => _semaphore.Release();
    }
}

public class SubAgentAssignment
{
    public string AgentId { get; init; } = string.Empty;
    public List<string> ModuleNames { get; init; } = new();
    public List<string> KeyFiles { get; init; } = new();
    public List<string> AllFiles { get; init; } = new();
    public List<string> SearchKeywords { get; init; } = new();
}
