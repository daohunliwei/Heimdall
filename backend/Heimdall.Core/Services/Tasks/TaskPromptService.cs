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
        string languageDisplayName, bool isComprehensiveView)
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
- Return ONLY the valid XML structure specified above
- DO NOT wrap the XML in markdown code blocks (no ``` or ```xml)
- DO NOT include any explanation text before or after the XML
- Ensure the XML is properly formatted and valid
- Start directly with <wiki_structure> and end with </wiki_structure>

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
7. Return ONLY valid XML with the structure specified above, with no markdown code block delimiters

QUALITY CHECKLIST before generating XML:
- Does each page have a clear, non-overlapping technical focus?
- Are the relevant_files directly related to the page's core functionality?
- Will this page structure enable deep technical documentation rather than superficial overviews?
- Are the page descriptions specific enough to guide comprehensive content generation?
""";
    }

    public string BuildWikiPagePrompt(
        WikiPageDto page, IEnumerable<WikiPageDto> allPages,
        string repoOwner, string repoName, string repoType, string? repoUrl, string languageDisplayName)
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

You will be given:
1. The "[WIKI_PAGE_TOPIC]" for the page you need to create.
2. A list of "[RELEVANT_SOURCE_FILES]" from the project that you MUST use as the sole basis for the content. You have access to the full content of these files through the RAG system.

CRITICAL: This page should provide UNIQUE, NON-OVERLAPPING content focused specifically on "{{page.Title}}". Avoid generic descriptions that could apply to any system. Instead, focus on:
- Specific implementation details found in the source files
- Unique architectural decisions and patterns used in this particular system
- Concrete code examples and technical specifics
- How this component/feature integrates with other parts of the system

CRITICAL STARTING INSTRUCTION:
The very first thing on the page MUST be a `<details>` block listing ALL the `[RELEVANT_SOURCE_FILES]` you used to generate the content. There MUST be AT LEAST 5 source files listed - if fewer were provided, you MUST find additional related files to include.
Format it exactly like this:
<details>
<summary>Relevant source files</summary>

Remember, do not provide any acknowledgements, disclaimers, apologies, or any other preface before the `<details>` block. JUST START with the `<details>` block.
The following files were used as context for generating this wiki page:

{{fileLinks}}
<!-- Add additional relevant files if fewer than 5 were provided -->
</details>

Immediately after the `<details>` block, the main title of the page should be a H1 Markdown heading: `# {{page.Title}}`.

Based ONLY on the content of the `[RELEVANT_SOURCE_FILES]`:

1. **Introduction:** Start with a concise introduction (1-2 paragraphs) explaining the SPECIFIC purpose, scope, and implementation details of "{{page.Title}}" within this project. Focus on what makes this component/feature unique in this codebase rather than generic descriptions. Include specific technical details found in the source files. If relevant, link to other wiki pages using the format `[Link Text](#page-anchor-or-id)`.

2. **Detailed Sections:** Break down "{{page.Title}}" into logical sections using H2 (`##`) and H3 (`###`) Markdown headings. For each section:
   - Provide DEEP TECHNICAL ANALYSIS of the architecture, components, data flow, or logic, with specific references to the source code
   - Identify and explain key functions, classes, data structures, API endpoints, or configuration elements with their actual implementations
   - Focus on HOW things work in this specific codebase, not just WHAT they do
   - Include performance considerations, design trade-offs, and architectural decisions evident in the code

3. **Mermaid Diagrams:**
   - EXTENSIVELY use Mermaid diagrams (e.g., `flowchart TD`, `sequenceDiagram`, `classDiagram`, `erDiagram`, `graph TD`) to visually represent architectures, flows, relationships, and schemas found in the source files.
   - Ensure diagrams are accurate and directly derived from information in the `[RELEVANT_SOURCE_FILES]`.
   - Provide a brief explanation before or after each diagram to give context.
   - CRITICAL: All diagrams MUST follow strict vertical orientation:
     - Use "graph TD" (top-down) directive for flow diagrams
     - NEVER use "graph LR" (left-right)
     - Maximum node width should be 3-4 words
     - For sequence diagrams, define all participants first and use Mermaid standard arrow syntax

4. **Tables:**
   - Use Markdown tables to summarize key components, API parameters, configuration options, or data model fields.

5. **Code Snippets (ENTIRELY OPTIONAL):**
   - Include short, relevant code snippets directly from the source files to illustrate implementation details.

6. **Source Citations (EXTREMELY IMPORTANT):**
   - For EVERY significant explanation, diagram, table entry, or code snippet, cite the specific source file(s) and relevant line numbers.
   - Use repository-accessible links when possible.
   - IMPORTANT: You MUST cite AT LEAST 5 different source files throughout the wiki page to ensure comprehensive coverage.

7. **Technical Accuracy:** All information must be derived SOLELY from the `[RELEVANT_SOURCE_FILES]`. Do not infer, invent, or use external knowledge unless directly supported by the provided code.

8. **Clarity and Conciseness:** Use clear, professional, and concise technical language suitable for other developers.

9. **Conclusion/Summary:** End with a brief summary paragraph if appropriate for "{{page.Title}}", reiterating the key aspects covered and their significance within the project.

IMPORTANT: Generate the content in {{languageDisplayName}} language.
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

Return your analysis in the following XML format:

XML 必须严格合法，所有开始标签都要使用同名结束标签闭合。
特别注意：`<parent_section>` 必须以 `</parent_section>` 结束，不能写成 `</section>`。

<wiki_structure>
  <title>[Overall title for the wiki]</title>
  <description>[Brief description of the repository]</description>
  <sections>
    <section id="section-1">
      <title>[Section title]</title>
      <pages>
        <page_ref>page-1</page_ref>
      </pages>
      <subsections>
        <section_ref>section-2</section_ref>
      </subsections>
    </section>
  </sections>
  <pages>
    <page id="page-1">
      <title>[Page title]</title>
      <description>[Brief description of what this page will cover]</description>
      <importance>high|medium|low</importance>
      <relevant_files>
        <file_path>[Path to a relevant file]</file_path>
      </relevant_files>
      <related_pages>
        <related>page-2</related>
      </related_pages>
      <parent_section>section-1</parent_section>
    </page>
  </pages>
</wiki_structure>
"""
            : """
Return your analysis in the following XML format:

<wiki_structure>
  <title>[Overall title for the wiki]</title>
  <description>[Brief description of the repository]</description>
  <pages>
    <page id="page-1">
      <title>[Page title]</title>
      <description>[Brief description of what this page will cover]</description>
      <importance>high|medium|low</importance>
      <relevant_files>
        <file_path>[Path to a relevant file]</file_path>
      </relevant_files>
      <related_pages>
        <related>page-2</related>
      </related_pages>
    </page>
  </pages>
</wiki_structure>
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
