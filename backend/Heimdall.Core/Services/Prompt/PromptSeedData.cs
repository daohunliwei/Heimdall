using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Prompt;

/// <summary>
/// 系统内置提示词种子数据——将现有硬编码提示词迁移为数据库模板。
/// </summary>
public sealed class PromptSeedData
{
    private readonly IPromptTemplateRepository _templateRepo;
    private readonly ILogger<PromptSeedData> _logger;

    public PromptSeedData(IPromptTemplateRepository templateRepo, ILogger<PromptSeedData> logger)
    {
        _templateRepo = templateRepo;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var seeds = new[]
        {
            new PromptTemplate
            {
                Slug = "wiki-structure-planning",
                Name = "Wiki 结构规划",
                Layer = "wiki_structure",
                TemplateContent = WikiStructureTemplate,
                IsSystem = true,
                IsActive = true,
                Version = 1
            },
            new PromptTemplate
            {
                Slug = "wiki-page-generation",
                Name = "Wiki 页面生成",
                Layer = "wiki_page",
                TemplateContent = WikiPageTemplate,
                IsSystem = true,
                IsActive = true,
                Version = 1
            },
            new PromptTemplate
            {
                Slug = "ask-query",
                Name = "问答对话",
                Layer = "ask",
                TemplateContent = AskTemplate,
                IsSystem = true,
                IsActive = true,
                Version = 1
            },
            new PromptTemplate
            {
                Slug = "slides-generation",
                Name = "演示文稿生成",
                Layer = "slides",
                TemplateContent = SlidesTemplate,
                IsSystem = true,
                IsActive = true,
                Version = 1
            },
            new PromptTemplate
            {
                Slug = "workshop-generation",
                Name = "训练营材料生成",
                Layer = "workshop",
                TemplateContent = WorkshopTemplate,
                IsSystem = true,
                IsActive = true,
                Version = 1
            }
        };

        foreach (var seed in seeds)
        {
            var existing = await _templateRepo.GetBySlugAsync(seed.Slug);
            if (existing is null)
            {
                await _templateRepo.AddAsync(seed);
                _logger.LogInformation("已创建系统提示词模板：{Slug}", seed.Slug);
            }
            else
            {
                _logger.LogDebug("系统提示词模板 {Slug} 已存在，跳过", seed.Slug);
            }
        }

        _logger.LogInformation("提示词种子数据初始化完成");
    }

    // ── 模板内容占位符 ──
    // 实际提示词内容由 TaskPromptService 迁移而来，此处提供占位模板；
    // 通过 API 或管理后台可以在线调优。

    private const string WikiStructureTemplate = """
你是资深软件架构师和技术文档专家。你的任务是分析此仓库并创建逻辑全面的 Wiki 结构。

请分析仓库结构并输出 JSON 格式的 Wiki 规划，包含 pages（页面列表）、sections（章节）和 rootSections（根章节）。

仓库信息：
- 文件树：{{file_tree}}
- README：{{readme}}
- 语言：{{language}}
""";

    private const string WikiPageTemplate = """
你是资深技术文档撰写专家。你的任务是针对以下页面主题生成全面的技术 Wiki 页面。

页面主题：{{page_title}}
页面描述：{{page_description}}
相关源文件：{{source_files}}
语言：{{language}}

请生成详细准确的技术文档（Markdown 格式）。
""";

    private const string AskTemplate = """
你是代码仓库分析专家。基于仓库知识库回答问题。

仓库上下文：{{repository_context}}
语言：{{language}}

用户问题：{{question}}
""";

    private const string SlidesTemplate = """
你是技术演讲专家。基于仓库 Wiki 内容生成演示文稿。

Wiki 内容：{{wiki_content}}
语言：{{language}}

请生成结构化的幻灯片大纲和内容。
""";

    private const string WorkshopTemplate = """
你是技术培训专家。基于仓库 Wiki 内容生成训练营材料。

Wiki 内容：{{wiki_content}}
语言：{{language}}

请生成包含定义、示例和练习的训练营内容。
""";
}
