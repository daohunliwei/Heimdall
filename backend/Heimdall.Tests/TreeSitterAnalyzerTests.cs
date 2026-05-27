using Heimdall.Infrastructure.AstAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using TreeSitter;

namespace Heimdall.Tests;

/// <summary>
/// Tree-sitter 分析器测试
/// </summary>
public class TreeSitterAnalyzerTests
{
    private readonly TreeSitterAnalyzer _analyzer = new(new NullLogger<TreeSitterAnalyzer>());

    /// <summary>
    /// 验证构建输出包含 C# native 语法库
    /// </summary>
    [Fact]
    public void NativeLibrary_CSharpDll_ShouldExistInBuildOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var nativePath = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            "win-x64",
            "native",
            "tree-sitter-c-sharp.dll");

        Assert.True(File.Exists(nativePath), $"缺少 native 语法库: {nativePath}");
    }

    /// <summary>
    /// 验证 25 种以上语言均可创建 Language 实例
    /// </summary>
    [Fact]
    public void LanguageInstances_ShouldLoadForTwentyFivePlusLanguages()
    {
        var languageNames = new[]
        {
            "Agda", "bash", "C", "C++", "cpp", "C#", "c-sharp", "CSS",
            "embedded-template", "Go", "Haskell", "HTML", "Java", "JavaScript",
            "JSDoc", "JSON", "Julia", "PHP", "Python", "QL", "Razor", "Ruby",
            "Rust", "Scala", "TSX", "TypeScript", "Verilog"
        }.ToList();

        if (OperatingSystem.IsWindows())
        {
            languageNames.AddRange(["OCaml", "Swift", "TOML", "TSQ"]);
        }

        foreach (var languageName in languageNames)
        {
            using Language language = new(languageName);
            Assert.NotNull(language);
        }
    }

    /// <summary>
    /// 验证 C# 符号提取会填充关键字段
    /// </summary>
    [Fact]
    public void Analyze_CSharpCode_ShouldPopulateCompleteSymbols()
    {
        var code = """
            using System.Threading.Tasks;

            [Service]
            public class UserService : BaseService, IUserService
            {
                private readonly EmailValidator _validator = new();

                public async Task<User> CreateUser(string name, string email)
                {
                    _validator.Validate(email);
                    return new User(name, email);
                }
            }
            """;

        var result = _analyzer.Analyze("UserService.cs", code, "csharp");

        Assert.True(result.Symbols.Count > 0);

        var classSymbol = Assert.Single(result.Symbols, symbol => symbol.Name == "UserService");
        Assert.Equal("class", classSymbol.Kind);
        Assert.Equal("BaseService", classSymbol.ParentClass);
        Assert.NotNull(classSymbol.Modifiers);
        Assert.Contains("public", classSymbol.Modifiers!);
        Assert.NotNull(classSymbol.BaseTypes);
        Assert.Contains("IUserService", classSymbol.BaseTypes!);
        Assert.NotNull(classSymbol.AttributeAnnotations);
        Assert.Contains(classSymbol.AttributeAnnotations!, attribute => attribute.Contains("Service", StringComparison.OrdinalIgnoreCase));

        var methodSymbol = Assert.Single(result.Symbols, symbol => symbol.Name == "CreateUser");
        Assert.Equal("method", methodSymbol.Kind);
        Assert.Equal("UserService", methodSymbol.ParentClass);
        Assert.NotNull(methodSymbol.Modifiers);
        Assert.Contains("public", methodSymbol.Modifiers!);
        Assert.Contains("async", methodSymbol.Modifiers!);
        Assert.Contains("CreateUser", methodSymbol.FullSignature);
        Assert.Contains("Task<User>", methodSymbol.FullSignature);
        Assert.True(methodSymbol.StartLine > 0);
        Assert.True(methodSymbol.EndLine >= methodSymbol.StartLine);
    }

    /// <summary>
    /// 验证 C# 调用边提取有效
    /// </summary>
    [Fact]
    public void Analyze_CSharpCode_ShouldExtractMethodCallEdges()
    {
        var code = """
            public class UserService
            {
                public void Save(string email)
                {
                    Validate(email);
                    Notify(email);
                }

                private void Validate(string email)
                {
                }

                private void Notify(string email)
                {
                }
            }
            """;

        var result = _analyzer.Analyze("UserService.cs", code, "csharp");
        var directCalls = result.CallEdges.Where(edge => edge.CallType == "direct").ToList();

        Assert.True(directCalls.Count > 0);
        Assert.All(directCalls, edge =>
        {
            Assert.False(string.IsNullOrWhiteSpace(edge.CallerSymbol));
            Assert.False(string.IsNullOrWhiteSpace(edge.CalleeSymbol));
            Assert.True(edge.Confidence >= 0.9);
        });

        Assert.Contains(directCalls, edge => edge.CallerSymbol == "UserService.Save" && edge.CalleeSymbol == "Validate");
        Assert.Contains(directCalls, edge => edge.CallerSymbol == "UserService.Save" && edge.CalleeSymbol == "Notify");
    }

    /// <summary>
    /// 验证设计模式提示不再为空
    /// </summary>
    [Fact]
    public void Analyze_PatternCode_ShouldReturnDesignPatternHints()
    {
        var code = """
            public interface IPaymentStrategy
            {
                void Pay();
            }

            public class WechatPaymentStrategy : IPaymentStrategy
            {
                public void Pay()
                {
                }
            }

            public class AlipayPaymentStrategy : IPaymentStrategy
            {
                public void Pay()
                {
                }
            }
            """;

        var result = _analyzer.Analyze("PaymentStrategies.cs", code, "csharp");

        Assert.NotEmpty(result.DesignPatternHints);
        Assert.Contains(result.DesignPatternHints, hint => hint.Contains("Strategy", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证空代码返回空结果
    /// </summary>
    [Fact]
    public void EmptyCode_ShouldReturnEmptyAstResult()
    {
        var result = _analyzer.Analyze("empty.cs", string.Empty, "csharp");

        Assert.Empty(result.Symbols);
        Assert.Empty(result.CallEdges);
        Assert.Empty(result.Chunks);
    }

    /// <summary>
    /// 验证不支持语言仍可安全回退
    /// </summary>
    [Fact]
    public void UnsupportedLanguage_ShouldFallbackSafely()
    {
        var result = _analyzer.Analyze("test.lua", "function hello() end", "lua");

        Assert.NotNull(result);
        Assert.Equal("lua", result.Language);
    }

    /// <summary>
    /// 验证 Heimdall 全量 C# 扫描产出足够规模的符号与调用边
    /// </summary>
    [Fact]
    public void HeimdallRepositoryScan_ShouldProduceLargeAstCounts()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var backendRoot = Path.Combine(repoRoot, "backend");
        if (!Directory.Exists(backendRoot))
        {
            return;
        }

        var files = Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var symbolCount = 0;
        var callEdgeCount = 0;
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var result = _analyzer.Analyze(file, source, "csharp");
            symbolCount += result.Symbols.Count;
            callEdgeCount += result.CallEdges.Count(edge => edge.CallType == "direct");
        }

        Assert.True(symbolCount > 500, $"符号数不足，实际 {symbolCount}");
        Assert.True(callEdgeCount > 100, $"调用边不足，实际 {callEdgeCount}");
    }
}
