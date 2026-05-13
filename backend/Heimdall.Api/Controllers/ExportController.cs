using Heimdall.Api.Models;
using Heimdall.Api.Services.Export;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供 Wiki 导出接口。
/// </summary>
[ApiController]
[Route("export")]
public sealed class ExportController : ControllerBase
{
    private readonly WikiExportService _wikiExportService;

    /// <summary>
    /// 初始化导出控制器。
    /// </summary>
    public ExportController(WikiExportService wikiExportService)
    {
        _wikiExportService = wikiExportService;
    }

    /// <summary>
    /// 导出 Wiki 页面集合。
    /// </summary>
    [HttpPost("wiki")]
    public ActionResult ExportWiki([FromBody] WikiExportRequest request)
    {
        var exportedFile = _wikiExportService.BuildExportFile(request);
        return File(exportedFile.Content, exportedFile.ContentType, exportedFile.FileName);
    }
}
