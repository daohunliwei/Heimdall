using Heimdall.Core.Entities;
using Heimdall.Core.Models;
using Heimdall.Infrastructure.Models;
using Heimdall.Infrastructure.Utilities;
using Heimdall.Core.Interfaces.Repositories;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 任务请求工具方法集合。
/// </summary>
public sealed class TaskRequestUtilityService
{
    private readonly TextUtilityService _textUtility;
    private readonly IRepositoryConfigRepository _repoConfigRepo;

    public TaskRequestUtilityService(TextUtilityService textUtility, IRepositoryConfigRepository repoConfigRepo)
    {
        _textUtility = textUtility;
        _repoConfigRepo = repoConfigRepo;
    }

    public RepoInfo BuildRepoInfo(TaskEnqueueRequest request)
    {
        return new RepoInfo
        {
            Owner = "unknown",
            Repo = "unknown",
            Type = "github"
        };
    }

    public string ResolveLanguage(TaskEnqueueRequest request)
    {
        return request.Language ?? "zh";
    }

    public string? ResolveRepoUrl(TaskEnqueueRequest request)
    {
        if (request.RepositoryId.HasValue)
            return null; // 从 DB 查询
        return null;
    }

    public string ResolveLanguageDisplayName(TaskEnqueueRequest request)
    {
        return ResolveLanguage(request) switch
        {
            "zh" => "中文",
            "en" => "English",
            _ => "中文"
        };
    }

    public ProviderChatRequest BuildChatRequest(
        string provider, string model,
        List<Infrastructure.Models.ChatMessage> messages)
    {
        return new ProviderChatRequest
        {
            ProviderId = provider,
            Model = model,
            Prompt = string.Join("\n", messages.Select(m => m.Content))
        };
    }
}
