using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services;

public class AstPersistenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CodeIndexService _codeIndexService;
    private readonly ILogger<AstPersistenceService> _logger;

    private const string ProjectionFormatVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AstPersistenceService(
        IServiceScopeFactory scopeFactory,
        CodeIndexService codeIndexService,
        ILogger<AstPersistenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _codeIndexService = codeIndexService;
        _logger = logger;
    }

    /// <summary>
    /// 为目标仓库版本解析或复用 AST 版本，返回可引用的 AstVersion。
    /// </summary>
    public async Task<AstVersion> ResolveOrCreateAsync(
        RepositoryVersion repoVersion,
        string repoPath,
        string? branchName = null,
        string? commitSha = null,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAstVersionRepository>();
        var configFingerprint = ComputeConfigFingerprint();

        // 尝试复用已有成功版本
        var existing = await repo.GetByRepoVersionAndConfigAsync(repoVersion.Id, configFingerprint);
        if (existing != null)
        {
            _logger.LogInformation("复用已有 AST 版本 {AstVersionId} for RepoVersion {RepoVersionId}",
                existing.Id, repoVersion.Id);
            return existing;
        }

        // 新建 AST 版本
        var version = new AstVersion
        {
            RepositoryVersionId = repoVersion.Id,
            BranchName = branchName ?? repoVersion.BranchName,
            CommitSha = commitSha ?? repoVersion.CommitSha,
            ConfigFingerprint = configFingerprint,
            ProjectionFormatVersion = ProjectionFormatVersion,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("开始 AST 解析 RepoVersion={RepoVersionId} Path={Path}", repoVersion.Id, repoPath);

            var projection = _codeIndexService.BuildPersistenceProjection(repoPath);

            version.TotalFiles = projection.TotalFiles;
            version.TotalSymbols = projection.TotalSymbols;
            version.TotalCallEdges = projection.TotalCallEdges;
            version.TotalChunks = projection.TotalChunks;
            version.ResultJson = JsonSerializer.Serialize(projection.FileResults, JsonOptions);
            version.SymbolNamesJson = JsonSerializer.Serialize(projection.SymbolNames, JsonOptions);
            version.FileListJson = JsonSerializer.Serialize(projection.FileList, JsonOptions);
            version.Status = "success";
            version.CompletedAt = DateTime.UtcNow;

            await repo.InsertAsync(version);

            _logger.LogInformation("AST 版本持久化成功 {AstVersionId}: {Files} 文件, {Symbols} 符号",
                version.Id, version.TotalFiles, version.TotalSymbols);

            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AST 解析或持久化失败 RepoVersion={RepoVersionId}", repoVersion.Id);

            version.Status = "failed";
            version.ErrorMessage = ex.Message;
            version.CompletedAt = DateTime.UtcNow;

            try { await repo.InsertAsync(version); }
            catch (Exception insertEx)
            {
                _logger.LogError(insertEx, "AST 版本失败记录写入也失败");
            }

            throw;
        }
    }

    /// <summary>
    /// 计算解析配置指纹——基于投影格式版本。
    /// </summary>
    private static string ComputeConfigFingerprint()
    {
        var seed = $"{ProjectionFormatVersion}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexStringLower(hash)[..12];
    }
}
