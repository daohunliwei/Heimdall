using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Heimdall.Core.Tools;

/// <summary>
/// 代码文件读取工具——LLM 通过 Tool Call 按需读取仓库中的代码文件。
/// </summary>
public static class ReadCodeFileTool
{
    /// <summary>
    /// 根据文件路径读取仓库中的代码内容，返回带行号的代码文本。单次最多返回 maxLines 行。
    /// </summary>
    [Description("根据文件路径读取仓库中的代码内容，返回带行号的代码文本。单次最多返回500行。")]
    public static string ReadCodeFile(
        string repoPath,
        string filePath,
        int maxLines = 500)
    {
        var fullPath = Path.Combine(repoPath, filePath.TrimStart('/', '\\'));
        if (!File.Exists(fullPath))
        {
            return $"错误：文件 {filePath} 不存在于当前仓库中。请检查文件路径拼写，或使用 SearchSymbols 搜索正确的文件名。";
        }

        var lines = File.ReadAllLines(fullPath);
        var sb = new System.Text.StringBuilder();

        if (lines.Length <= maxLines)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                sb.AppendLine($"{i + 1,5}| {lines[i]}");
            }
        }
        else
        {
            for (var i = 0; i < maxLines; i++)
            {
                sb.AppendLine($"{i + 1,5}| {lines[i]}");
            }
            sb.AppendLine();
            sb.AppendLine($"[截断：文件共 {lines.Length} 行，已返回前 {maxLines} 行。可通过指定更高的 maxLines 参数或使用 SearchSymbols 定位后分批读取]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 创建 AIFunction 实例，捕获 repoPath 上下文。
    /// </summary>
    public static AIFunction Create(string repoPath) =>
        AIFunctionFactory.Create(
            (string filePath, int maxLines = 500) => ReadCodeFile(repoPath, filePath, maxLines),
            name: "ReadCodeFile",
            description: "根据文件路径读取仓库中的代码内容，返回带行号的代码文本。单次最多返回500行。");
}
