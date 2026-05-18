using System.Security.Cryptography;
using System.Text;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Providers;
using Heimdall.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Rag;

/// <summary>代码嵌入服务 — 对仓库文件分块、生成嵌入向量、写入 code_embedding_chunks</summary>
public class CodeEmbeddingService : ICodeEmbeddingService
{
    private readonly ICodeEmbeddingRepository _codeRepo;
    private readonly ProviderRegistry _providerRegistry;
    private readonly TextUtilityService _textUtility;
    private readonly ILogger<CodeEmbeddingService> _logger;

    public CodeEmbeddingService(
        ICodeEmbeddingRepository codeRepo,
        ProviderRegistry providerRegistry,
        TextUtilityService textUtility,
        ILogger<CodeEmbeddingService> logger)
    {
        _codeRepo = codeRepo;
        _providerRegistry = providerRegistry;
        _textUtility = textUtility;
        _logger = logger;
    }

    public async Task<int> EmbedRepositoryAsync(
        Guid repositoryVersionId, List<EmbeddedDocument> documents, CancellationToken ct = default)
    {
        var embedder = _providerRegistry.ResolveEmbeddingProvider();
        var embedderType = embedder.EmbedderType;
        var chunks = new List<CodeEmbeddingChunk>();

        foreach (var doc in documents)
        {
            ct.ThrowIfCancellationRequested();

            var language = DetectLanguage(doc.FilePath);
            var lines = doc.Text.Split('\n');
            var textChunks = _textUtility.SplitByLines(doc.Text, 80, 10);

            for (var i = 0; i < textChunks.Count; i++)
            {
                var chunkText = textChunks[i];
                if (string.IsNullOrWhiteSpace(chunkText)) continue;

                var startLine = i * 70 + 1; // approximate line range
                var endLine = Math.Min(startLine + chunkText.Split('\n').Length - 1, lines.Length);

                var contentHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(chunkText))).ToLowerInvariant();

                float[]? vector = null;
                byte[]? vectorBytes = null;
                try
                {
                    vector = await embedder.EmbedAsync(chunkText, ct);
                    vectorBytes = TextUtilityService.ConvertFloatsToBytes(vector);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "嵌入生成失败 File={Path} Chunk={Index}", doc.FilePath, i);
                }

                chunks.Add(new CodeEmbeddingChunk
                {
                    RepositoryVersionId = repositoryVersionId,
                    FilePath = doc.FilePath,
                    ChunkIndex = i,
                    ChunkType = doc.IsCode ? "code_block" : "file_summary",
                    Language = language,
                    StartLine = startLine,
                    EndLine = endLine,
                    ContentRaw = chunkText,
                    ContentNormalized = NormalizeCode(chunkText),
                    ContentHash = contentHash,
                    TokenCount = _textUtility.EstimateTokenCount(chunkText),
                    EmbeddingModel = embedderType,
                    EmbeddingVector = vectorBytes
                });
            }
        }

        if (chunks.Count > 0)
        {
            await _codeRepo.DeleteByVersionIdAsync(repositoryVersionId);
            await _codeRepo.AddRangeAsync(chunks);
        }

        _logger.LogInformation(
            "代码嵌入完成 VersionId={VersionId} Documents={DocCount} Chunks={ChunkCount}",
            repositoryVersionId, documents.Count, chunks.Count);

        return chunks.Count;
    }

    public async Task<int> GetChunkCountAsync(Guid repositoryVersionId, CancellationToken ct = default)
    {
        var chunks = await _codeRepo.GetByVersionIdAsync(repositoryVersionId);
        return chunks.Count;
    }

    private static string DetectLanguage(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".jsx" => "jsx",
            ".go" => "go",
            ".rs" => "rust",
            ".java" => "java",
            ".rb" => "ruby",
            ".php" => "php",
            ".c" or ".h" => "c",
            ".cpp" or ".hpp" or ".cc" => "cpp",
            ".sql" => "sql",
            ".html" => "html",
            ".css" => "css",
            ".json" => "json",
            ".xml" => "xml",
            ".yaml" or ".yml" => "yaml",
            ".md" => "markdown",
            ".sh" => "bash",
            _ => "text"
        };
    }

    private static string NormalizeCode(string code)
    {
        // 移除多余空行，保留代码语义
        var lines = code.Split('\n')
            .Select(l => l.TrimEnd())
            .ToList();

        // 移除连续空行
        var result = new List<string>();
        var prevEmpty = false;
        foreach (var line in lines)
        {
            var isEmpty = string.IsNullOrWhiteSpace(line);
            if (isEmpty && prevEmpty) continue;
            result.Add(line);
            prevEmpty = isEmpty;
        }

        return string.Join('\n', result).Trim();
    }
}
