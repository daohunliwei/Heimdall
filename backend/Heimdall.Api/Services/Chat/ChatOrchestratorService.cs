using System.Text;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Configuration;
using Heimdall.Api.Services.Providers;
using Heimdall.Api.Services.Rag;
using Heimdall.Api.Services.Utility;

namespace Heimdall.Api.Services.Chat;

/// <summary>
/// 聊天编排服务，负责统一请求处理链路。
/// </summary>
public sealed class ChatOrchestratorService
{
    private readonly HeimdallConfigService _configService;
    private readonly PromptTemplateService _promptTemplateService;
    private readonly ProviderRegistry _providerRegistry;
    private readonly RagContextService _ragContextService;
    private readonly TextUtilityService _textUtilityService;

    /// <summary>
    /// 初始化聊天编排服务。
    /// </summary>
    public ChatOrchestratorService(
        HeimdallConfigService configService,
        PromptTemplateService promptTemplateService,
        ProviderRegistry providerRegistry,
        RagContextService ragContextService,
        TextUtilityService textUtilityService)
    {
        _configService = configService;
        _promptTemplateService = promptTemplateService;
        _providerRegistry = providerRegistry;
        _ragContextService = ragContextService;
        _textUtilityService = textUtilityService;
    }

    /// <summary>
    /// 执行聊天请求。
    /// </summary>
    public async Task<string> GenerateAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var memoryService = new ConversationMemoryService();
        memoryService.HydrateFromMessages(request.Messages);
        var context = await _ragContextService.BuildContextAsync(request, memoryService, cancellationToken);

        var lastMessage = request.Messages[^1];
        var isDeepResearch = request.Messages.Any(message => message.Content.Contains("[DEEP RESEARCH]", StringComparison.OrdinalIgnoreCase));
        var query = lastMessage.Content.Replace("[DEEP RESEARCH]", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (isDeepResearch &&
            lastMessage.Content.Contains("continue", StringComparison.OrdinalIgnoreCase) &&
            lastMessage.Content.Contains("research", StringComparison.OrdinalIgnoreCase))
        {
            var originalTopic = request.Messages
                .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content.Replace("[DEEP RESEARCH]", string.Empty, StringComparison.OrdinalIgnoreCase).Trim())
                .FirstOrDefault(content => !content.Contains("continue", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(originalTopic))
            {
                query = originalTopic;
            }
        }
        var repoUrl = request.RepoUrl;
        var repoName = _textUtilityService.ExtractRepositoryName(repoUrl);
        var repoType = request.Type ?? "github";
        var languageConfig = _configService.GetLanguageConfig();
        var languageCode = request.Language ?? languageConfig.Default;
        var languageName = languageConfig.SupportedLanguages.TryGetValue(languageCode, out var configuredLanguage) ? configuredLanguage : "English";
        var researchIteration = isDeepResearch
            ? request.Messages.Count(message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)) + 1
            : 1;

        var systemPrompt = BuildSystemPrompt(isDeepResearch, researchIteration, repoType, repoUrl, repoName, languageName);
        var prompt = BuildPrompt(request, memoryService, context, systemPrompt, query);
        var (providerId, model, parameters, provider) = _providerRegistry.ResolveChatProvider(request);
        var providerRequest = new ProviderChatRequest
        {
            ProviderId = providerId,
            Model = model,
            Prompt = prompt,
            Temperature = parameters.Temperature,
            TopP = parameters.TopP,
            TopK = parameters.TopK,
            Options = parameters.Options
        };

        var response = await provider.GenerateAsync(providerRequest, cancellationToken);
        if (context.InputTooLarge && !string.IsNullOrWhiteSpace(response) &&
            (response.Contains("maximum context length", StringComparison.OrdinalIgnoreCase) ||
             response.Contains("token limit", StringComparison.OrdinalIgnoreCase) ||
             response.Contains("too many tokens", StringComparison.OrdinalIgnoreCase)))
        {
            var fallbackContext = new ChatContextResult
            {
                ContextText = string.Empty,
                FileContent = context.FileContent,
                InputTooLarge = true,
                MemoryTurns = context.MemoryTurns
            };
            var fallbackPrompt = BuildPrompt(request, memoryService, fallbackContext, systemPrompt, query);
            var fallbackRequest = new ProviderChatRequest
            {
                ProviderId = providerRequest.ProviderId,
                Model = providerRequest.Model,
                Prompt = fallbackPrompt,
                Temperature = providerRequest.Temperature,
                TopP = providerRequest.TopP,
                TopK = providerRequest.TopK,
                Options = providerRequest.Options
            };
            return await provider.GenerateAsync(fallbackRequest, cancellationToken);
        }

        return response;
    }

    /// <summary>
    /// 校验聊天请求。
    /// </summary>
    private static void ValidateRequest(ChatCompletionRequest request)
    {
        if (request.Messages.Count == 0)
        {
            throw new InvalidOperationException("No messages provided");
        }

        if (!string.Equals(request.Messages[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Last message must be from the user");
        }
    }

    /// <summary>
    /// 构造系统提示词。
    /// </summary>
    private string BuildSystemPrompt(bool isDeepResearch, int researchIteration, string repoType, string repoUrl, string repoName, string languageName)
    {
        if (!isDeepResearch)
        {
            return _promptTemplateService.GetSimpleChatPrompt(repoType, repoUrl, repoName, languageName);
        }

        return researchIteration switch
        {
            <= 1 => _promptTemplateService.GetDeepResearchFirstIterationPrompt(repoType, repoUrl, repoName, languageName),
            >= 5 => _promptTemplateService.GetDeepResearchFinalIterationPrompt(repoType, repoUrl, repoName, researchIteration, languageName),
            _ => _promptTemplateService.GetDeepResearchIntermediateIterationPrompt(repoType, repoUrl, repoName, researchIteration, languageName)
        };
    }

    /// <summary>
    /// 按既定格式构造最终 prompt。
    /// </summary>
    private string BuildPrompt(ChatCompletionRequest request, ConversationMemoryService memoryService, ChatContextResult context, string systemPrompt, string query)
    {
        var builder = new StringBuilder();
        builder.Append("/no_think ");
        builder.Append(systemPrompt);
        builder.AppendLine();
        builder.AppendLine();

        var conversationHistory = memoryService.BuildConversationHistoryXml();
        if (!string.IsNullOrWhiteSpace(conversationHistory))
        {
            builder.AppendLine("<conversation_history>");
            builder.AppendLine(conversationHistory);
            builder.AppendLine("</conversation_history>");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(context.FileContent) && !string.IsNullOrWhiteSpace(request.FilePath))
        {
            builder.AppendLine($"<currentFileContent path=\"{request.FilePath}\">");
            builder.AppendLine(context.FileContent);
            builder.AppendLine("</currentFileContent>");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(context.ContextText))
        {
            builder.AppendLine("<START_OF_CONTEXT>");
            builder.AppendLine(context.ContextText);
            builder.AppendLine("<END_OF_CONTEXT>");
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine("<note>Answering without retrieval augmentation.</note>");
            builder.AppendLine();
        }

        builder.AppendLine("<query>");
        builder.AppendLine(query);
        builder.AppendLine("</query>");
        builder.AppendLine();
        builder.Append("Assistant: ");

        if (string.Equals(request.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(" /no_think");
        }

        return builder.ToString();
    }
}
