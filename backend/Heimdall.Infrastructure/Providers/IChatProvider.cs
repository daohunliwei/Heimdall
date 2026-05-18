using Heimdall.Infrastructure.Models;

namespace Heimdall.Infrastructure.Providers;

public interface IChatProvider
{
    string ProviderId { get; }
    Task<string> GenerateAsync(ProviderChatRequest request, CancellationToken cancellationToken);
}
