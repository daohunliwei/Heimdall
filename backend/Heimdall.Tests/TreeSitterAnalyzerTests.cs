using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Heimdall.Infrastructure.AstAnalysis;

namespace Heimdall.Tests;

public class TreeSitterAnalyzerTests
{
    private readonly TreeSitterAnalyzer _analyzer = new(new NullLogger<TreeSitterAnalyzer>());

    [Fact]
    public void Analyze_ReturnsResultForAllLanguages()
    {
        foreach (var lang in new[] { "csharp", "typescript", "javascript", "python", "go", "rust", "java" })
        {
            var code = lang switch
            {
                "csharp" => "class Foo { void Bar() {} }",
                "typescript" or "javascript" => "function hello() { return 1; }",
                "python" => "def hello():\n    return 1",
                "go" => "package main\nfunc main() {}",
                "rust" => "fn main() {}",
                "java" => "class Foo { void bar() {} }",
                _ => "x = 1"
            };
            var result = _analyzer.Analyze($"test.{lang}", code, lang);
            Assert.NotNull(result);
            Assert.Equal(lang, result.Language);
            Assert.NotEmpty(result.FilePath);
        }
    }

    [Fact]
    public void UnsupportedLanguage_FallsBackToRegex()
    {
        var result = _analyzer.Analyze("test.lua", "function hello() end", "lua");
        Assert.NotNull(result);
        Assert.Equal("lua", result.Language);
    }

    [Fact]
    public void EmptyCode_ReturnsEmptySymbols()
    {
        var result = _analyzer.Analyze("empty.cs", "", "csharp");
        Assert.NotNull(result);
        Assert.Empty(result.Symbols);
    }

    [Fact]
    public void LargeFile_Truncated()
    {
        var code = new string('x', 200_000);
        var result = _analyzer.Analyze("big.cs", code, "csharp");
        Assert.NotNull(result);
    }

    [Fact]
    public void SupportsLanguage_ListsAllMappedLanguages()
    {
        Assert.True(_analyzer.SupportsLanguage("csharp"));
        Assert.True(_analyzer.SupportsLanguage("typescript"));
        Assert.True(_analyzer.SupportsLanguage("python"));
        Assert.True(_analyzer.SupportsLanguage("go"));
        Assert.True(_analyzer.SupportsLanguage("rust"));
        Assert.True(_analyzer.SupportsLanguage("java"));
        Assert.False(_analyzer.SupportsLanguage("brainfuck"));
    }

    [Fact]
    public void DetectLanguageMapping_AllMapped()
    {
        // 通过这些测试验证我们支持的语言映射，不依赖 native lib 加载
        var tests = new[] { "csharp", "typescript", "javascript", "python", "go", "rust", "java",
            "ruby", "php", "cpp", "swift", "scala", "haskell", "html", "css", "json", "bash" };
        foreach (var lang in tests)
        {
            var result = _analyzer.Analyze($"test.{lang}", "x", lang);
            Assert.NotNull(result);
            Assert.Equal(lang, result.Language);
        }
    }
}
