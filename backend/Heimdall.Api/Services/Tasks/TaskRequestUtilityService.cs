using Heimdall.Api.Models;
using Heimdall.Api.Services.Configuration;
using Heimdall.Api.Services.Utility;

namespace Heimdall.Api.Services.Tasks;

/// <summary>
/// 任务请求辅助服务，负责解析仓库与语言上下文。
/// </summary>
public sealed class TaskRequestUtilityService
{
    private readonly HeimdallConfigService _configService;
    private readonly TextUtilityService _textUtilityService;

    /// <summary>
    /// 初始化请求辅助服务。
    /// </summary>
    public TaskRequestUtilityService(HeimdallConfigService configService, TextUtilityService textUtilityService)
    {
        _configService = configService;
        _textUtilityService = textUtilityService;
    }

    /// <summary>
    /// 构建仓库信息。
    /// </summary>
    public RepoInfo BuildRepoInfo(TaskRequestBase request)
    {
        var repoType = ResolveRepoType(request);
        var repoUrl = ResolveRepoUrl(request);
        var owner = ResolveOwner(request, repoUrl, repoType);
        var repo = ResolveRepo(request, repoUrl);

        return new RepoInfo
        {
            Owner = owner,
            Repo = repo,
            Type = repoType,
            RepoUrl = repoType == "local" ? repoUrl : repoUrl,
            Token = request.Token,
            LocalPath = repoType == "local" ? repoUrl : null
        };
    }

    /// <summary>
    /// 解析最终仓库地址。
    /// </summary>
    public string ResolveRepoUrl(TaskRequestBase request)
    {
        if (!string.IsNullOrWhiteSpace(request.RepoUrl) &&
            !request.RepoUrl.Contains("example/", StringComparison.OrdinalIgnoreCase))
        {
            return request.RepoUrl.Trim();
        }

        var repoType = ResolveRepoType(request);
        if (repoType == "local")
        {
            return request.RepoUrl?.Trim() ?? string.Empty;
        }

        var owner = ResolveOwner(request, request.RepoUrl, repoType);
        var repo = ResolveRepo(request, request.RepoUrl);
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            return request.RepoUrl?.Trim() ?? string.Empty;
        }

        return repoType switch
        {
            "gitlab" => $"https://gitlab.com/{owner}/{repo}",
            "bitbucket" => $"https://bitbucket.org/{owner}/{repo}",
            _ => $"https://github.com/{owner}/{repo}"
        };
    }

    /// <summary>
    /// 解析语言代码。
    /// </summary>
    public string ResolveLanguage(TaskRequestBase request)
    {
        var languageConfig = _configService.GetLanguageConfig();
        if (string.IsNullOrWhiteSpace(request.Language))
        {
            return languageConfig.Default;
        }

        var language = request.Language.Trim();
        return languageConfig.SupportedLanguages.ContainsKey(language)
            ? language
            : languageConfig.Default;
    }

    /// <summary>
    /// 解析语言展示名。
    /// </summary>
    public string ResolveLanguageDisplayName(TaskRequestBase request)
    {
        var language = ResolveLanguage(request);
        var languageConfig = _configService.GetLanguageConfig();
        return languageConfig.SupportedLanguages.TryGetValue(language, out var displayName)
            ? displayName
            : "English";
    }

    /// <summary>
    /// 构建聊天请求。
    /// </summary>
    public ChatCompletionRequest BuildChatRequest(
        TaskRequestBase request,
        IReadOnlyCollection<ChatMessage> messages,
        string? filePath = null)
    {
        return new ChatCompletionRequest
        {
            RepoUrl = ResolveRepoUrl(request),
            Messages = messages.ToList(),
            FilePath = filePath,
            Token = request.Token,
            Type = ResolveRepoType(request),
            Provider = request.Provider,
            Model = request.Model,
            CustomModel = request.CustomModel,
            Language = ResolveLanguage(request),
            ExcludedDirs = request.ExcludedDirs,
            ExcludedFiles = request.ExcludedFiles,
            IncludedDirs = request.IncludedDirs,
            IncludedFiles = request.IncludedFiles
        };
    }

    private string ResolveRepoType(TaskRequestBase request)
    {
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            return request.Type.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.RepoUrl) && Directory.Exists(request.RepoUrl))
        {
            return "local";
        }

        return "github";
    }

    private string ResolveOwner(TaskRequestBase request, string? repoUrl, string repoType)
    {
        if (!string.IsNullOrWhiteSpace(request.Owner))
        {
            return request.Owner.Trim();
        }

        if (repoType == "local")
        {
            return "local";
        }

        if (Uri.TryCreate(repoUrl, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                return segments[^2];
            }
        }

        return string.Empty;
    }

    private string ResolveRepo(TaskRequestBase request, string? repoUrl)
    {
        if (!string.IsNullOrWhiteSpace(request.Repo))
        {
            return request.Repo.Trim();
        }

        return _textUtilityService.ExtractRepositoryName(repoUrl);
    }
}
