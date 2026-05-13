using System.Text;
using System.Text.Json;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Utility;

namespace Heimdall.Api.Services.Export;

/// <summary>
/// 负责将 Wiki 页面集合导出为文件内容。
/// </summary>
public sealed class WikiExportService
{
    private readonly TextUtilityService _textUtilityService;

    /// <summary>
    /// 初始化导出服务。
    /// </summary>
    public WikiExportService(TextUtilityService textUtilityService)
    {
        _textUtilityService = textUtilityService;
    }

    /// <summary>
    /// 根据导出格式生成文件。
    /// </summary>
    public ExportedFileResult BuildExportFile(WikiExportRequest request)
    {
        var repoName = _textUtilityService.ExtractRepositoryName(request.RepoUrl);
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var useJson = string.Equals(request.Format, "json", StringComparison.OrdinalIgnoreCase);
        var fileName = useJson ? $"{repoName}_wiki_{timestamp}.json" : $"{repoName}_wiki_{timestamp}.md";
        var content = useJson ? BuildJsonExport(request) : BuildMarkdownExport(request);
        var bytes = Encoding.UTF8.GetBytes(content);
        var contentType = useJson ? "application/json; charset=utf-8" : "text/markdown; charset=utf-8";

        return new ExportedFileResult
        {
            Content = bytes,
            ContentType = contentType,
            FileName = fileName
        };
    }

    /// <summary>
    /// 将页面集合导出为 Markdown。
    /// </summary>
    private static string BuildMarkdownExport(WikiExportRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {request.RepoUrl} Wiki 导出");
        builder.AppendLine();
        builder.AppendLine($"生成时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine("## 目录");
        builder.AppendLine();

        foreach (var page in request.Pages)
        {
            builder.AppendLine($"- [{page.Title}](#{page.Id})");
        }

        builder.AppendLine();
        foreach (var page in request.Pages)
        {
            builder.AppendLine($"<a id='{page.Id}'></a>");
            builder.AppendLine();
            builder.AppendLine($"## {page.Title}");
            builder.AppendLine();
            if (page.RelatedPages.Count > 0)
            {
                builder.AppendLine("### 相关页面");
                builder.AppendLine();
                builder.AppendLine(string.Join('、', page.RelatedPages));
                builder.AppendLine();
            }

            builder.AppendLine(page.Content);
            builder.AppendLine();
            builder.AppendLine("---");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// 将页面集合导出为 JSON。
    /// </summary>
    private static string BuildJsonExport(WikiExportRequest request)
    {
        var payload = new
        {
            metadata = new
            {
                repository = request.RepoUrl,
                generated_at = DateTimeOffset.Now,
                page_count = request.Pages.Count
            },
            pages = request.Pages
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

/// <summary>
/// 导出文件的二进制内容与元信息。
/// </summary>
public sealed class ExportedFileResult
{
    /// <summary>
    /// 文件内容。
    /// </summary>
    public byte[] Content { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// 文件类型。
    /// </summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>
    /// 下载文件名。
    /// </summary>
    public string FileName { get; init; } = "export.bin";
}
