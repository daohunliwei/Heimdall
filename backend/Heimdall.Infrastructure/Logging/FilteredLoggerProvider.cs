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
            categoryName: "Microsoft.EntityFrameworkCore.Database.Command",
            logLevel: null, // 由 filter 委托决定
            filter: (provider, category, level) =>
            {
                if (category?.StartsWith("Microsoft.EntityFrameworkCore.Database.Command") == true)
                    return _filter.ShowSqlCommands ? level >= LogLevel.Information : false;
                if (category?.StartsWith("Microsoft.EntityFrameworkCore") == true)
                    return _filter.ShowEfCore ? level >= LogLevel.Information : level >= LogLevel.Warning;
                return true;
            }));
    }
}
