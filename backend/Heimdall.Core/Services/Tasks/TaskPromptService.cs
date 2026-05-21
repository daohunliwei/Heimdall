using System.Text;
using Heimdall.Core.Models;
using Heimdall.Core.Services.Prompt;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 任务提示词服务，承载前端迁移过来的任务编排提示词。
/// 通过 IServiceScopeFactory 按需解析 PromptManagementService。
/// </summary>
public sealed class TaskPromptService
{
    private readonly IServiceScopeFactory? _scopeFactory;

    public TaskPromptService(IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 尝试从数据库解析托管提示词模板，失败则返回 null。
    /// </summary>
    /// <param name="slug">提示词模板唯一标识。</param>
    /// <param name="repositoryId">仓库 ID（可选，用于查找覆写）。</param>
    /// <param name="variables">模板变量。</param>
    /// <returns>解析后的提示词文本；未找到模板则返回 null。</returns>
    public async Task<string?> TryResolveManagedTemplateAsync(
        string slug, Guid? repositoryId, Dictionary<string, string>? variables = null)
    {
        if (_scopeFactory is null) return null;
        using var scope = _scopeFactory.CreateScope();
        var promptManagement = scope.ServiceProvider.GetRequiredService<PromptManagementService>();
        return await promptManagement.ResolveTemplateAsync(slug, repositoryId, variables);
    }
    /// <summary>
    /// 构建增强版结构规划提示词——集成 CodeUnderstandingResult 以实现深层嵌套结构。
    /// </summary>
    public string BuildWikiStructurePromptV7(
        string owner, string repo, string fileTree, string readme,
        string languageDisplayName, bool isComprehensiveView,
        CodeUnderstandingResult? codeUnderstanding,
        string generationProfile = "comprehensive")
    {
        var codeInsightSection = "";
        if (codeUnderstanding != null)
        {
            var modules = string.Join("\n", codeUnderstanding.DependencyTopology.Modules.Take(20)
                .Select(m => $"- {m.Name} ({m.ModuleType}, {m.FileCount} files)"));

            var deps = string.Join("\n", codeUnderstanding.DependencyTopology.Edges.Take(20)
                .Select(e => $"- {e.FromModule} → {e.ToModule}"));

            var patterns = string.Join("\n", codeUnderstanding.DesignPatterns.Take(10)
                .Select(p => $"- {p.PatternName} ({p.Confidence:P0}): {string.Join(", ", p.Participants.Select(pp => pp.SymbolName))}"));

            var archInsight = codeUnderstanding.ArchitectureInsight;
            var archSection = !string.IsNullOrEmpty(archInsight.ArchitecturePattern)
                ? $"架构模式：{archInsight.ArchitecturePattern}\n描述：{archInsight.PatternDescription}"
                : "";

            var layers = string.Join("\n", archInsight.Layers.Select(l =>
                $"- {l.Name}: {l.Responsibility}"));

            codeInsightSection = $"""

            DEEP CODE UNDERSTANDING RESULTS (from automated analysis):

            Architecture Pattern: {archSection}

            Module Dependencies:
            {modules}

            Key Dependency Edges:
            {deps}

            Detected Design Patterns:
            {patterns}

            Architecture Layers:
            {layers}

            Call Graph Summary: {codeUnderstanding.CallGraph.NodeCount} methods, {codeUnderstanding.CallGraph.Edges.Count} call edges, max depth {codeUnderstanding.CallGraph.MaxDepth}
            """;
        }

        return $$"""
## 角色

你是一位拥有 15 年经验的软件架构师和技术文档策略专家。你为 Spring Framework、Kubernetes、VS Code 等级别的开源项目设计过文档架构。你的核心能力是：从代码结构中洞察系统本质，设计出逻辑严密、层次清晰的文档地图。

## 上下文

以下是该代码仓库的完整分析数据：

**仓库**：{{owner}}/{{repo}}

**文件树**：
<file_tree>
{{fileTree}}
</file_tree>

**README**：
<readme>
{{readme}}
</readme>
{{codeInsightSection}}

目标语言：{{languageDisplayName}}
生成档位：{{(isComprehensiveView ? "完整型（50+ 页）" : "简洁型（20+ 页）")}}

## 分步指令

### 步骤 1：全局架构理解
- 从 README 和文件树推断项目类型（Web 应用 / 库 / CLI 工具 / 微服务等）
- 识别技术栈和 3-7 个核心功能域
- 若有代码理解数据，将其中的架构模式、设计模式、依赖拓扑作为核心参考

### 步骤 2：确定文档层级深度
- 文件数 < 50：2 层（章 → 页）
- 文件数 50-200：3 层（章 → 节 → 页）
- 文件数 200-500：4 层（章 → 节 → 子节 → 页）
- 文件数 > 500：4-5 层（章 → 节 → 子节 → 页 → 子页）

### 步骤 3：设计顶层章节（≤ 7 个）
每个章节覆盖一个独立的功能域或技术关注点。章节之间逻辑互斥、内容互补。

### 步骤 4：规划页面清单
- overview 页面（1-2 层）：架构全景、模块关系、设计理念
- section 页面（2-3 层）：模块分析、数据流、关键接口
- article 页面（3-5 层）：实现细节、代码深挖、逐方法分析
- 每个页面明确 pageType、depth（1-5）、contentDepthLevel

### 步骤 5：建立关联关系
- 为每个页面标记 1-3 个关联页面（relatedPages）
- 为需要前置知识的页面标记前置页面（prerequisitePages）

### 步骤 6：映射源文件
- 为每个页面分配 5-15 个最相关的源文件路径
- 文件选择依据：文件名/路径与页面主题的语义相关性

## 输出约束

{{GetWikiStructureFormatInstructions(isComprehensiveView)}}

核心格式规则：
1. 输出纯 JSON，以 `{` 开头 `}` 结尾，禁止代码围栏
2. `sections` 通过 `children` 递归形成树形结构
3. 每个页面必须指定 `depth`（1-5）、`contentDepthLevel`（overview/section/article/appendix）、`parentId`
4. `parentId` 必须引用页面 ID，不可引用章节 ID
5. 顶层页面 `parentId` 为 null

## 质量自查清单

1. □ 顶层章节是否 ≤ 7 个且互斥互补？
2. □ 页面总数是否合理（简介型 15-25、完整型 35-80）？
3. □ overview 页面是否放在 1-2 层、article 页面放在 3-5 层？
4. □ 每个 article 页面是否指定了 ≥ 5 个源文件？
5. □ 是否有循环依赖？
6. □ JSON 是否可以成功解析？
""";
    }

    /// <summary>
    /// 构建单个 Wiki 页面的生成提示词。
    /// </summary>
    /// <param name="page">目标页面 DTO。</param>
    /// <param name="allPages">所有页面。</param>
    /// <param name="repoOwner">仓库所有者。</param>
    /// <param name="repoName">仓库名称。</param>
    /// <param name="repoType">仓库类型。</param>
    /// <param name="repoUrl">仓库 URL。</param>
    /// <param name="languageDisplayName">输出语言。</param>
    /// <param name="fileContents">相关文件内容。</param>
    /// <param name="previousPageContext">V4 跨页面上下文——已生成页面的标题与摘要文本。</param>
    public string BuildWikiPagePrompt(
        WikiPageDto page, IEnumerable<WikiPageDto> allPages,
        string repoOwner, string repoName, string repoType, string? repoUrl,
        string languageDisplayName, string fileContents,
        string? previousPageContext = null)
    {
        var relatedPagesContext = string.Join('\n',
            page.RelatedPages
                .Select(relatedId => allPages.FirstOrDefault(item => string.Equals(item.Id, relatedId, StringComparison.OrdinalIgnoreCase)))
                .Where(relatedPage => relatedPage is not null)
                .Select(relatedPage => $"- {relatedPage!.Title}: {relatedPage.Description}"));

        var fileLinks = string.Join('\n', page.FilePaths.Select(path =>
            $"- [{path}]({BuildRepositoryFileUrl(repoType, repoUrl, repoOwner, repoName, path)})"));

        // 根据 ContentDepthLevel 构建差异化深度要求
        var depthGuidance = GetDepthGuidance(page.ContentDepthLevel);

        return $$"""
## 角色

你是一位资深技术文档撰写专家。你的文档被一线工程师用作日常开发参考。你的写作风格：精确、深入、以代码为证据、以图表辅助理解。你从不写空洞的概述——每一句话都有代码或架构事实支撑。

## 上下文

### 页面元数据
- 标题：{{page.Title}}
- 描述：{{page.Description}}
- 深度级别：{{page.ContentDepthLevel}}
- 目标语言：{{languageDisplayName}}

### 关联页面（避免内容重复）
{{(string.IsNullOrWhiteSpace(relatedPagesContext) ? "无" : relatedPagesContext)}}

### 已生成页面上下文（跨页面一致性）
{{(string.IsNullOrWhiteSpace(previousPageContext) ? "无" : previousPageContext)}}

### 真实源代码片段
以下是从仓库中检索到的与当前主题最相关的真实代码。**你只能使用这些片段中的代码作为依据**：
{{fileContents}}

### 源文件链接
{{fileLinks}}

## 分步指令

### 步骤 1：理解主题范围
- 仔细阅读页面标题和描述，确定本文档的精确范围边界
- 识别哪些内容属于本文档、哪些应留给关联页面
- 从代码片段中提取与主题直接相关的类、方法、接口

### 步骤 2：构建内容大纲（3-6 个 H2 小节）
- 按逻辑递进排列小节
- 每个 H2 聚焦一个独立的技术点
{{depthGuidance}}

### 步骤 3：撰写正文（以代码为中心）
- 每个断言必须引用至少一个真实代码片段作为证据
- 使用表格对比 API 参数、配置选项、类职责
- 在关键流程处插入 Mermaid 图表
- 代码引用格式：从上方提供的片段中提取，标注文件路径

### 步骤 4：生成 Mermaid 图表（至少 1 个）
- 架构图用 graph TD、调用流程用 sequenceDiagram、类关系用 classDiagram
- 时序图使用 autonumber 自动编号
- 图表节点文字简洁（3-4 词）

### 步骤 5：编写关联导航
- 在末尾"另见"区域列出关联页面链接

## 输出约束

返回纯 JSON（不要用代码围栏包裹），格式：
{
  "id": "{{page.Id}}",
  "title": "{{page.Title}}",
  "pageType": "overview|section|article|appendix",
  "parentId": "父页面 id 或 null",
  "relatedPages": ["page-id"],
  "frontMatter": { "summary": "...", "tags": [...] },
  "content": "Markdown 正文（以 <details><summary>📁 源文件参考</summary> 开头，随后是 H2/H3 正文、表格、Mermaid 图）"
}

内容约束：
1. 以 `<details><summary>📁 源文件参考</summary>` 折叠块开头
2. 使用 H2/H3 组织正文，不包含 H1 顶级标题
3. 必须引用真实代码证据（文件路径、类名、方法签名）
4. 至少 1 个表格 + 1 个 Mermaid 图 + 1 个列表
5. 禁止虚构代码——所有引用必须来自上方提供的真实片段
6. 若代码不足以完整描述某方面，注明"未在代码中找到对应实现"
7. 全部内容使用 {{languageDisplayName}}

## 质量自查清单

1. □ 所有类名、方法名是否来自真实代码片段？
2. □ 是否包含至少 1 个 Mermaid 图表和 1 个表格？
3. □ H2 小节是否在 3-6 个之间且有实质性内容？
4. □ 源文件引用块是否列出了 ≥ 5 个文件？
5. □ 是否与关联页面内容互补而非重复？
6. □ JSON 格式是否正确（双引号、无尾逗号、括号配对）？
""";
    }

    /// <summary>
    /// 根据页面深度级别返回差异化的内容深度要求。
    /// </summary>
    private static string GetDepthGuidance(string? contentDepthLevel)
    {
        return (contentDepthLevel?.ToLowerInvariant()) switch
        {
            "overview" =>
                "这是 OVERVIEW（架构全景）页面。聚焦系统架构鸟瞰图、模块间关系、设计理念和技术决策。必须包含 Mermaid 架构图。不要深入任何实现细节——留给子页面完成。目标 800-1200 字。",
            "section" =>
                "这是 SECTION（模块分析）页面。介绍模块职责和边界、数据流分析、关键类/接口概述、配置说明。必须包含至少 1 个 Mermaid 时序/类图 + 1 个接口或配置表格。引用具体文件和代码模式。目标 1200-2000 字。",
            "article" =>
                "这是 ARTICLE（实现细节）页面。提供完整的实现级别分析。必须直接引用源代码片段（方法签名、参数、返回值、异常处理）。解释算法逻辑、数据流转和边界条件。逐方法/逐类深挖。必须包含 Mermaid 时序图展示调用流程。目标 2000-3000+ 字。每一个断言都必须有代码证据。",
            _ =>
                "根据页面在 Wiki 中的角色提供适当深度的内容。引用代码证据、插入合适的图表、提供开发者所需的足够细节信息。"
        };
    }

    public string BuildSlidesPlanPrompt(string owner, string repo, string wikiContent, string languageDisplayName)
    {
        return $$"""
Create an engaging outline for a high-quality marketing slide presentation about the {{owner}}/{{repo}} repository.

Based on this wiki content:
{{wikiContent}}

I need a numbered list of 7-8 creative slide titles with brief descriptions for a professional marketing presentation. Think of this as a pitch deck that would impress potential users or investors.

Focus on:
- Compelling value propositions
- Unique selling points
- Impressive features and capabilities
- Real-world applications and benefits
- Visually interesting concepts that can be represented creatively

For example, instead of generic titles like "Introduction" or "Features", use more engaging titles like:
1. "Revolutionizing Development with {{repo}}"
2. "Unlock Powerful Capabilities with Our Innovative Architecture"
3. "How {{repo}} Transforms Your Workflow"

Give me the numbered list with brief descriptions for each slide. Be creative but professional.
Respond in {{languageDisplayName}}.
""";
    }

    public string BuildSlidePrompt(string owner, string repo, string slideTitle,
        string slideDescription, int slideIndex, int totalSlides, string wikiContent, string languageDisplayName)
    {
        var template = """
Create a single HTML slide about the __OWNER__/__REPO__ repository with the title "__TITLE__".

This is slide __INDEX__ of __TOTAL__ in the presentation.
__DESCRIPTION_LINE__

Use the following wiki content as reference:
__WIKI_CONTENT__

I need ONLY the HTML for this slide. The slide should maintain a consistent dark theme with gradients and professional styling, but BE CREATIVE with the content and layout.

IMPORTANT LAYOUT REQUIREMENTS:
1. The slide MUST be designed for a 16:9 HORIZONTAL layout (landscape orientation)
2. All content MUST fit within the visible area without requiring scrolling
3. Text must be properly sized and positioned for readability in a presentation context
4. Content should be well-structured with clear visual hierarchy
5. Use grid or flexbox layouts to ensure proper horizontal organization of content
6. Limit text content to what can be comfortably read from a distance

MARKETING QUALITY:
Create a genuinely high-quality marketing slide that would impress potential users or investors. Use compelling language, impactful visuals, and professional marketing techniques. Think of this as a slide for a professional pitch deck or product showcase.

You can use:
- Two or three-column layouts for better horizontal space utilization
- Engaging marketing copy with concise bullet points (no more than 4-5 per slide)
- Visual metaphors and analogies positioned to the side of text content
- Charts, diagrams, or code snippets when relevant (positioned appropriately)
- Icons from Font Awesome (already included)
- Creative use of gradients, shadows, and visual elements

The slide should maintain the dark theme aesthetic but can be uniquely designed. Use creative HTML/CSS to make the slide visually impressive while ensuring all content fits properly in the horizontal layout.

Please write the slide content in __LANGUAGE__.

Here's a basic structure to build upon (but feel free to be creative):

<div class="slide">
    <div class="code-pattern"></div>
    <div class="accent-glow"></div>
    <div class="content">
        <div class="slide-header">
            <h1 class="main-title">__TITLE__</h1>
        </div>
        <div class="slide-body">
            <div class="left-column">
            </div>
            <div class="right-column">
            </div>
        </div>
    </div>
</div>
<style>
    .slide {
        width: 100%;
        height: 100%;
        background: linear-gradient(135deg, #0d1117 0%, #161b22 100%);
        color: #e6edf3;
        display: flex;
        flex-direction: column;
        overflow: hidden;
    }
    .content {
        display: flex;
        flex-direction: column;
        height: 100%;
        padding: 40px 60px;
        z-index: 2;
    }
    .slide-header {
        margin-bottom: 30px;
    }
    .slide-body {
        display: flex;
        flex: 1;
        gap: 40px;
    }
    .left-column, .right-column {
        flex: 1;
        display: flex;
        flex-direction: column;
    }
</style>

Please return ONLY the HTML with no markdown formatting or code blocks. Just the raw HTML for the slide.
""";

        return template
            .Replace("__OWNER__", owner)
            .Replace("__REPO__", repo)
            .Replace("__TITLE__", slideTitle)
            .Replace("__INDEX__", slideIndex.ToString())
            .Replace("__TOTAL__", totalSlides.ToString())
            .Replace("__DESCRIPTION_LINE__", string.IsNullOrWhiteSpace(slideDescription) ? string.Empty : $"The slide should cover: {slideDescription}")
            .Replace("__WIKI_CONTENT__", wikiContent)
            .Replace("__LANGUAGE__", languageDisplayName);
    }

    public string BuildWorkshopPrompt(string owner, string repo, string wikiContent, string languageDisplayName)
    {
        return $$"""
Create a comprehensive workshop for learning how to use and contribute to the {{owner}}/{{repo}} repository.

I'll provide you with information from the project's wiki to help you create a more accurate and relevant workshop.

{{wikiContent}}

This workshop should be designed as a hands-on tutorial that guides users through understanding, using, and potentially contributing to this project. The workshop should be highly readable and optimized for quick onboarding of new users.

The workshop should include:

1. A series of progressive exercises that build on each other (at least 3-4 exercises)
2. Clear instructions for each exercise with step-by-step guidance
3. Code examples and snippets where appropriate
4. "Challenge" sections that encourage deeper exploration
5. Solutions for each exercise and challenge (in collapsible sections using <details> tags)
6. Explanations that connect the exercises to the actual codebase

Format the workshop in Markdown with the following structure:

# {{repo}} Workshop

## Introduction
- Brief overview of the project
- What users will learn in this workshop
- Prerequisites and setup instructions

## Exercise 1: [First Core Concept]
- Explanation of the concept
- Step-by-step instructions with clear formatting
- Expected outcome
- Challenge (optional harder task)
- Solution (in a collapsible section using <details> tags)

## Exercise 2: [Second Core Concept]
...

## Exercise 3: [Third Core Concept]
...

## Final Project
- A culminating exercise that brings together multiple concepts
- Clear success criteria
- Solution

## Next Steps
- Suggestions for further learning
- How to contribute to the project
- Additional resources

IMPORTANT FORMATTING GUIDELINES:
1. Use clear headings and subheadings with proper hierarchy
2. Use bullet points and numbered lists for clarity
3. Highlight important information in **bold** or with blockquotes
4. Use code blocks with proper syntax highlighting
5. Include Mermaid diagrams where they would help illustrate concepts or workflows
6. Put solutions in collapsible <details> sections
7. Use tables for comparing options or summarizing information
8. Break long sections into smaller, digestible chunks
9. Use consistent formatting throughout

IMPORTANT CONTENT GUIDELINES:
1. Make sure each exercise focuses on a REAL aspect of the {{repo}} repository
2. Use REAL code examples from the repository, not generic examples
3. Create exercises that are practical and relevant to the actual codebase
4. Include at least 3-4 exercises covering different aspects of the repository
5. The final project should be challenging but achievable
6. Ensure the workshop is specific to this repository, not generic
7. Focus on the most important/core features of the repository
8. Include diagrams to visualize complex concepts
9. Make sure the workshop is engaging and interactive

Make the workshop content in {{languageDisplayName}} language.
""";
    }

    public string BuildWikiReferenceText(WikiGenerationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Project Overview");
        builder.AppendLine(result.WikiStructure.Description);
        builder.AppendLine();

        var pages = result.WikiStructure.Pages;
        var totalContentLength = 0;
        const int maxContentLength = 30000;

        foreach (var page in pages.Where(p => p.Importance == "high"))
        {
            totalContentLength = AppendPageContent(builder, result.GeneratedPages, page, totalContentLength, maxContentLength);
            if (totalContentLength > maxContentLength) return builder.ToString();
        }

        foreach (var page in pages.Where(p => p.Importance != "high"))
        {
            totalContentLength = AppendPageContent(builder, result.GeneratedPages, page, totalContentLength, maxContentLength);
            if (totalContentLength > maxContentLength) break;
        }

        return builder.ToString();
    }

    private static int AppendPageContent(StringBuilder builder,
        Dictionary<string, WikiPageDto> generatedPages, WikiPageDto page, int currentLen, int maxLen)
    {
        if (!generatedPages.TryGetValue(page.Id, out var generated) || string.IsNullOrWhiteSpace(generated.Content))
            return currentLen;

        var fullContent = $"## {page.Title}\n{generated.Content}\n\n";
        if (currentLen + fullContent.Length <= maxLen)
        {
            builder.Append(fullContent);
            return currentLen + fullContent.Length;
        }

        var summary = ExtractSummary(generated.Content);
        var summaryText = $"## {page.Title}\n{summary}\n\n";
        builder.Append(summaryText);
        return currentLen + summaryText.Length;
    }

    private static string ExtractSummary(string content)
    {
        var parts = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.StartsWith("```")).Take(2).ToList();
        return parts.Count == 0 ? "No summary available" : string.Join("\n\n", parts);
    }

