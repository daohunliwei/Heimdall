using Heimdall.Core.Models;
using ApiChatMessage = Heimdall.Infrastructure.Models.ChatMessage;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Microsoft.Extensions.AI;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 统一构建 Chat 与 Ask 所需的多角色消息链路
/// </summary>
public sealed class ChatMessageBuilderService
{
    /// <summary>
    /// 构建 Chat 场景的消息列表
    /// 保留历史对话顺序
    /// 将模板系统约束作为独立的 System 消息插入
    /// 当前问题始终作为最后一条 User 消息发送
    /// </summary>
    public List<AiChatMessage> BuildChatMessages(
        IReadOnlyList<ApiChatMessage> history,
        string? templatedSystemPrompt,
        string? rewrittenUserPrompt)
    {
        var messages = new List<AiChatMessage>();
        var currentUserIndex = FindLastUserMessageIndex(history);

        if (currentUserIndex > 0)
        {
            foreach (var item in history.Take(currentUserIndex))
            {
                var mapped = MapApiMessage(item);
                if (mapped is not null)
                {
                    messages.Add(mapped);
                }
            }
        }

        InsertSystemPrompt(messages, templatedSystemPrompt);

        var effectivePrompt = !string.IsNullOrWhiteSpace(rewrittenUserPrompt)
            ? rewrittenUserPrompt
            : currentUserIndex >= 0
                ? history[currentUserIndex].Content
                : history.LastOrDefault()?.Content;

        if (!string.IsNullOrWhiteSpace(effectivePrompt))
        {
            messages.Add(new AiChatMessage(ChatRole.User, effectivePrompt));
        }

        if (messages.Count == 0)
        {
            messages.Add(new AiChatMessage(ChatRole.User, "Hello"));
        }

        return messages;
    }

    /// <summary>
    /// 构建 Ask 场景的消息列表
    /// 版本约束与回答要求使用 System 消息承载
    /// 历史对话 证据上下文 当前问题分别使用独立消息表达
    /// </summary>
    public List<AiChatMessage> BuildAskMessages(
        VersionedKnowledgeContext knowledgeContext,
        string question,
        string? filePath,
        bool deepResearch,
        string artifactContext,
        string pageContext,
        IReadOnlyList<TaskConversationMessage> history)
    {
        var messages = new List<AiChatMessage>
        {
            new(ChatRole.System, BuildAskSystemInstruction(knowledgeContext, deepResearch))
        };

        foreach (var historyMessage in history.TakeLast(8))
        {
            var mapped = MapConversationMessage(historyMessage);
            if (mapped is not null)
            {
                messages.Add(mapped);
            }
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            messages.Add(new AiChatMessage(
                ChatRole.User,
                $"## 用户关注文件\n- {filePath}"));
        }

        if (!string.IsNullOrWhiteSpace(artifactContext))
        {
            messages.Add(new AiChatMessage(ChatRole.User, artifactContext));
        }

        if (!string.IsNullOrWhiteSpace(pageContext))
        {
            messages.Add(new AiChatMessage(ChatRole.User, pageContext));
        }

        messages.Add(new AiChatMessage(
            ChatRole.User,
            BuildAskQuestionMessage(question, deepResearch)));

        return messages;
    }

    /// <summary>
    /// 把 API 层聊天消息转换为 MEAI 消息
    /// 空内容消息会被过滤
    /// 未识别角色默认按 User 处理
    /// </summary>
    public AiChatMessage? MapApiMessage(ApiChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Content))
        {
            return null;
        }

        return new AiChatMessage(ParseRole(message.Role), message.Content);
    }

    /// <summary>
    /// 把任务历史消息转换为 MEAI 消息
    /// </summary>
    public AiChatMessage? MapConversationMessage(TaskConversationMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Content))
        {
            return null;
        }

        return new AiChatMessage(ParseRole(message.Role), message.Content);
    }

    /// <summary>
    /// 解析角色名称
    /// system assistant tool 可保留原语义
    /// 其他值统一回退到 user
    /// </summary>
    public static ChatRole ParseRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "system" => ChatRole.System,
            "assistant" => ChatRole.Assistant,
            "tool" => ChatRole.Tool,
            _ => ChatRole.User
        };
    }

    /// <summary>
    /// 在历史中的 System 消息之后插入模板系统消息
    /// 避免覆盖客户端已传入的系统约束
    /// </summary>
    private static void InsertSystemPrompt(List<AiChatMessage> messages, string? templatedSystemPrompt)
    {
        if (string.IsNullOrWhiteSpace(templatedSystemPrompt))
        {
            return;
        }

        var insertIndex = 0;
        while (insertIndex < messages.Count && messages[insertIndex].Role == ChatRole.System)
        {
            insertIndex++;
        }

        messages.Insert(insertIndex, new AiChatMessage(ChatRole.System, templatedSystemPrompt));
    }

    /// <summary>
    /// 找到最后一条用户消息
    /// 该消息会被视为当前问题并在模板渲染后重新写入
    /// </summary>
    private static int FindLastUserMessageIndex(IReadOnlyList<ApiChatMessage> history)
    {
        for (var index = history.Count - 1; index >= 0; index--)
        {
            if (string.Equals(history[index].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// 构建 Ask 的系统级约束
    /// 版本绑定与回答规范都放在这里
    /// </summary>
    private static string BuildAskSystemInstruction(VersionedKnowledgeContext knowledgeContext, bool deepResearch)
    {
        return $"""
你是一个代码仓库技术专家
你必须严格基于指定版本的仓库页面内容与工件证据回答问题

## 版本绑定
- 仓库：{knowledgeContext.Repository.DisplayName}
- 地址：{knowledgeContext.Repository.RepoUrl}
- 分支：{knowledgeContext.EffectiveBranch}
- 输出语言：{knowledgeContext.EffectiveLanguage}
- RepositoryVersionId：{knowledgeContext.RepositoryVersion.Id}
- CommitSha：{knowledgeContext.RepositoryVersion.CommitSha}
- WikiVersionId：{knowledgeContext.WikiVersion.Id}
- WikiVersionNo：{knowledgeContext.WikiVersion.VersionNo}

## 回答要求
- 只基于上述版本化证据回答
- 禁止回退到未指定版本或泛化臆测
- 优先引用版本化页面中的具体证据
- 当证据不足时，明确说明“当前版本证据不足”
- 回答使用中文
- {(deepResearch ? "需要给出更完整的架构脉络 关键模块关系 潜在限制与可验证依据" : "回答保持聚焦 优先解决当前问题")}
""";
    }

    /// <summary>
    /// 构建 Ask 的最终问题消息
    /// 当前问题单独承载
    /// 避免与证据上下文混写成一个大 Prompt
    /// </summary>
    private static string BuildAskQuestionMessage(string question, bool deepResearch)
    {
        return $"""
## 用户问题
{question}

## 回答深度
{(deepResearch ? "请进行深度研究式回答" : "请进行聚焦式回答")}
""";
    }
}
