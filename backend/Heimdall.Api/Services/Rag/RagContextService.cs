using Heimdall.Api.Models;
using Heimdall.Api.Services.Providers;
using Heimdall.Api.Services.Repository;
using Heimdall.Api.Services.Utility;

namespace Heimdall.Api.Services.Rag;

/// <summary>
/// RAG 上下文服务，负责检索上下文、读取文件内容和处理大输入降级。
/// </summary>
public sealed class RagContextService
{
    private readonly RepositoryEmbeddingService _repositoryEmbeddingService;
    private readonly RepositoryAccessService _repositoryAccessService;
    private readonly ProviderRegistry _providerRegistry;
    private readonly TextUtilityService _textUtilityService;

    /// <summary>
    /// 初始化 RAG 上下文服务。
    /// </summary>
    public RagContextService(
        RepositoryEmbeddingService repositoryEmbeddingService,
        RepositoryAccessService repositoryAccessService,
        ProviderRegistry providerRegistry,
        TextUtilityService textUtilityService)
    {
        _repositoryEmbeddingService = repositoryEmbeddingService;
        _repositoryAccessService = repositoryAccessService;
        _providerRegistry = providerRegistry;
        _textUtilityService = textUtilityService;
    }

    /// <summary>
    /// 构建聊天上下文。
    /// </summary>
    public async Task<ChatContextResult> BuildContextAsync(
        ChatCompletionRequest request,
        ConversationMemoryService memoryService,
        CancellationToken cancellationToken)
    {
        var inputTooLarge = false;
        if (request.Messages.Count > 0)
        {
            var lastMessage = request.Messages[^1];
            if (_textUtilityService.EstimateTokenCount(lastMessage.Content) > 8000)
            {
                inputTooLarge = true;
            }
        }

        var fileContent = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            try
            {
                fileContent = await _repositoryAccessService.GetFileContentAsync(
                    request.RepoUrl,
                    request.FilePath,
                    request.Type ?? "github",
                    request.Token,
                    cancellationToken);
            }
            catch
            {
                fileContent = string.Empty;
            }
        }

        if (inputTooLarge)
        {
            return new ChatContextResult
            {
                ContextText = string.Empty,
                FileContent = fileContent,
                InputTooLarge = true,
                MemoryTurns = memoryService.GetTurns().ToList()
            };
        }

        var excludedDirs = _textUtilityService.ParseMultiLineList(request.ExcludedDirs);
        var excludedFiles = _textUtilityService.ParseMultiLineList(request.ExcludedFiles);
        var includedDirs = _textUtilityService.ParseMultiLineList(request.IncludedDirs);
        var includedFiles = _textUtilityService.ParseMultiLineList(request.IncludedFiles);
        List<EmbeddedDocument> documents;
        float[] queryVector;
        try
        {
            documents = await _repositoryEmbeddingService.PrepareDocumentsAsync(
                request.RepoUrl,
                request.Type ?? "github",
                request.Token,
                excludedDirs,
                excludedFiles,
                includedDirs,
                includedFiles,
                cancellationToken);

            var query = request.Messages[^1].Content;
            var retrievalQuery = !string.IsNullOrWhiteSpace(request.FilePath)
                ? $"Contexts related to {request.FilePath}"
                : query;
            var embeddingProvider = _providerRegistry.ResolveEmbeddingProvider();
            queryVector = await embeddingProvider.EmbedAsync(retrievalQuery, cancellationToken);
        }
        catch
        {
            return new ChatContextResult
            {
                ContextText = string.Empty,
                FileContent = fileContent,
                InputTooLarge = false,
                MemoryTurns = memoryService.GetTurns().ToList()
            };
        }

        var topDocuments = RankDocuments(documents, queryVector, 20);

        var grouped = topDocuments.GroupBy(document => document.FilePath);
        var contextParts = grouped.Select(group =>
        {
            var content = string.Join("\n\n", group.Select(document => document.Text));
            return $"## File Path: {group.Key}\n\n{content}";
        });

        return new ChatContextResult
        {
            ContextText = string.Join("\n\n----------\n\n", contextParts),
            FileContent = fileContent,
            InputTooLarge = false,
            MemoryTurns = memoryService.GetTurns().ToList()
        };
    }

    /// <summary>
    /// 使用余弦相似度进行排序。
    /// </summary>
    private static List<EmbeddedDocument> RankDocuments(List<EmbeddedDocument> documents, float[] queryVector, int topK)
    {
        return documents
            .Select(document => new
            {
                Document = document,
                Score = CalculateScore(document, queryVector)
            })
            .OrderByDescending(item => item.Score)
            .Take(topK)
            .Select(item => item.Document)
            .ToList();
    }

    /// <summary>
    /// 计算文档得分。
    /// </summary>
    private static double CalculateScore(EmbeddedDocument document, float[] queryVector)
    {
        if (document.Vector.Length == 0 || queryVector.Length == 0 || document.Vector.Length != queryVector.Length)
        {
            return 0;
        }

        double dot = 0;
        double queryNorm = 0;
        double documentNorm = 0;
        for (var index = 0; index < queryVector.Length; index++)
        {
            dot += queryVector[index] * document.Vector[index];
            queryNorm += queryVector[index] * queryVector[index];
            documentNorm += document.Vector[index] * document.Vector[index];
        }

        var score = dot / (Math.Sqrt(queryNorm) * Math.Sqrt(documentNorm) + 1e-10);
        if (document.IsImplementation)
        {
            score += 0.25;
        }

        return score;
    }
}
