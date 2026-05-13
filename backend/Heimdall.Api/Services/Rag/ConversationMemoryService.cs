using Heimdall.Api.Models;

namespace Heimdall.Api.Services.Rag;

/// <summary>
/// 对话记忆服务，对齐历史实现的会话记忆行为。
/// </summary>
public sealed class ConversationMemoryService
{
    private readonly List<DialogTurn> _dialogTurns = new();

    /// <summary>
    /// 返回当前会话历史。
    /// </summary>
    public IReadOnlyList<DialogTurn> GetTurns()
    {
        return _dialogTurns.ToList();
    }

    /// <summary>
    /// 追加一轮对话。
    /// </summary>
    public void AddDialogTurn(string userQuery, string assistantResponse)
    {
        _dialogTurns.Add(new DialogTurn
        {
            UserQuery = userQuery,
            AssistantResponse = assistantResponse
        });
    }

    /// <summary>
    /// 从消息列表中恢复会话历史。
    /// </summary>
    public void HydrateFromMessages(IReadOnlyList<ChatMessage> messages)
    {
        _dialogTurns.Clear();
        for (var index = 0; index < messages.Count - 1; index += 2)
        {
            if (index + 1 >= messages.Count)
            {
                break;
            }

            var userMessage = messages[index];
            var assistantMessage = messages[index + 1];
            if (string.Equals(userMessage.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(assistantMessage.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                AddDialogTurn(userMessage.Content, assistantMessage.Content);
            }
        }
    }

    /// <summary>
    /// 生成提示词中的 conversation_history 块。
    /// </summary>
    public string BuildConversationHistoryXml()
    {
        if (_dialogTurns.Count == 0)
        {
            return string.Empty;
        }

        var lines = _dialogTurns.Select(turn => $"<turn>\n<user>{turn.UserQuery}</user>\n<assistant>{turn.AssistantResponse}</assistant>\n</turn>");
        return string.Join('\n', lines);
    }
}
