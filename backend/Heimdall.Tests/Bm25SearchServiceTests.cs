using Heimdall.Infrastructure.Search;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Tests;

public class Bm25SearchServiceTests
{
    private readonly Bm25SearchService _service = new(
        new NullLogger<Bm25SearchService>());

    [Fact]
    public void BuildAndSearch_ShouldReturnRelevantResults()
    {
        var docs = new List<Bm25Document>
        {
            new() { FilePath = "src/auth/LoginService.cs", ModuleName = "auth",
                Content = "public class LoginService { public User Authenticate(string token) { ... } }",
                Symbols = "LoginService Authenticate", Title = "LoginService.cs", Language = "csharp" },
            new() { FilePath = "src/data/Repository.cs", ModuleName = "data",
                Content = "public class Repository { public T Find<T>(int id) { ... } }",
                Symbols = "Repository Find", Title = "Repository.cs", Language = "csharp" },
        };

        _service.BuildIndex("test", docs);

        // 搜索认证相关
        var results = _service.Search("test", "Authenticate token login", topK: 5);

        Assert.True(results.Count > 0, "应有搜索结果");
        Assert.Contains(results, r => r.FilePath.Contains("LoginService"));

        // 按模块过滤
        var filtered = _service.Search("test", "class", filterModule: "data", topK: 5);
        Assert.All(filtered, r => Assert.Equal("data", r.ModuleName));
    }

    [Fact]
    public void Search_WithNoIndex_ShouldReturnEmpty()
    {
        var results = _service.Search("nonexistent", "query");
        Assert.Empty(results);
    }
}