    private string GetWikiStructureFormatInstructions(bool isComprehensiveView)
    {
        return isComprehensiveView
            ? """
Create a structured wiki with the following main sections:
- Overview (general information about the project)
- System Architecture (how the system is designed)
- Core Features (key functionality)
- Data Management/Flow (how data is stored, processed, accessed, and managed)
- Frontend Components (if applicable)
- Backend Systems (if applicable)
- Model Integration (if applicable)
- Deployment/Infrastructure
- Extensibility and Customization

Return your analysis in the following JSON format:

{
  "id": "wiki",
  "title": "[Overall title for the wiki]",
  "description": "[Brief description of the repository]",
  "rootSections": ["section-1"],
  "sections": [
    {
      "id": "section-1",
      "title": "[Section title]",
      "pages": ["page-1"],
      "subsections": ["section-2"],
      "children": [],
      "depth": 0
    }
  ],
  "pages": [
    {
      "id": "page-1",
      "title": "[Page title]",
      "description": "[Brief description of what this page will cover]",
      "navTitle": "[Short nav title]",
      "pageType": "overview|section|article|appendix",
      "importance": "high|medium|low",
      "depth": 0,
      "contentDepthLevel": "overview|module|component|implementation",
      "filePaths": ["[Path to a relevant file]"],
      "searchKeywords": ["keyword1", "keyword2"],
      "keyFilePaths": ["[Must-include file path]"],
      "relatedPages": ["page-2"],
      "prerequisitePages": ["page-0"],
      "parentId": null
    }
  ]
}
"""
            : """
Return your analysis in the following JSON format:

{
  "id": "wiki",
  "title": "[Overall title for the wiki]",
  "description": "[Brief description of the repository]",
  "pages": [
    {
      "id": "page-1",
      "title": "[Page title]",
      "description": "[Brief description of what this page will cover]",
      "navTitle": "[Short nav title]",
      "pageType": "overview|section|article|appendix",
      "importance": "high|medium|low",
      "depth": 0,
      "contentDepthLevel": "overview|module|component|implementation",
      "filePaths": ["[Path to a relevant file]"],
      "searchKeywords": ["keyword1", "keyword2"],
      "relatedPages": ["page-2"],
      "prerequisitePages": ["page-0"],
      "parentId": null
    }
  ]
}
""";
    }

