namespace Heimdall.Core.Models;

/// <summary>
/// Wiki 页面 DTO。
/// 该对象同时承载结构规划结果、页面草案、全局收敛结果与渲染后处理后的稳定页面元数据。
/// </summary>
public class WikiPageDto
{
    /// <summary>
    /// 页面稳定标识。
    /// 该值在结构规划阶段生成，后续批次生成、收敛与落库阶段都应保持不变。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 页面主标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 页面简介。
    /// 该字段用于草案摘要、版本说明与空内容兜底描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 页面最终 Markdown 内容。
    /// 经过渲染后处理后，该字段会包含 Frontmatter、相关源文件说明与正文。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 页面导航标题。
    /// 当前端侧边栏需要更短标题或全局收敛需要消解重名时，使用该字段作为展示标题。
    /// </summary>
    public string NavTitle { get; set; } = string.Empty;

    /// <summary>
    /// 页面类型。
    /// 允许值包括 overview、section、article、appendix。
    /// </summary>
    public string PageType { get; set; } = "article";

    /// <summary>
    /// 与当前页面强相关的源文件列表。
    /// </summary>
    public List<string> FilePaths { get; set; } = new();

    /// <summary>
    /// 页面搜索关键词——供当前检索链路在生成时检索真实代码片段。
    /// </summary>
    public List<string> SearchKeywords { get; set; } = new();

    /// <summary>
    /// 必须包含的关键文件路径——检索结果会与这些路径取并集。
    /// </summary>
    public List<string> KeyFilePaths { get; set; } = new();

    /// <summary>
    /// 页面重要性。
    /// 允许值包括 high、medium、low。
    /// </summary>
    public string Importance { get; set; } = "medium";

    /// <summary>
    /// 关联页面标识列表。
    /// 主要用于“延伸阅读”“交叉引用”与页面关系图谱。
    /// </summary>
    public List<string> RelatedPages { get; set; } = new();

    /// <summary>
    /// 前置阅读页面标识列表。
    /// 主要用于 depends_on 关系建模与阅读顺序推荐。
    /// </summary>
    public List<string> PrerequisitePages { get; set; } = new();

    /// <summary>
    /// 父页面标识。
    /// 当页面处于嵌套结构中时，用于表示其直接父页面。
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// V7: 页面在结构树中的深度（0 为根页面，1 为一级子页面，以此类推）。
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// V7: 页面内容深度级别——决定页面内容的详细程度。
    /// overview=高层概述，module=模块级介绍，component=组件级详解，implementation=实现细节。
    /// </summary>
    public string ContentDepthLevel { get; set; } = "module";

    /// <summary>
    /// 是否表示目录型页面。
    /// </summary>
    public bool? IsSection { get; set; }

    /// <summary>
    /// 子页面标识列表。
    /// 该字段可由结构规划直接提供，也可在收敛阶段根据父子关系反向补齐。
    /// </summary>
    public List<string>? Children { get; set; }

    /// <summary>
    /// 页面 Frontmatter 元数据。
    /// 该字段在页面草案阶段以严格结构化对象输出，并在渲染后处理中转换为 Markdown Frontmatter。
    /// </summary>
    public WikiPageFrontMatterDto FrontMatter { get; set; } = new();

    /// <summary>
    /// 页面目录提纲。
    /// 该字段可由模型草案直接给出，也可在渲染后处理时从 Markdown 标题重新提取。
    /// </summary>
    public List<WikiPageHeadingDto> Outline { get; set; } = new();

    /// <summary>
    /// 页面源码覆盖信息。
    /// 该字段用于持久化到 SourceCoverageJson，辅助质量审计与问题定位。
    /// </summary>
    public WikiPageSourceCoverageDto SourceCoverage { get; set; } = new();

    /// <summary>
    /// 页面生成或收敛阶段产生的警告列表。
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 是否使用后端兜底草案。
    /// 当页面模型输出无法解析时，后端会保留该标记以便质量报告统计。
    /// </summary>
    public bool IsFallbackDraft { get; set; }
}

/// <summary>
/// Wiki 页面 Frontmatter DTO。
/// </summary>
public class WikiPageFrontMatterDto
{
    /// <summary>
    /// 页面摘要。
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 页面描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 页面标签。
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Frontmatter 中显式暴露的源文件列表。
    /// </summary>
    public List<string> SourceFiles { get; set; } = new();
}

/// <summary>
/// Wiki 页面目录项 DTO。
/// </summary>
public class WikiPageHeadingDto
{
    /// <summary>
    /// 标题层级。
    /// H1 对应 1，H2 对应 2，以此类推。
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 标题文本。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 标题锚点。
    /// </summary>
    public string Anchor { get; set; } = string.Empty;
}

/// <summary>
/// 页面源码覆盖 DTO。
/// </summary>
public class WikiPageSourceCoverageDto
{
    /// <summary>
    /// 主要源文件列表。
    /// </summary>
    public List<string> PrimaryFiles { get; set; } = new();

