namespace Heimdall.Core.Interfaces.Services;

public interface IPromptMergeService
{
    /// <summary>
    /// 按类别、SubCategory、Provider 拼装最终提示词
    /// </summary>
    /// <param name="category">任务类别，如 wiki_structure、wiki_page、code_summary</param>
    /// <param name="provider">LLM Provider，如 ollama、openai、google</param>
    /// <param name="outputFormat">目标输出格式，如 json、markdown、html、text</param>
    /// <param name="variables">变量替换字典</param>
    /// <param name="subCategory">子类别过滤，如 "file"、"base"、"format"。null 表示不限定</param>
    /// <returns>拼装完成的提示词字符串</returns>
    Task<string> BuildPromptAsync(
        string category,
        string provider,
        string outputFormat,
        Dictionary<string, string>? variables = null,
        string? subCategory = null);

    /// <summary>
    /// 获取分离后的 SystemPrompt 和 UserPrompt
    /// </summary>
    Task<(string? SystemPrompt, string UserPrompt)> BuildChatPromptAsync(
        string category,
        string provider,
        string outputFormat,
        Dictionary<string, string>? variables = null,
        string? subCategory = null);
}
