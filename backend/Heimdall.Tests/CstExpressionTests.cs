using Heimdall.Infrastructure.AstAnalysis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Tests;

public class CstExpressionTests
{
    [Fact]
    public void Analyze_CSharpCode_ShouldProduceNonEmptyCstExpression()
    {
        var analyzer = new TreeSitterAnalyzer(new NullLogger<TreeSitterAnalyzer>());
        var code = """
            public class Hello
            {
                public void World() { }
            }
            """;

        var result = analyzer.Analyze("test.cs", code, "csharp");

        Assert.False(string.IsNullOrEmpty(result.CstSExpression),
            "CstSExpression 不应为空——原始 CST S-expression 必须被保留");
        Assert.StartsWith("(compilation_unit", result.CstSExpression.TrimStart());
        Assert.True(result.CstSExpression.Length > 200,
            $"CstSExpression 应包含完整语法树，实际长度 {result.CstSExpression.Length}");
    }
}
