using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Infrastructure.Logging;

/// <summary>
/// 动态日志过滤配置 — 注册为 Singleton，PostConfigure 每次日志写入时检查 LogCategoryFilter。
/// </summary>
public sealed class DynamicLogFilterOptions : IPostConfigureOptions<LoggerFilterOptions>
{
    private readonly LogCategoryFilter _filter;

    public DynamicLogFilterOptions(LogCategoryFilter filter)
    {
        _filter = filter;
    }

    public void PostConfigure(string? name, LoggerFilterOptions options)
    {
        // 在已有规则之前插入自定义过滤规则
        options.Rules.Insert(0, new LoggerFilterRule(
            providerName: null,
            categoryName: "SqlSugar",
            logLevel: null, // 由 filter 委托决定
            filter: (provider, category, level) =>
            {
                if (category?.StartsWith("SqlSugar") == true)
                    return _filter.ShowSqlCommands ? level >= LogLevel.Information : false;
                return true;
            }));
    }
}