    private static string BuildRepositoryFileUrl(string repoType, string? repoUrl, string owner, string repo, string filePath)
    {
        if (repoType == "local" || string.IsNullOrWhiteSpace(repoUrl)) return filePath;

        var normalized = repoUrl.TrimEnd('/').Replace(".git", "");
        return repoType switch
        {
            "gitlab" => $"{normalized}/-/blob/main/{filePath}",
            "bitbucket" => $"{normalized}/src/main/{filePath}",
            _ => $"{normalized}/blob/main/{filePath}"
        };
    }

    /// <summary>
    /// 构建包含深度代码分析结果的增强型 Wiki 结构规划提示词（V4）。
    /// 将系统级摘要、模块级摘要与入口文件信息注入规划上下文，
    /// 使生成的 Wiki 结构基于实际代码语义而非仅依赖文件树与 README。
    /// </summary>
    /// <param name="owner">仓库所有者。</param>
    /// <param name="repo">仓库名称。</param>
    /// <param name="fileTree">文件树文本。</param>
    /// <param name="readme">README 内容。</param>
    /// <param name="languageDisplayName">输出语言展示名。</param>
    /// <param name="isComprehensiveView">是否完整视角。</param>
    /// <param name="generationProfile">生成档位。</param>
    /// <param name="systemSummaryText">系统级架构摘要文本（由 CodeSummaryService 产出）。</param>
    /// <param name="moduleSummariesText">模块级摘要文本，每个模块一行描述。</param>
    /// <param name="recommendedPageCount">基于代码复杂度推荐的页面数量。</param>
    /// <returns>增强型结构规划提示词。</returns>
    public string BuildEnhancedWikiStructurePrompt(
        string owner, string repo, string fileTree, string readme,
        string languageDisplayName, bool isComprehensiveView,
        string generationProfile, string? systemSummaryText,
        string? moduleSummariesText, int recommendedPageCount)
    {
        var hasAnalysis = !string.IsNullOrWhiteSpace(systemSummaryText);
        var analysisSection = hasAnalysis
            ? $"""
<code_analysis>
系统架构概述：
{systemSummaryText}

模块级摘要：
{moduleSummariesText}

基于以上分析，建议生成约 {recommendedPageCount} 个页面。
</code_analysis>
"""
            : string.Empty;

        return $$"""
你是资深软件架构师和技术文档专家。分析此仓库并创建逻辑全面的 Wiki 结构。

步骤 1：仓库分析
分析 {{owner}}/{{repo}} 仓库的架构、用途和关键组件：

1. 完整文件树：
<file_tree>
{{fileTree}}
</file_tree>

2. README 内容：
<readme>
{{readme}}
</readme>

{{analysisSection}}
步骤 2：架构理解
基于文件结构和以上分析，识别：
1. **项目类型与架构**：Web 应用、库、CLI 工具？主要技术栈？
2. **核心系统组件**：入口点、关键模块/包、数据层、UI 组件、配置与构建系统
3. **关键关系与依赖**：模块间交互、数据流、外部依赖
4. **开发与部署工作流**：构建方式、测试结构、部署流程

步骤 3：Wiki 结构设计
创建提供深度技术洞察的 Wiki 结构。重点关注：
- **系统架构**：组件之间如何交互
- **实现细节**：关键算法、数据结构、设计模式
- **集成点**：API、数据库、外部服务
- **开发工作流**：设置、测试、部署流程
- **可扩展性**：如何扩展或修改系统

{{(hasAnalysis ? $"目标页面数：约 {recommendedPageCount} 页，3 层目录嵌套。" : $"创建 {(isComprehensiveView ? "8-12" : "4-6")} 个提供深度技术洞察的页面。")}}

步骤 4：智能文件映射
每个页面必须识别最相关的源文件。关键要求：
- 每个页面至少 8-10 个相关源文件
- 文件应直接关联页面主题
- 包含核心实现文件、配置文件和辅助模块
- 优先包含页面主题核心逻辑的文件

{{GetWikiStructureFormatInstructions(isComprehensiveView)}}

格式化说明：
- 仅返回下方指定的有效 JSON 对象
- 不要在 markdown 代码块中包装 JSON
- 不要在 JSON 前后包含解释文本
- 直接以 { 开头以 } 结尾
- 所有数组必须仅包含符合模式的字符串 ID 或对象
- `parentId` 必须引用另一个页面 ID 或为 null

关键要求：
1. 创建 {{(hasAnalysis ? recommendedPageCount.ToString() : (isComprehensiveView ? "8-12" : "4-6"))}} 个提供深度技术洞察的页面
2. 每页聚焦特定方面并提供全面分析（非表面描述）
3. `relevant_files` 必须精心选择包含每页核心实现的实际文件
4. 确保页面之间重叠最小——每页覆盖系统不同方面
5. 页面描述应具体且技术化，说明将覆盖哪些实现细节
6. 优先包含：详细代码分析和架构模式、系统集成点和数据流、性能考虑和优化、可扩展机制和设计决策
7. 仅返回上述指定结构的有效 JSON，不带 markdown 代码块分隔符
8. `sections.pages` 应包含已在 `pages` 中定义的页面 ID
9. 仓库级入口页面优先使用 `pageType=overview`，主题着陆页使用 `section`，深度技术页使用 `article`
10. 存在有意义的交叉引用时，为每页提供至少 1-3 个 `relatedPages`

质量检查清单：
- 各页面是否有清晰、不重叠的技术焦点？
- `relevant_files` 是否直接关联页面核心功能？
- 此页面结构是否能够产生深度技术文档而非表面概述？
- 页面描述是否具体到足以指导全面的内容生成？

以 {{languageDisplayName}} 输出。
""";
    }
}
