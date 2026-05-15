using System.Text;
using Heimdall.Core.Models;
using Heimdall.Infrastructure.Models;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// 任务提示词服务，承载前端迁移过来的任务编排提示词。
/// </summary>
public sealed class TaskPromptService
{
    public string BuildWikiStructurePrompt(
        string owner, string repo, string fileTree, string readme,
        string languageDisplayName, bool isComprehensiveView,
        string generationProfile = "comprehensive")
    {
        return $$"""
You are an expert software architect and technical documentation specialist. Your task is to analyze this repository and create a logical, comprehensive wiki structure.

STEP 1: REPOSITORY ANALYSIS
Analyze this {{owner}}/{{repo}} repository to understand its architecture, purpose, and key components:

1. Complete file tree:
<file_tree>
{{fileTree}}
</file_tree>

2. README content:
<readme>
{{readme}}
</readme>

STEP 2: ARCHITECTURAL UNDERSTANDING
Based on the file structure and README, identify:

1. **Project Type & Architecture**:
   - Is this a web application, library, CLI tool, mobile app, etc.?
   - What's the primary technology stack (React, Python, Java, etc.)?
   - What architectural patterns are used (MVC, microservices, monolith, etc.)?

2. **Core System Components**:
   - Main application entry points
   - Key modules/packages and their responsibilities
   - Data layer (databases, APIs, storage)
   - User interface components (if applicable)
   - Configuration and deployment files
   - Testing and build infrastructure

3. **Key Relationships & Dependencies**:
   - How do different modules interact?
   - What are the main data flows?
   - What external dependencies exist?

4. **Development & Deployment Workflow**:
   - How is the project built and deployed?
   - What development tools are used?
   - How is testing structured?

STEP 3: WIKI STRUCTURE DESIGN
Create a wiki structure that provides deep technical insight rather than surface-level descriptions. Focus on:

- **System Architecture**: Deep dive into how components interact
- **Implementation Details**: Key algorithms, data structures, and design patterns
- **Integration Points**: APIs, databases, external services
- **Development Workflow**: Setup, testing, deployment processes
- **Extensibility**: How to extend or modify the system

I want to create a wiki for this repository. Determine the most logical structure for a wiki based on the repository's content and architectural analysis.

IMPORTANT: The wiki content will be generated in {{languageDisplayName}} language.

When designing the wiki structure, include pages that would benefit from visual diagrams, such as:
- Architecture overviews
- Data flow descriptions
- Component relationships
- Process workflows
- State machines
- Class hierarchies

STEP 4: INTELLIGENT FILE MAPPING
For each page you create, you MUST identify the most relevant source files by analyzing:

1. **File Purpose Analysis**: Look at file names, extensions, and directory structure to understand what each file does
2. **Dependency Relationships**: Identify which files import/require others
3. **Functional Grouping**: Group files that work together to implement specific features
4. **Entry Points**: Identify main files, configuration files, and key implementation files

CRITICAL REQUIREMENTS for relevant_files:
- Each page MUST have AT LEAST 8-10 relevant source files
- Files should be directly related to the page topic, not just randomly selected
- Include a mix of: main implementation files, configuration files, and supporting modules
- Prioritize files that contain the core logic for the page's topic
- Avoid including only test files or documentation files unless the page is specifically about testing/docs

Examples of good file selection:
- For "Authentication System" page: auth.js, login.component.tsx, auth.config.js, user.model.js, auth.middleware.js
- For "Database Layer" page: database.js, models/*, migrations/*, db.config.js, schema.sql
- For "API Endpoints" page: routes/*, controllers/*, middleware/*, api.config.js, swagger.yaml

{{GetWikiStructureFormatInstructions(isComprehensiveView)}}

IMPORTANT FORMATTING INSTRUCTIONS:
- Return ONLY the valid JSON object specified above
- DO NOT wrap the JSON in markdown code blocks
- DO NOT include any explanation text before or after the JSON
- Start directly with { and end with }
- All arrays must contain only string IDs or objects matching the schema
- `parentId` MUST reference another page id or be null; do not use section id in `parentId`

CRITICAL REQUIREMENTS:
1. Create {{(isComprehensiveView ? "8-12" : "4-6")}} pages that provide DEEP TECHNICAL INSIGHT into this repository
2. Each page should focus on a specific aspect with COMPREHENSIVE ANALYSIS (not surface-level descriptions)
3. The relevant_files MUST be carefully selected actual files that contain the core implementation for each page topic
4. Ensure MINIMAL OVERLAP between pages - each should cover distinct aspects of the system
5. Page descriptions should be SPECIFIC and TECHNICAL, indicating what implementation details will be covered
6. Prioritize pages that will include:
   - Detailed code analysis and architectural patterns
   - System integration points and data flows
   - Performance considerations and optimizations
   - Extensibility mechanisms and design decisions
7. Return ONLY valid JSON with the structure specified above, with no markdown code block delimiters
8. `sections.pages` should contain page ids already defined in `pages`
9. Prefer `pageType=overview` for repository-level entry pages, `section` for topic landing pages, `article` for deep technical pages
10. Provide at least 1-3 `relatedPages` for each page whenever there is a meaningful cross-reference

QUALITY CHECKLIST before generating JSON:
- Does each page have a clear, non-overlapping technical focus?
- Are the relevant_files directly related to the page's core functionality?
- Will this page structure enable deep technical documentation rather than superficial overviews?
- Are the page descriptions specific enough to guide comprehensive content generation?
""";
    }

