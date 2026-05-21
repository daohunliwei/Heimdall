using System.Text.RegularExpressions;
using Heimdall.Core.Models;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Repository;

/// <summary>
/// 设计模式检测器——基于命名约定和结构特征启发式检测常见设计模式。
/// </summary>
public sealed class DesignPatternDetector
{
    private readonly ILogger<DesignPatternDetector> _logger;

    public DesignPatternDetector(ILogger<DesignPatternDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 从源文件中检测设计模式。
    /// </summary>
    public List<DetectedPattern> Detect(IEnumerable<(string filePath, string content)> sourceFiles)
    {
        var patterns = new List<DetectedPattern>();
        var fileList = sourceFiles.ToList();

        patterns.AddRange(DetectFactory(fileList));
        patterns.AddRange(DetectStrategy(fileList));
        patterns.AddRange(DetectObserver(fileList));
        patterns.AddRange(DetectBuilder(fileList));
        patterns.AddRange(DetectSingleton(fileList));
        patterns.AddRange(DetectRepository(fileList));
        patterns.AddRange(DetectMediator(fileList));

        _logger.LogInformation("设计模式检测完成 共检测到 {Count} 个模式", patterns.Count);
        return patterns;
    }

    private static IEnumerable<DetectedPattern> DetectFactory(List<(string filePath, string content)> files)
    {
        // 检测 Factory 模式：类名含 Factory + Create 方法
        foreach (var (filePath, content) in files)
        {
            var classMatch = Regex.Match(content, @"class\s+(\w*Factory\w*)\b");
            if (!classMatch.Success) continue;

            var className = classMatch.Groups[1].Value;
            var hasCreate = Regex.IsMatch(content, @"\b(?:Create|Build|Make|Get)\w*\s*\(");

            if (hasCreate)
            {
                // 查找产品类型
                var returnTypes = Regex.Matches(content, @"(?:returns?|:)\s*(\w+)")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(t => !IsCommonType(t))
                    .Distinct()
                    .Take(3)
                    .ToList();

                var participants = new List<PatternParticipant>
                {
                    new() { SymbolName = className, Role = "Creator", FilePath = filePath }
                };
                participants.AddRange(returnTypes.Select(t => new PatternParticipant
                {
                    SymbolName = t, Role = "Product", FilePath = filePath
                }));

                yield return new DetectedPattern
                {
                    PatternName = "Factory",
                    Participants = participants,
                    Confidence = 0.85,
                    ModuleName = GetModuleName(filePath)
                };
            }
        }
    }

    private static IEnumerable<DetectedPattern> DetectStrategy(List<(string filePath, string content)> files)
    {
        // 检测 Strategy 模式：接口 + 多个实现类
        var interfaces = files
            .SelectMany(f => Regex.Matches(f.content, @"interface\s+(I\w*(?:Strategy|Policy|Handler|Processor)\w*)")
                .Cast<Match>()
                .Select(m => (f.filePath, interfaceName: m.Groups[1].Value)))
            .ToList();

        foreach (var (ifFilePath, ifName) in interfaces)
        {
            var implementations = files
                .Where(f => Regex.IsMatch(f.content, $@"class\s+(\w+)\s*(?::\s*[^{{]*)?{Regex.Escape(ifName)}"))
                .Select(f =>
                {
                    var match = Regex.Match(f.content, $@"class\s+(\w+)\s*(?::\s*[^{{]*)?{Regex.Escape(ifName)}");
                    return (f.filePath, className: match.Groups[1].Value);
                })
                .ToList();

            if (implementations.Count >= 2)
            {
                var participants = new List<PatternParticipant>
                {
                    new() { SymbolName = ifName, Role = "Strategy Interface", FilePath = ifFilePath }
                };
                participants.AddRange(implementations.Select(i => new PatternParticipant
                {
                    SymbolName = i.className, Role = "Concrete Strategy", FilePath = i.filePath
                }));

                yield return new DetectedPattern
                {
                    PatternName = "Strategy",
                    Participants = participants,
                    Confidence = 0.80,
                    ModuleName = GetModuleName(ifFilePath)
                };
            }
        }
    }

    private static IEnumerable<DetectedPattern> DetectObserver(List<(string filePath, string content)> files)
    {
        // 检测 Observer/Event 模式
        foreach (var (filePath, content) in files)
        {
            var hasEvent = Regex.IsMatch(content, @"\bevent\s+\w+<?\w+>?\s+\w+");
            var hasSubscribe = Regex.IsMatch(content, @"\b(?:Subscribe|AddListener|On\w+|Register)\s*\(");
            var hasPublish = Regex.IsMatch(content, @"\b(?:Publish|Notify|Emit|Invoke|Fire|Raise)\s*\(");

            if (hasEvent || (hasSubscribe && hasPublish))
            {
                var classMatch = Regex.Match(content, @"class\s+(\w+)");
                if (classMatch.Success)
                {
                    yield return new DetectedPattern
                    {
                        PatternName = "Observer",
                        Participants = new()
                        {
                            new() { SymbolName = classMatch.Groups[1].Value, Role = "Subject", FilePath = filePath }
                        },
                        Confidence = hasEvent ? 0.85 : 0.65,
                        ModuleName = GetModuleName(filePath)
                    };
                }
            }
        }
    }

    private static IEnumerable<DetectedPattern> DetectBuilder(List<(string filePath, string content)> files)
    {
        foreach (var (filePath, content) in files)
        {
            var classMatch = Regex.Match(content, @"class\s+(\w*Builder\w*)");
            if (!classMatch.Success) continue;

            var className = classMatch.Groups[1].Value;
            var hasFluentMethods = Regex.Matches(content, @"public\s+\w*Builder\w*\s+\w+\s*\(")
                .Count >= 2;
            var hasBuild = Regex.IsMatch(content, @"\bBuild\s*\(");

            if (hasFluentMethods || hasBuild)
            {
                yield return new DetectedPattern
                {
                    PatternName = "Builder",
                    Participants = new()
                    {
                        new() { SymbolName = className, Role = "Builder", FilePath = filePath }
                    },
                    Confidence = hasFluentMethods && hasBuild ? 0.90 : 0.70,
                    ModuleName = GetModuleName(filePath)
                };
            }
        }
    }

    private static IEnumerable<DetectedPattern> DetectSingleton(List<(string filePath, string content)> files)
    {
        foreach (var (filePath, content) in files)
        {
            var classMatch = Regex.Match(content, @"class\s+(\w+)");
            if (!classMatch.Success) continue;

            var className = classMatch.Groups[1].Value;
            var hasStaticInstance = Regex.IsMatch(content, $@"static\s+(?:readonly\s+)?{className}\s+(?:Instance|_instance|instance)");
            var hasPrivateConstructor = Regex.IsMatch(content, $@"private\s+{className}\s*\(");

            if (hasStaticInstance && hasPrivateConstructor)
            {
                yield return new DetectedPattern
                {
                    PatternName = "Singleton",
                    Participants = new()
                    {
                        new() { SymbolName = className, Role = "Singleton", FilePath = filePath }
                    },
                    Confidence = 0.90,
                    ModuleName = GetModuleName(filePath)
                };
            }
        }
    }

    private static IEnumerable<DetectedPattern> DetectRepository(List<(string filePath, string content)> files)
    {
        var repos = files
            .Where(f => Regex.IsMatch(f.content, @"class\s+\w*Repository\w*"))
            .Select(f =>
            {
                var match = Regex.Match(f.content, @"class\s+(\w*Repository\w*)");
                return (f.filePath, className: match.Groups[1].Value);
            })
            .ToList();

        if (repos.Count >= 2)
        {
            yield return new DetectedPattern
            {
                PatternName = "Repository",
                Participants = repos.Select(r => new PatternParticipant
                {
                    SymbolName = r.className, Role = "Repository", FilePath = r.filePath
                }).ToList(),
                Confidence = 0.90,
                ModuleName = "DataAccess"
            };
        }
    }

    private static IEnumerable<DetectedPattern> DetectMediator(List<(string filePath, string content)> files)
    {
        foreach (var (filePath, content) in files)
        {
            var hasMediator = Regex.IsMatch(content, @"class\s+\w*(?:Mediator|Orchestrator|Coordinator)\w*");
            var hasMultipleDeps = Regex.Matches(content, @"private\s+readonly\s+\w+\s+_\w+").Count >= 3;

            if (hasMediator && hasMultipleDeps)
            {
                var classMatch = Regex.Match(content, @"class\s+(\w+)");
                yield return new DetectedPattern
                {
                    PatternName = "Mediator",
                    Participants = new()
                    {
                        new() { SymbolName = classMatch.Groups[1].Value, Role = "Mediator", FilePath = filePath }
                    },
                    Confidence = 0.75,
                    ModuleName = GetModuleName(filePath)
                };
            }
        }
    }

    private static string GetModuleName(string filePath)
    {
        var parts = filePath.Replace('\\', '/').Split('/');
        // 取倒数第二个目录名
        return parts.Length >= 2 ? parts[^2] : "root";
    }

    private static bool IsCommonType(string type)
    {
        return type is "void" or "string" or "int" or "bool" or "Task" or "object"
            or "var" or "List" or "Dictionary" or "IEnumerable" or "null";
    }
}
