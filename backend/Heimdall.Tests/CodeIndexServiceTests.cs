using Heimdall.Core.Services.Repository;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Tests;

public class CodeIndexServiceTests
{
    private readonly CodeIndexService _service = new(
        new NullLogger<CodeIndexService>());

    [Fact]
    public void IndexRepository_ShouldFindCsharpFiles()
    {
        // 使用 Heimdall 项目自身作为测试数据
        var repoPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "Heimdall.Core"));

        if (!Directory.Exists(repoPath))
            return; // 路径变化时跳过

        var result = _service.IndexRepository(repoPath);

        Assert.NotNull(result);
        Assert.True(result.SourceFileCount > 0,
            $"应有至少 1 个源文件，实际：{result.SourceFileCount}");
        Assert.Contains("csharp", result.Entries
            .Select(e => e.Language).Distinct());
        Assert.True(result.ModuleNames.Count > 0);
    }

    [Fact]
    public void ChunkFile_ShouldReturnChunks()
    {
        // 写一个临时 C# 文件来测试分块
        var tempFile = Path.GetTempFileName() + ".cs";
        try
        {
            var code = """
            public class UserService
            {
                public User GetById(int id)
                {
                    return _repo.Find(id);
                }

                public void Save(User user)
                {
                    _repo.Save(user);
                }
            }

            public interface IUserRepo
            {
                User Find(int id);
                void Save(User user);
            }
            """;
            File.WriteAllText(tempFile, code);

            var chunks = _service.ChunkFile(tempFile, "csharp");

            Assert.True(chunks.Count > 0,
                $"应有至少 1 个代码块，实际：{chunks.Count}");
            // 第一块应包含类定义
            Assert.Contains("UserService", chunks[0].Content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
