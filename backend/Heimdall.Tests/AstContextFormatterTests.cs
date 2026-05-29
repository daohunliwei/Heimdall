using Heimdall.Core.Services;
using Heimdall.Infrastructure.AstAnalysis;

namespace Heimdall.Tests;

/// <summary>
/// AST 上下文格式化器测试 — 验证 L1/L2 格式输出和 AST 真实性检查
/// </summary>
public class AstContextFormatterTests
{
    private readonly AstContextFormatter _formatter = new();

    private static List<AstSymbol> CreateSampleSymbols()
    {
        return
        [
            new("UserService", "class", "public class UserService : BaseService, IUserService",
                "/src/Services/UserService.cs", 15, 120, "BaseService",
                ["public"], ["IUserService", "IDisposable"], ["[Service]"]),
            new("CreateUser", "method", "public async Task<User> CreateUser(string name, string email)",
                "/src/Services/UserService.cs", 30, 55, "UserService",
                ["public", "async"], null, null),
            new("GetById", "method", "public async Task<User> GetById(Guid id)",
                "/src/Services/UserService.cs", 60, 75, "UserService",
                ["public", "async"], null, null),
            new("IUserService", "interface", "public interface IUserService",
                "/src/Abstractions/IUserService.cs", 5, 15, null,
                ["public"], null, null),
            new("AuthController", "class", "public class AuthController : ControllerBase",
                "/src/Controllers/AuthController.cs", 10, 80, "ControllerBase",
                ["public"], null, ["[ApiController]"]),
            new("Register", "method", "public async Task<IActionResult> Register(RegisterRequest req)",
                "/src/Controllers/AuthController.cs", 25, 40, "AuthController",
                ["public", "async"], null, null),
        ];
    }

    private static List<AstCallEdge> CreateSampleCallEdges()
    {
        return
        [
            new("AuthController.Register", "/src/Controllers/AuthController.cs",
                "UserService.CreateUser", "", "direct", 0.9),
            new("UserService.CreateUser", "/src/Services/UserService.cs",
                "IUserRepository.AddAsync", "/src/Data/IUserRepository.cs", "direct", 0.7),
            new("UserService.GetById", "/src/Services/UserService.cs",
                "IUserRepository.FindAsync", "/src/Data/IUserRepository.cs", "direct", 0.7),
        ];
    }

    [Fact]
    public void FormatTypeHierarchy_WithSymbols_ShouldContainClassNameAndInheritance()
    {
        var symbols = CreateSampleSymbols();
        var output = _formatter.FormatTypeHierarchy(symbols);

        Assert.Contains("UserService", output);
        Assert.Contains("BaseService", output);
        Assert.Contains("IUserService", output);
        Assert.Contains("class", output);
        Assert.Contains("public", output);
        Assert.Contains("CreateUser", output);
        Assert.Contains("AuthController", output);
    }

    [Fact]
    public void FormatTypeHierarchy_EmptySymbols_ShouldReturnPlaceholder()
    {
        var output = _formatter.FormatTypeHierarchy([]);
        Assert.Contains("未提取到", output);
    }

    [Fact]
    public void FormatCallTopology_WithEdges_ShouldContainCallerChain()
    {
        var edges = CreateSampleCallEdges();
        var output = _formatter.FormatCallTopology(edges);

        Assert.Contains("AuthController.Register", output);
        Assert.Contains("UserService.CreateUser", output);
        Assert.Contains("IUserRepository.AddAsync", output);
        Assert.Contains("→", output);
    }

    [Fact]
    public void FormatCallTopology_EmptyEdges_ShouldReturnPlaceholder()
    {
        var output = _formatter.FormatCallTopology([]);
        Assert.Contains("未提取到", output);
    }

    [Fact]
    public void FormatDesignPatternEvidence_WithHints_ShouldContainPatternNameAndConfidence()
    {
        var hints = new List<string> { "Strategy|0.95|/src/IUserService.cs|IUserService:UserService,MockUserService" };
        var output = _formatter.FormatDesignPatternEvidence(hints);

        Assert.Contains("Strategy", output);
        Assert.Contains("95%", output);
        Assert.Contains("IUserService", output);
    }

    [Fact]
    public void FormatDesignPatternEvidence_EmptyHints_ShouldReturnPlaceholder()
    {
        var output = _formatter.FormatDesignPatternEvidence([]);
        Assert.Contains("未检测到", output);
    }

    [Fact]
    public void FormatPageCodeBlockContext_WithSymbol_ShouldContainL2Format()
    {
        var symbols = CreateSampleSymbols();
        var edges = CreateSampleCallEdges();
        var method = symbols.First(s => s.Name == "CreateUser");

        var output = _formatter.FormatPageCodeBlockContext(method, edges, [], compact: false);

        Assert.Contains("AST Context", output);
        Assert.Contains("CreateUser", output);
        Assert.Contains("Signature", output);
        Assert.Contains("async", output);
    }

    [Fact]
    public void FormatPageCodeBlockContext_Compact_ShouldBeSingleLine()
    {
        var symbols = CreateSampleSymbols();
        var method = symbols.First(s => s.Name == "CreateUser");

        var output = _formatter.FormatPageCodeBlockContext(method, [], [], compact: true);

        Assert.Contains("AST", output);
        Assert.Contains("CreateUser", output);
        // 紧凑格式应为单行
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void FoldStrategy_LowImportance_ShouldBeCompact()
    {
        var symbols = CreateSampleSymbols();
        var method = symbols.First(s => s.Name == "CreateUser");

        var output = _formatter.FormatPageCodeBlockContextWithFoldStrategy(method, [], [], importance: 3);

        Assert.Contains("AST", output);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void FoldStrategy_HighImportance_ShouldBeFullContext()
    {
        var symbols = CreateSampleSymbols();
        var edges = CreateSampleCallEdges();
        var method = symbols.First(s => s.Name == "CreateUser");

        var output = _formatter.FormatPageCodeBlockContextWithFoldStrategy(method, edges, [], importance: 8);

        Assert.Contains("AST Context", output);
        Assert.Contains("Signature", output);
    }

    [Fact]
    public void VerifyAstAuthenticity_RealSymbols_ShouldPass()
    {
        var symbols = CreateSampleSymbols();
        var edges = CreateSampleCallEdges();
        var content = "`UserService` 是核心服务类，`CreateUser` 方法负责创建用户。";

        var result = AstContextFormatter.VerifyAstAuthenticity(content, symbols, edges);

        Assert.Equal(0, result.FictionalCount);
        Assert.True(result.AuthenticityRate >= 0.9);
    }

    [Fact]
    public void VerifyAstAuthenticity_FictionalSymbol_ShouldBeDetected()
    {
        var symbols = CreateSampleSymbols();
        var edges = CreateSampleCallEdges();
        var content = "`NonExistentService` 是一个不存在的类，`CreateUser` 方法也有调用。";

        var result = AstContextFormatter.VerifyAstAuthenticity(content, symbols, edges);

        Assert.True(result.FictionalCount > 0);
        Assert.Contains("NonExistentService", result.FictionalReferences);
        Assert.DoesNotContain("CreateUser", result.FictionalReferences);
    }

    [Fact]
    public void VerifyAstAuthenticity_EmptySymbols_ShouldReturnEmpty()
    {
        var result = AstContextFormatter.VerifyAstAuthenticity("some content", [], []);

        Assert.Equal(0, result.TotalChecked);
        Assert.Equal(1.0, result.AuthenticityRate);
    }
}
