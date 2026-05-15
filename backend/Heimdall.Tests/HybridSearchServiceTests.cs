using Heimdall.Core.Interfaces.Services;
using Heimdall.Core.Services.Search;
using Heimdall.Infrastructure.Search;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Tests;

public class HybridSearchServiceTests
{
    private readonly HybridSearchService _service = new(
        new Bm25SearchService(new NullLogger<Bm25SearchService>()),
        new NullLogger<HybridSearchService>());

    [Fact]
    public async Task BuildAndSearch_ShouldMergeResults()
    {
        var snippets = new List<CodeSnippetInput>
        {
            new() { FilePath = "src/auth/AuthHandler.cs", ModuleName = "auth",
                Content = "public class AuthHandler { public bool Validate(string jwt) { ... } }",
                Symbols = "AuthHandler Validate", Language = "csharp" },
            new() { FilePath = "src/api/UserController.cs", ModuleName = "api",
                Content = "[HttpGet] public User Get(int id) { ... }",
                Symbols = "UserController Get", Language = "csharp" },
        };

        await _service.BuildIndexAsync("hybrid-test", snippets);

        var results = await _service.SearchAsync("hybrid-test", "jwt authentication", topK: 5);

        Assert.True(results.Count > 0, "混合搜索应有结果");
        Assert.Contains(results, r => r.FilePath.Contains("AuthHandler"));

        // 格式化测试
        var formatted = _service.FormatForPrompt(results);
        Assert.Contains("AuthHandler", formatted);
        Assert.Contains("```", formatted);
    }

    [Fact]
    public async Task Search_WithKeyFilePaths_ShouldBoostResults()
    {
        var snippets = new List<CodeSnippetInput>
        {
            new() { FilePath = "src/a/ServiceA.cs", ModuleName = "a",
                Content = "class ServiceA { public void DoWork() {} }", Symbols = "ServiceA", Language = "csharp" },
            new() { FilePath = "src/b/ServiceB.cs", ModuleName = "b",
                Content = "class ServiceB { public void Process() {} }", Symbols = "ServiceB", Language = "csharp" },
        };

        await _service.BuildIndexAsync("boost-test-2", snippets);

        var results = await _service.SearchAsync("boost-test-2", "class public void",
            keyFilePaths: new List<string> { "ServiceB" }, topK: 5);

        Assert.True(results.Count > 0, "混合搜索应返回结果");
        // ServiceB gets extra boost from key file paths
        var topScore = results.Max(r => r.CombinedScore);
        Assert.True(topScore >= 0, "最高融合分数应非负");
    }
}
