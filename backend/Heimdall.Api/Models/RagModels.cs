namespace Heimdall.Api.Models;

/// <summary>
/// 对话记忆中的单轮对话。
/// </summary>
public sealed class DialogTurn
{
    /// <summary>
    /// 轮次标识。
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 用户提问。
    /// </summary>
    public string UserQuery { get; init; } = string.Empty;

    /// <summary>
    /// 助手回答。
    /// </summary>
    public string AssistantResponse { get; init; } = string.Empty;
}

/// <summary>
/// 聊天上下文构建结果。
/// </summary>
public sealed class ChatContextResult
{
    /// <summary>
    /// 拼接后的上下文文本。
    /// </summary>
    public string ContextText { get; init; } = string.Empty;

    /// <summary>
    /// 会话记忆。
    /// </summary>
    public List<DialogTurn> MemoryTurns { get; init; } = new();

    /// <summary>
    /// 当前文件内容。
    /// </summary>
    public string FileContent { get; init; } = string.Empty;

    /// <summary>
    /// 是否因为输入过大而跳过检索。
    /// </summary>
    public bool InputTooLarge { get; init; }
}
