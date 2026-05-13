namespace Heimdall.Api.Services.Providers;

/// <summary>
/// 嵌入向量生成接口。
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// 嵌入器类型。
    /// </summary>
    string EmbedderType { get; }

    /// <summary>
    /// 生成单条文本的向量。
    /// </summary>
    Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken);

    /// <summary>
    /// 批量生成文本向量。
    /// </summary>
    Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}
