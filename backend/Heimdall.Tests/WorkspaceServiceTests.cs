using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Tests;

public class WorkspaceServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"heimdall_test_{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("HEIMDALL_WORKSPACE", _tempRoot);
        var config = new WorkspaceConfig();
        Environment.SetEnvironmentVariable("HEIMDALL_WORKSPACE", null);
        _service = new WorkspaceService(config, NullLogger<WorkspaceService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    [Fact]
    public void RootPath_ReturnsConfiguredPath()
    {
        Assert.Equal(_tempRoot, _service.RootPath);
    }

    [Fact]
    public void EnsureDirectories_CreatesAllTopLevelDirs()
    {
        _service.EnsureDirectories();

        Assert.True(Directory.Exists(_tempRoot));
        Assert.True(Directory.Exists(Path.Combine(_tempRoot, "repos")));
        Assert.True(Directory.Exists(Path.Combine(_tempRoot, "ast")));
        Assert.True(Directory.Exists(Path.Combine(_tempRoot, "wiki")));
        Assert.True(Directory.Exists(Path.Combine(_tempRoot, "artifacts")));
        Assert.True(Directory.Exists(Path.Combine(_tempRoot, "logs")));
        Assert.True(Directory.Exists(Path.Combine(_tempRoot, "cache")));
    }

    [Fact]
    public void GetRepoPath_ReturnsCorrectPath()
    {
        var path = _service.GetRepoPath("owner", "repo");
        Assert.Equal(Path.Combine(_tempRoot, "repos", "owner_repo"), path);
    }

    [Fact]
    public void GetAstDir_UsesGuidPrefix()
    {
        var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var path = _service.GetAstDir(id);
        var expected = Path.Combine(_tempRoot, "ast", "12345678");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetWikiDir_UsesGuidPrefix()
    {
        var id = Guid.Parse("abcdefab-1234-1234-1234-123456789abc");
        var path = _service.GetWikiDir(id);
        var expected = Path.Combine(_tempRoot, "wiki", "abcdefab");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetArtifactDir_UsesGuidPrefix()
    {
        var id = Guid.Parse("deadbeef-1234-1234-1234-123456789abc");
        var path = _service.GetArtifactDir(id);
        var expected = Path.Combine(_tempRoot, "artifacts", "deadbeef");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetLogDir_UsesGuidPrefix()
    {
        var id = Guid.Parse("cafebabe-1234-1234-1234-123456789abc");
        var path = _service.GetLogDir(id);
        var expected = Path.Combine(_tempRoot, "logs", "cafebabe");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetCacheDir_ReturnsCorrectPath()
    {
        var path = _service.GetCacheDir("bm25");
        Assert.Equal(Path.Combine(_tempRoot, "cache", "bm25"), path);
    }

    [Fact]
    public async Task ReadOrRegenerate_ReadsExistingFile()
    {
        var filePath = Path.Combine(_tempRoot, "test.txt");
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(filePath, "existing content");

        var callCount = 0;
        var result = await _service.ReadOrRegenerateAsync(filePath, () =>
        {
            callCount++;
            return Task.FromResult("regenerated");
        });

        Assert.Equal("existing content", result);
        Assert.Equal(0, callCount); // Regenerate was not called
    }

    [Fact]
    public async Task ReadOrRegenerate_TriggersRegenerationWhenFileMissing()
    {
        var filePath = Path.Combine(_tempRoot, "nonexistent.txt");

        var callCount = 0;
        var result = await _service.ReadOrRegenerateAsync(filePath, () =>
        {
            callCount++;
            return Task.FromResult("regenerated content");
        });

        Assert.Equal("regenerated content", result);
        Assert.Equal(1, callCount);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task ReadOrRegenerate_Generic_DeserializesJson()
    {
        _service.EnsureDirectories();
        var filePath = Path.Combine(_tempRoot, "data.json");
        var data = new TestData { Name = "test", Value = 42 };
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(filePath, json);

        var result = await _service.ReadOrRegenerateJsonAsync(filePath,
            () => Task.FromResult(new TestData { Name = "fallback", Value = 0 }));

        Assert.Equal("test", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task WriteFileAsync_CreatesDirectoryAndWrites()
    {
        var filePath = Path.Combine(_tempRoot, "subdir", "nested", "file.txt");
        await _service.WriteFileAsync(filePath, "hello world");

        Assert.True(File.Exists(filePath));
        Assert.Equal("hello world", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task AppendLineAsync_CreatesDirectoryAndAppends()
    {
        var filePath = Path.Combine(_tempRoot, "logs", "test.jsonl");
        await _service.AppendLineAsync(filePath, "line1");
        await _service.AppendLineAsync(filePath, "line2");

        var lines = await File.ReadAllLinesAsync(filePath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
    }

    [Fact]
    public async Task ReadWithFallback_UsesFileWhenExists()
    {
        var filePath = Path.Combine(_tempRoot, "fallback.txt");
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(filePath, "file content");

        var dbCalled = false;
        var result = await _service.ReadWithFallbackAsync(filePath, () =>
        {
            dbCalled = true;
            return Task.FromResult<string?>("db content");
        });

        Assert.Equal("file content", result);
        Assert.False(dbCalled);
    }

    [Fact]
    public async Task ReadWithFallback_FallsBackToDbAndWritesFile()
    {
        var filePath = Path.Combine(_tempRoot, "fallback2.txt");

        var result = await _service.ReadWithFallbackAsync(filePath,
            () => Task.FromResult<string?>("db content"));

        Assert.Equal("db content", result);
        Assert.True(File.Exists(filePath));
        Assert.Equal("db content", await File.ReadAllTextAsync(filePath));
    }

    private class TestData
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }
}
