namespace Heimdall.Infrastructure.Providers;

public interface IEmbeddingProvider
{
    string EmbedderType { get; }
    Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken);
    Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}