    /// <summary>
    /// 证据项列表。
    /// </summary>
    public List<WikiPageSourceEvidenceDto> Evidence { get; set; } = new();
}

/// <summary>
/// 页面源码证据 DTO。
/// </summary>
public class WikiPageSourceEvidenceDto
{
    /// <summary>
    /// 源文件路径。
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 该源文件为何与当前页面相关的简要说明。
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 证据涉及的关键符号名称列表。
    /// </summary>
    public List<string> Symbols { get; set; } = new();
}

/// <summary>
/// Wiki 目录分组 DTO。
/// </summary>
public class WikiSectionDto
{
    /// <summary>
    /// 分组标识。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 分组标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 直属页面标识列表。
    /// </summary>
    public List<string> Pages { get; set; } = new();

    /// <summary>
    /// 子分组标识列表。
    /// </summary>
    public List<string>? Subsections { get; set; }

    /// <summary>
    /// V7: 递归子分组（内联展开），用于深层嵌套结构规划。
    /// </summary>
    public List<WikiSectionDto>? Children { get; set; }

    /// <summary>
    /// V7: 该分组在结构树中的深度。
    /// </summary>
    public int Depth { get; set; }
}

/// <summary>
/// Wiki 结构 DTO。
/// 该对象是结构规划工件、全局收敛输入与最终渲染落库的统一载体。
/// </summary>
public class WikiStructureDto
{
    /// <summary>
    /// 结构标识。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Wiki 总标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Wiki 总描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 页面列表。
    /// </summary>
    public List<WikiPageDto> Pages { get; set; } = new();

    /// <summary>
    /// 目录分组列表。
    /// </summary>
    public List<WikiSectionDto> Sections { get; set; } = new();

    /// <summary>
    /// 根分组标识列表。
    /// </summary>
    public List<string> RootSections { get; set; } = new();
}

/// <summary>
/// Wiki 全局收敛结果 DTO。
/// </summary>
public class WikiConvergenceResultDto
{
    /// <summary>
    /// 收敛后的结构对象。
    /// </summary>
    public WikiStructureDto Structure { get; set; } = new();

    /// <summary>
    /// 质量报告。
    /// </summary>
    public WikiQualityReportDto QualityReport { get; set; } = new();
}

/// <summary>
/// Wiki 质量报告 DTO。
/// </summary>
public class WikiQualityReportDto
{
    /// <summary>
    /// 页面总数。
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// 兜底草案页面数量。
    /// </summary>
    public int FallbackPageCount { get; set; }

    /// <summary>
    /// 空正文页面数量。
    /// </summary>
    public int EmptyContentPageCount { get; set; }

    /// <summary>
    /// 被统一处理的导航标题数量。
    /// </summary>
    public int NormalizedNavTitleCount { get; set; }

    /// <summary>
    /// 新增的双向关联数量。
    /// </summary>
    public int AddedReciprocalRelationCount { get; set; }

    /// <summary>
    /// 新增的父子关系数量。
    /// </summary>
    public int AddedChildLinkCount { get; set; }

    /// <summary>
    /// 收敛阶段发现的问题列表。
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// V4 每页质量评分（0-100），由收敛阶段计算。
    /// Key 为页面 ID，Value 为评分。
    /// </summary>
    public Dictionary<string, int> PageQualityScores { get; set; } = new();

    /// <summary>
    /// V4 需要重新生成的弱页面 ID 列表（评分低于阈值）。
    /// </summary>
    public List<string> WeakPageIds { get; set; } = new();
}

/// <summary>
/// Wiki 渲染后处理结果 DTO。
/// </summary>
public class WikiRenderResultDto
{
    /// <summary>
    /// 渲染后处理后的结构对象。
    /// </summary>
    public WikiStructureDto Structure { get; set; } = new();

    /// <summary>
    /// 参与渲染后处理的页面数量。
    /// </summary>
    public int RenderedPageCount { get; set; }

    /// <summary>
    /// 生成 Frontmatter 的页面数量。
    /// </summary>
    public int FrontMatterPageCount { get; set; }

    /// <summary>
    /// 重新提取的目录项总数。
    /// </summary>
    public int OutlineHeadingCount { get; set; }
}

/// <summary>
/// Wiki 生成结果 DTO。
/// </summary>
public class WikiGenerationResult
{
    /// <summary>
    /// 是否来自缓存。
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// 仓库所属者。
    /// </summary>
    public string? RepoOwner { get; set; }

    /// <summary>
    /// 仓库名称。
    /// </summary>
    public string? RepoName { get; set; }

    /// <summary>
    /// 仓库类型。
    /// </summary>
    public string? RepoType { get; set; }

    /// <summary>
    /// 语言。
    /// </summary>
    public string Language { get; set; } = "zh";

    /// <summary>
    /// 生效 Provider。
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 生效模型。
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 结构化 Wiki 结果。
    /// </summary>
    public WikiStructureDto WikiStructure { get; set; } = new();

    /// <summary>
    /// 已生成页面集合。
    /// </summary>
    public Dictionary<string, WikiPageDto> GeneratedPages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 错误信息。
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 告警集合。
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
