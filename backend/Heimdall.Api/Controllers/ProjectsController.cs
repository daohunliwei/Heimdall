using Heimdall.Api.Models;
using Heimdall.Api.Services.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// 提供已处理项目列表接口。
/// </summary>
[ApiController]
[Route("api/processed_projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly ProcessedProjectService _processedProjectService;

    /// <summary>
    /// 初始化项目控制器。
    /// </summary>
    public ProjectsController(ProcessedProjectService processedProjectService)
    {
        _processedProjectService = processedProjectService;
    }

    /// <summary>
    /// 获取已处理项目列表。
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyCollection<ProcessedProjectEntry>> GetProcessedProjects()
    {
        return Ok(_processedProjectService.GetProcessedProjects());
    }
}
