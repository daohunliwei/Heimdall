using System.Text.Json;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Configuration;
using Heimdall.Api.Services.Providers;
using Heimdall.Api.Services.Repository;
using Heimdall.Api.Services.Utility;

namespace Heimdall.Api.Services.Rag;

/// <summary>
/// 仓库嵌入服务，负责仓库读取、切分、嵌入和本地索引缓存。
/// </summary>
public sealed class RepositoryEmbeddingService
{
    private readonly RepositoryAccessService _repositoryAccessService;
    private readonly ProviderRegistry _providerRegistry;
    private readonly HeimdallConfigService _configService;
    private readonly TextUtilityService _textUtilityService;

    /// <summary>
    /// 初始化仓库嵌入服务。
    /// </summary>
    public RepositoryEmbeddingService(
        RepositoryAccessService repositoryAccessService,
        ProviderRegistry providerRegistry,
        HeimdallConfigService configService,
        TextUtilityService textUtilityService)
    {
        _repositoryAccessService = repositoryAccessService;
        _providerRegistry = providerRegistry;
        _configService = configService;
        _textUtilityService = textUtilityService;
    }

    /// <summary>
    /// 为指定仓库准备嵌入文档集合。
    /// </summary>
    public async Task<List<EmbeddedDocument>> PrepareDocumentsAsync(
        string repoUrlOrPath,
        string repoType,
        string? accessToken,
        List<string> excludedDirs,
        List<string> excludedFiles,
        List<string> includedDirs,
        List<string> includedFiles,
        CancellationToken cancellationToken)
    {
        var embedderType = _configService.GetEmbedderType();
        var filterSignature = _textUtilityService.ToSha256(JsonSerializer.Serialize(new
        {
            excludedDirs,
            excludedFiles,
            includedDirs,
            includedFiles
        }));
        var cachePath = _repositoryAccessService.GetRepositoryDatabaseCachePath(repoUrlOrPath, repoType, embedderType, filterSignature);
        if (File.Exists(cachePath))
        {
            var content = await File.ReadAllTextAsync(cachePath, cancellationToken);
            var cached = JsonSerializer.Deserialize<RepositoryIndexCache>(content);
            if (cached is not null && cached.Documents.Count > 0)
            {
                return cached.Documents;
            }
        }

        var repositoryPath = await _repositoryAccessService.PrepareRepositoryAsync(repoUrlOrPath, repoType, accessToken, cancellationToken);
        var rawDocuments = await _repositoryAccessService.ReadRepositoryDocumentsAsync(
            repositoryPath,
            embedderType,
            excludedDirs,
            excludedFiles,
            includedDirs,
            includedFiles,
            cancellationToken);
        var splitter = _configService.GetEmbedderConfig().TextSplitter;
        var embeddingProvider = _providerRegistry.ResolveEmbeddingProvider();
        var transformedDocuments = new List<EmbeddedDocument>();

        foreach (var document in rawDocuments)
        {
            List<string> chunks;
            var splitBy = splitter.SplitBy.Trim().ToLowerInvariant();
            if (splitBy is "char" or "chars" or "character" or "characters")
            {
                chunks = _textUtilityService.SplitByCharacters(document.Text, splitter.ChunkSize, splitter.ChunkOverlap);
            }
            else if (splitBy is "line" or "lines")
            {
                chunks = _textUtilityService.SplitByLines(document.Text, splitter.ChunkSize, splitter.ChunkOverlap);
            }
            else
            {
                chunks = _textUtilityService.SplitByWords(document.Text, splitter.ChunkSize, splitter.ChunkOverlap);
            }

            if (chunks.Count == 0)
            {
                chunks.Add(document.Text);
            }

            var maxChunkChars = embeddingProvider.EmbedderType == "ollama" ? 2000 : Math.Max(2000, splitter.ChunkSize * 20);
            var overlapChars = embeddingProvider.EmbedderType == "ollama"
                ? 200
                : Math.Min(maxChunkChars / 2, splitter.ChunkOverlap * 20);
            var normalized = new List<string>();
            foreach (var chunk in chunks)
            {
                if (chunk.Length <= maxChunkChars)
                {
                    normalized.Add(chunk);
                }
                else
                {
                    normalized.AddRange(_textUtilityService.SplitByCharacters(chunk, maxChunkChars, overlapChars));
                }
            }

            chunks = normalized.Count > 0 ? normalized : chunks;

            var chunkVectors = new List<(string Chunk, float[] Vector)>();
            if (embeddingProvider.EmbedderType == "ollama")
            {
                foreach (var chunk in chunks)
                {
                    try
                    {
                        var vector = await embeddingProvider.EmbedAsync(chunk, cancellationToken);
                        if (vector.Length > 0)
                        {
                            chunkVectors.Add((chunk, vector));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                var vectors = await embeddingProvider.EmbedBatchAsync(chunks, cancellationToken);
                for (var index = 0; index < chunks.Count && index < vectors.Count; index++)
                {
                    if (vectors[index].Length == 0)
                    {
                        continue;
                    }

                    chunkVectors.Add((chunks[index], vectors[index]));
                }
            }

            foreach (var (chunk, vector) in chunkVectors)
            {
                transformedDocuments.Add(new EmbeddedDocument
                {
                    FilePath = document.FilePath,
                    FileType = document.FileType,
                    IsCode = document.IsCode,
                    IsImplementation = document.IsImplementation,
                    Text = chunk,
                    TokenCount = _textUtilityService.EstimateTokenCount(chunk),
                    Vector = vector
                });
            }
        }

        var validatedDocuments = ValidateAndFilterEmbeddings(transformedDocuments);
        if (validatedDocuments.Count == 0)
        {
            throw new InvalidOperationException("No valid documents with embeddings found. Cannot create retriever.");
        }

        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var cache = new RepositoryIndexCache
        {
            Repository = repoUrlOrPath,
            EmbedderType = embedderType,
            FilterSignature = filterSignature,
            Documents = validatedDocuments
        };
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return validatedDocuments;
    }

    /// <summary>
    /// 过滤掉向量维度不一致的文档。
    /// </summary>
    private static List<EmbeddedDocument> ValidateAndFilterEmbeddings(List<EmbeddedDocument> documents)
    {
        if (documents.Count == 0)
        {
            return new List<EmbeddedDocument>();
        }

        var grouped = documents
            .Where(document => document.Vector.Length > 0)
            .GroupBy(document => document.Vector.Length)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();
        if (grouped is null)
        {
            return new List<EmbeddedDocument>();
        }

        var targetSize = grouped.Key;
        return documents.Where(document => document.Vector.Length == targetSize).ToList();
    }
}