    public string BuildWikiPagePrompt(
        WikiPageDto page, IEnumerable<WikiPageDto> allPages,
        string repoOwner, string repoName, string repoType, string? repoUrl,
        string languageDisplayName, string fileContents)
    {
        var relatedPagesContext = string.Join('\n',
            page.RelatedPages
                .Select(relatedId => allPages.FirstOrDefault(item => string.Equals(item.Id, relatedId, StringComparison.OrdinalIgnoreCase)))
                .Where(relatedPage => relatedPage is not null)
                .Select(relatedPage => $"- {relatedPage!.Title}: {relatedPage.Description}"));

        var fileLinks = string.Join('\n', page.FilePaths.Select(path =>
            $"- [{path}]({BuildRepositoryFileUrl(repoType, repoUrl, repoOwner, repoName, path)})"));

        return $$"""
You are an expert technical writer and software architect.
Your task is to generate a comprehensive and accurate technical wiki page in Markdown format about a specific feature, system, or module within a given software project.

CONTEXT AWARENESS: This wiki has multiple pages. You are generating content for "{{page.Title}}" specifically.
{{(string.IsNullOrWhiteSpace(relatedPagesContext) ? string.Empty : $"\nRelated pages in this wiki:\n{relatedPagesContext}\n\nEnsure your content is DISTINCT from these related pages and does not duplicate their coverage.")}}

## [WIKI_PAGE_TOPIC]
Title: {{page.Title}}
Description: {{page.Description}}

## [RELEVANT_SOURCE_FILES]
The following are the ACTUAL source file contents from the repository. You MUST use these as the sole basis for your analysis. Do NOT invent or infer anything not present in these files.

{{fileContents}}

## File Reference Links:
{{fileLinks}}

CRITICAL: This page should provide UNIQUE, NON-OVERLAPPING content focused specifically on "{{page.Title}}". Analyze the REAL source code provided above and generate content based on what you ACTUALLY SEE in the files.

Return ONLY one valid JSON object with the following schema:
{
  "id": "{{page.Id}}",
  "title": "{{page.Title}}",
  "description": "页面描述，可比原始描述更具体",
  "navTitle": "用于导航的短标题",
  "pageType": "overview|section|article|appendix",
  "importance": "high|medium|low",
  "parentId": "父页面 id 或 null",
  "filePaths": ["必须是与页面直接相关的仓库文件路径"],
  "relatedPages": ["page-id"],
  "prerequisitePages": ["page-id"],
  "frontMatter": {
    "summary": "一段可用于 Frontmatter 的摘要",
    "description": "一段可用于 Frontmatter 的描述",
    "tags": ["标签1", "标签2"],
    "sourceFiles": ["与页面强相关的文件路径"]
  },
  "outline": [
    { "level": 2, "title": "章节标题", "anchor": "chapter-anchor" }
  ],
  "sourceCoverage": {
    "primaryFiles": ["核心文件路径"],
    "evidence": [
      {
        "filePath": "文件路径",
        "reason": "为什么该文件支撑当前页面",
        "symbols": ["类名", "方法名"]
      }
    ]
  },
  "content": "仅包含 Markdown 正文，不要包含 Frontmatter，不要包含 <details>，正文内部必须包含 H2/H3 分节、表格或列表，并在合适位置使用 Mermaid。"
}

Markdown 正文写作要求：
1. 正文不要包含最外层 Frontmatter，也不要包含 `# {{page.Title}}` 顶级标题。
2. 必须使用 `##` / `###` 组织结构，聚焦实现细节而非泛泛介绍。
3. 必须引用真实代码证据，明确指出文件、类、方法或配置。
4. 至少提供一个表格、一个列表；当适合时提供 Mermaid 图。
5. 不要输出 HTML 页面，不要输出 XML，不要输出额外解释文本。
6. 如果信息不足，需在 Markdown 中明确说明“当前源文件未提供足够证据”。
7. 所有内容必须使用 {{languageDisplayName}}。
""";
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
      "subsections": ["section-2"]
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
      "filePaths": ["[Path to a relevant file]"],
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
      "filePaths": ["[Path to a relevant file]"],
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
}
