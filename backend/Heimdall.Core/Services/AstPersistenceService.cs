using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Repository;
using Heimdall.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services;

public class AstPersistenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CodeIndexService _codeIndexService;
    private readonly WorkspaceService _workspace;
    private readonly ILogger<AstPersistenceService> _logger;

    private const string ProjectionFormatVersion = "2.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AstPersistenceService(
        IServiceScopeFactory scopeFactory,
        CodeIndexService codeIndexService,
        WorkspaceService workspace,
        ILogger<AstPersistenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _codeIndexService = codeIndexService;
        _workspace = workspace;
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

            // 双写：Workspace 文件系统
            version.AstDirPath = await WriteAstToWorkspaceAsync(version.Id, projection);

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
    /// 将 AST 解析结果写入 Workspace 文件系统，返回 ast_dir_path。
    /// </summary>
    private async Task<string> WriteAstToWorkspaceAsync(Guid astVersionId, AstPersistenceProjection projection)
    {
        var astDir = _workspace.GetAstDir(astVersionId);
        var filesDir = Path.Combine(astDir, "files");
        Directory.CreateDirectory(filesDir);

        // 每个文件双写：
        //   {hash}.cst  = 原始 Tree-sitter S-expression（canonical source，不可丢弃）
        //   {hash}.json = 解析后结构化数据（symbols/callEdges/chunks，供 Tool 快速查询）
        foreach (var fr in projection.FileResults)
        {
            var fileHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(fr.FilePath)))[..16];

            // 原始 CST S-expression（仅 Tree-sitter 成功解析的文件有）
            if (!string.IsNullOrEmpty(fr.CstSExpression))
            {
                var cstPath = Path.Combine(filesDir, $"{fileHash}.cst");
                await File.WriteAllTextAsync(cstPath, fr.CstSExpression);
            }

            // 解析后结构化数据（始终写入：symbols, callEdges, chunks, patterns）
            var analysisPath = Path.Combine(filesDir, $"{fileHash}.json");
            var analysisJson = JsonSerializer.Serialize(fr, JsonOptions);
            await File.WriteAllTextAsync(analysisPath, analysisJson);
        }

        // manifest.json
        var manifest = new
        {
            total_files = projection.TotalFiles,
            total_symbols = projection.TotalSymbols,
            total_call_edges = projection.TotalCallEdges,
            total_chunks = projection.TotalChunks,
            files = projection.FileList.Select(f => new
            {
                path = f.Path,
                language = f.Language,
                symbol_count = f.SymbolCount
            })
        };
        await File.WriteAllTextAsync(
            Path.Combine(astDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, PrettyJsonOptions));

        // symbols.json
        await File.WriteAllTextAsync(
            Path.Combine(astDir, "symbols.json"),
            JsonSerializer.Serialize(projection.SymbolNames, PrettyJsonOptions));

        _logger.LogInformation("AST Workspace 文件写入完成: {Dir}", astDir);
        return astDir;
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
