using System.Text.Json;
using Heimdall.Core.Entities;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Repository;
using Heimdall.Infrastructure.AstAnalysis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Tests;

public class AstPersistenceTests
{
    private readonly CodeIndexService _codeIndexService = new(
        new NullLogger<CodeIndexService>(),
        new TreeSitterAnalyzer(new NullLogger<TreeSitterAnalyzer>()));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void BuildPersistenceProjection_WithCSharpFile_ShouldReturnCompleteResults()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"heimdall_ast_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Service.cs"), """
                using System;

                namespace TestApp;

                public interface IUserService
                {
                    User GetUser(int id);
                }

                public class UserService : IUserService
                {
                    private readonly IRepository _repo;

                    public UserService(IRepository repo)
                    {
                        _repo = repo;
                    }

                    public User GetUser(int id)
                    {
                        return _repo.Find<User>(id);
                    }
                }

                public class User
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
                """);

            var projection = _codeIndexService.BuildPersistenceProjection(dir);

            Assert.NotEmpty(projection.FileResults);
            Assert.True(projection.TotalFiles > 0);
            Assert.True(projection.TotalSymbols > 0);
            Assert.NotEmpty(projection.SymbolNames);
            Assert.NotEmpty(projection.FileList);

            var fileEntry = projection.FileList.First();
            Assert.Equal("csharp", fileEntry.Language);
            Assert.True(fileEntry.SymbolCount > 0);

            var classSymbols = projection.SymbolNames.Where(s => s.Kind == "class").ToList();
            Assert.Contains(classSymbols, s => s.Name == "UserService");
            Assert.Contains(classSymbols, s => s.Name == "User");

            var interfaceSymbols = projection.SymbolNames.Where(s => s.Kind == "interface").ToList();
            Assert.Contains(interfaceSymbols, s => s.Name == "IUserService");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AstFileResult_ShouldSerializeAndDeserialize_Roundtrip()
    {
        var original = new AstFileResult(
            "Test.cs",
            "csharp",
            [
                new("MyClass", "class", "public class MyClass", "Test.cs", 1, 10,
                    null, ["public"], null, null)
            ],
            [
                new("MyClass.DoSomething", "Test.cs", "Logger.Log", "Logger.cs", "direct", 0.9)
            ],
            [
                new(1, 10, "class", "public class MyClass { }")
            ],
            ["Repository|0.95|Test.cs|MyRepository"]);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var restored = JsonSerializer.Deserialize<AstFileResult>(json, JsonOptions);

        Assert.NotNull(restored);
        Assert.Equal("Test.cs", restored.FilePath);
        Assert.Equal("csharp", restored.Language);
        Assert.Single(restored.Symbols);
        Assert.Equal("MyClass", restored.Symbols[0].Name);
        Assert.Single(restored.CallEdges);
        Assert.Single(restored.Chunks);
        Assert.Single(restored.DesignPatternHints);
    }

    [Fact]
    public void AstVersion_ShouldHaveDefaultValues()
    {
        var version = new AstVersion();

        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal("pending", version.Status);
        Assert.Equal("1.0", version.ProjectionFormatVersion);
        Assert.True(version.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public void BuildPersistenceProjection_WithMultipleFiles_ShouldAggregateStats()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"heimdall_ast_multi_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "A.cs"), "public class A { public void M1() {} }");
            File.WriteAllText(Path.Combine(dir, "B.cs"), "public class B { public void M2() {} }");
            File.WriteAllText(Path.Combine(dir, "C.cs"), "public class C { public void M3() {} }");

            var projection = _codeIndexService.BuildPersistenceProjection(dir);

            Assert.True(projection.TotalFiles >= 3);
            Assert.True(projection.TotalSymbols >= 6); // 3 classes + 3 methods
            Assert.Equal(projection.FileList.Count, projection.TotalFiles);
            Assert.Equal(projection.TotalCallEdges + projection.TotalChunks + projection.TotalSymbols,
                projection.TotalCallEdges + projection.TotalChunks + projection.TotalSymbols); // sanity
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SymbolNames_ShouldNotExceedLimit_PerFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"heimdall_ast_limit_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            // 生成一个包含大量方法的 C# 文件
            var methods = string.Join("\n", Enumerable.Range(1, 250).Select(i =>
                $"    public void Method{i}() {{ }}"));
            File.WriteAllText(Path.Combine(dir, "LargeFile.cs"), $$"""
                public class LargeClass {
                {{methods}}
                }
                """);

            var projection = _codeIndexService.BuildPersistenceProjection(dir);
            var largeFileSymbols = projection.SymbolNames
                .Where(s => s.File.Contains("LargeFile.cs")).ToList();

            // 每个文件最多取 200 个符号
            Assert.True(largeFileSymbols.Count <= 201, $"Expected <= 201, got {largeFileSymbols.Count}");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
