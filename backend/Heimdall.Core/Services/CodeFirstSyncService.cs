using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Heimdall.Core.Services;

public class CodeFirstSyncService
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<CodeFirstSyncService> _logger;

    public CodeFirstSyncService(ISqlSugarClient db, ILogger<CodeFirstSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SyncAsync()
    {
        var sw = Stopwatch.StartNew();
        int successCount = 0;
        int failedCount = 0;
        var failedTables = new List<string>();

        try
        {
            // 扫描 Core.Entities 命名空间的所有实体类型
            var entityTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                    && t.Namespace == "Heimdall.Core.Entities"
                    && t.GetCustomAttribute<SugarTable>() != null)
                .ToArray();

            _logger.LogInformation("扫描到 {Count} 个实体类型，开始 CodeFirst 同步", entityTypes.Length);

            foreach (var type in entityTypes)
            {
                try
                {
                    _db.CodeFirst.SetStringDefaultLength(200).InitTables(type);
                    successCount++;
                    _logger.LogDebug("实体 {Entity} 同步成功", type.Name);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    failedTables.Add($"{type.Name}: {ex.Message}");
                    _logger.LogError(ex, "实体 {Entity} 同步失败: {Message}", type.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "实体扫描失败，CodeFirst 同步中断");
        }

        sw.Stop();
        _logger.LogInformation("CodeFirst 同步完成: 成功 {SuccessCount} 张表, 失败 {FailedCount} 张表, 耗时 {ElapsedMs}ms",
            successCount, failedCount, sw.ElapsedMilliseconds);

        if (failedTables.Count > 0)
        {
            _logger.LogWarning("失败的表: {FailedTables}", string.Join("; ", failedTables));
        }
    }
}
