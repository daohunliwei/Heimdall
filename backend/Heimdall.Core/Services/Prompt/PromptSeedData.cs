using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Prompt;

/// <summary>
/// 系统内置提示词种子数据——V5 中文提示词，基于 deepwiki-open 原始英文版重写。
/// 同时支持种子代码初始化与 SQL 脚本独立恢复。
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
        var seeds = GetSeedTemplates();

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

        _logger.LogInformation("提示词种子数据初始化完成，共 {Count} 个模板", seeds.Length);
    }

    public static PromptTemplate[] GetSeedTemplates()
    {
        return new[]
        {
            // ═══════════════════════════════════════════════════════
            // Wiki 结构规划 — 基础任务提示词
            // ═══════════════════════════════════════════════════════
            new PromptTemplate
            {
                Slug = "wiki-structure-planning",
                Name = "Wiki 结构规划",
                Category = "wiki_structure",
                SubCategory = "base",
                Priority = 10,
                ApplicableProviders = null,
                TemplateContent = WikiStructureTemplate,
                Variables = new[] { "file_tree", "readme", "language", "generation_profile", "recommended_page_count", "file_count" },
                IsSystem = true,
            },
            // Wiki 结构规划 — JSON 格式约束
            new PromptTemplate
            {
                Slug = "wiki-structure-json-format",
                Name = "Wiki 结构 JSON 格式指令",
                Category = "wiki_structure",
                SubCategory = "format",
                Priority = 20,
                ApplicableProviders = null,
                TemplateContent = WikiStructureJsonFormatTemplate,
                IsSystem = true,
            },
            // Ollama 专用结构规划格式强化
            new PromptTemplate
            {
                Slug = "wiki-structure-ollama-format",
                Name = "Ollama 结构规划格式强化",
                Category = "wiki_structure",
                SubCategory = "format",
                Priority = 30,
                ApplicableProviders = new[] { "ollama" },
                TemplateContent = OllamaJsonEnforcementTemplate,
                IsSystem = true,
            },

            // ═══════════════════════════════════════════════════════
            // Wiki 页面生成 — 基础任务提示词
            // ═══════════════════════════════════════════════════════
            new PromptTemplate
            {
                Slug = "wiki-page-generation",
                Name = "Wiki 页面生成",
                Category = "wiki_page",
                SubCategory = "base",
                Priority = 10,
                ApplicableProviders = null,
                TemplateContent = WikiPageTemplate,
                Variables = new[] { "page_title", "page_description", "retrieved_code_snippets", "language", "repo_owner", "repo_name", "related_pages", "prerequisite_pages" },
                IsSystem = true,
            },
            // Wiki 页面 — Markdown 样式规范
            new PromptTemplate
            {
                Slug = "wiki-page-markdown-rules",
                Name = "Wiki 页面 Markdown 样式规范",
                Category = "wiki_page",
                SubCategory = "format",
                Priority = 20,
                ApplicableProviders = null,
                TemplateContent = WikiPageMarkdownRulesTemplate,
                IsSystem = true,
            },

            // ═══════════════════════════════════════════════════════
            // 问答（Ask）
            // ═══════════════════════════════════════════════════════
            new PromptTemplate
            {
                Slug = "ask-query",
                Name = "问答对话",
                Category = "ask",
                SubCategory = "base",
                Priority = 10,
                ApplicableProviders = null,
                TemplateContent = AskTemplate,
                Variables = new[] { "repository_context", "language", "question" },
                IsSystem = true,
            },

            // ═══════════════════════════════════════════════════════
            // Slides 演示文稿生成
            // ═══════════════════════════════════════════════════════
            new PromptTemplate
            {
                Slug = "slides-generation",
                Name = "演示文稿生成",
                Category = "slides",
                SubCategory = "base",
                Priority = 10,
                ApplicableProviders = null,
                TemplateContent = SlidesTemplate,
                Variables = new[] { "wiki_content", "language", "repo_name", "total_slides" },
                IsSystem = true,
            },

            // ═══════════════════════════════════════════════════════
            // Workshop 训练营材料生成
            // ═══════════════════════════════════════════════════════
            new PromptTemplate
            {
                Slug = "workshop-generation",
                Name = "训练营材料生成",
                Category = "workshop",
                SubCategory = "base",
                Priority = 10,
                ApplicableProviders = null,
                TemplateContent = WorkshopTemplate,
                Variables = new[] { "wiki_content", "language", "repo_name" },
                IsSystem = true,
            },

            // 代码摘要模板已移除（V6：不再使用三级 LLM 摘要）

            // ═══════════════════════════════════════════════════════
            // 聊天 System Prompt（ChatController SSE 端点）
            // ═══════════════════════════════════════════════════════
            new PromptTemplate
            {
                Slug = "chat-system",
                Name = "聊天助手角色设定",
                Category = "chat",
                SubCategory = "system",
                Priority = 10,
                ApplicableProviders = null,
                TemplateContent = ChatSystemTemplate,
                IsSystem = true,
            },

            // ═══════════════════════════════════════════════════════
            // Provider 个性片段
            // ═══════════════════════════════════════════════════════
            new PromptTemplate
            {
                Slug = "provider-ollama-system",
                Name = "Ollama Provider 系统角色",
                Category = "general",
                SubCategory = "provider_system",
                Priority = 1,
                ApplicableProviders = new[] { "ollama" },
                TemplateContent = OllamaSystemRoleTemplate,
                IsSystem = true,
            },
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // ── 模板内容定义 ──
    // ═══════════════════════════════════════════════════════════════

    private const string WikiStructureTemplate = """
        你是一位资深技术文档专家和软件架构师。你的任务是深入分析该代码仓库的文件树和README，为其设计一个专业、层次清晰的Wiki文档结构。

        ## 分析步骤

        1. **通览全局**：先查看 README 了解项目定位、技术栈和主要功能；再浏览文件树，识别核心模块和代码组织方式。
        2. **分层规划**：按照从宏观到微观的顺序设计文档层级——先确定顶层章节（如概述、架构、核心模块等），再为每个章节细分子章节和具体页面。
        3. **源文件映射**：为每个页面（不要为章节）指定相关的源文件路径，确保文档与代码之间有清晰的追溯关系。

        ## 层级深度规则

        根据仓库规模和复杂度，自行判断合理的目录深度：
        - 小仓库（< 50 个文件）：至少 2 层（章节 → 页面）
        - 中型仓库（50-200 个文件）：至少 3 层（章 → 节 → 页面）
        - 大型仓库（> 200 个文件）：至少 3 层，鼓励 4-5 层（章 → 节 → 子节 → 页面 → 子页面）
        - 任何情况最多 5 层，保持结构清晰可读

        当前仓库文件数：{{file_count}}
        {{recommended_page_count}}

        ## 章节规划建议（全面视图适用）

        以下为建议的顶层章节框架，请根据实际仓库情况调整：
        1. **项目概览** — 项目背景、核心功能、技术栈、快速入门
        2. **系统架构** — 整体架构设计、核心设计模式、数据流、组件交互
        3. **核心模块** — 按功能域拆分为主要模块及其子模块的详细说明
        4. **数据管理** — 数据模型、存储方案、数据库设计、缓存策略
        5. **API 与接口** — 对外接口、内部服务通信、事件/消息机制
        6. **部署与运维** — 环境配置、构建部署、监控告警、扩展方案
        7. **开发指南** — 本地开发环境搭建、编码规范、测试策略、贡献指南

        ## 输入数据

        **文件树**：
        ```
        {{file_tree}}
        ```

        **README**：
        {{readme}}

        **目标语言**：{{language}}

        ## 输出要求

        输出纯 JSON（不要用 ``` ```json ``` ``` 包裹），结构如下：
        - `id`: "wiki"
        - `title`: Wiki 标题（项目名称）
        - `description`: 简要描述（1-2 句）
        - `rootSections`: 根章节 ID 列表（顶层导航入口）
        - `sections`: 章节数组，每个包含：
          - `id`: 唯一标识（如 "section-1"）
          - `title`: 章节标题
          - `description`: 章节简要说明
          - `pages`: 该章节直接包含的页面 ID 列表
          - `subsections`: 子章节 ID 列表（支持深度嵌套）
        - `pages`: 页面数组，每个包含：
          - `id`: 唯一标识（如 "page-1"）
          - `title`: 页面标题
          - `description`: 页面简要说明
          - `navTitle`: 导航栏缩略标题（可选）
          - `pageType`: "overview" | "section" | "article" | "appendix"
          - `importance`: "high" | "medium" | "low"
          - `filePaths`: 相关源文件路径数组
          - `relatedPages`: 相关页面 ID 数组
          - `prerequisitePages`: 建议先读的前置页面 ID 数组
          - `parentId`: 父页面 ID（null 表示顶层；引用另一个页面 ID）

        **注意**：章节（section）通过 `subsections` 形成多级目录，页面通过 `parentId` 形成阅读顺序链。`parentId` 必须引用页面 ID，不可引用章节 ID。
        """;

    private const string WikiStructureJsonFormatTemplate = """
        ## 输出格式强制要求

        你必须返回**纯 JSON，不要用任何 Markdown 代码围栏包裹**。具体规则：

        1. 不要输出 ` ```json ` 或 ` ``` ` 围栏
        2. 不要输出任何解释性文字、前言或后记
        3. 直接输出 `{` 开头的 JSON 对象
        4. JSON 必须完整闭合——检查所有括号和花括号
        5. 所有字符串使用双引号，不可使用单引号
        6. `filePaths`、`relatedPages`、`prerequisitePages` 等数组字段若为空则输出 `[]`
        """;

    private const string OllamaJsonEnforcementTemplate = """
        重要提醒：你必须严格遵循格式要求，直接输出纯 JSON 对象。不要添加任何解释文字、不要包裹在 XML 标签中、不要使用 Markdown 围栏。输出必须以 `{"id":"wiki"` 开头。
        """;

    private const string WikiPageTemplate = """
        你是一位资深技术文档撰写专家和软件架构师。你的任务是为以下 Wiki 页面生成详细、准确、专业的技术文档。

        ## 页面信息

        - **页面标题**：{{page_title}}
        - **页面描述**：{{page_description}}
        - **页面类型**：技术文档
        - **目标语言**：{{language}}

        ## 参考源文件

        以下是为本页面分配的源文件内容，请基于这些文件撰写准确的文档，不要凭空编造：

        {{retrieved_code_snippets}}

        ## 相关页面上下文

        以下是与本页面相关的其他 Wiki 页面摘要，请与此内容保持整体一致，避免重复：

        {{related_pages}}

        {{prerequisite_pages}}

        ## 内容结构要求

        1. **源文件引用块**：页面开头必须放置 `<details><summary>📁 源文件参考</summary>` 折叠块，列出所有被本文档引用的源文件路径（至少 5 个）。该块之前不得有任何其他内容。
        2. **页面标题**：紧跟源文件引用块后放置 `# {{page_title}}` 一级标题。
        3. **概述**：1-2 段简介，概括本页要讲解的模块/功能/架构。
        4. **详细说明**：使用 H2/H3 标题划分小节，逐层深入。每个 H2 节应涵盖一个逻辑独立的话题。
        5. **Mermaid 图表**：在合适的位置插入 Mermaid 图表（架构图、流程图、时序图等），具体语法规则见下文。
        6. **表格**：参数说明、配置项、API 接口等使用 Markdown 表格呈现。
        7. **代码片段**：关键逻辑使用带语言标识的围栏代码块展示。

        ## Mermaid 图表规范

        ### 流程图（graph）
        - 仅使用 `graph TD`（自上而下），禁止使用 `graph LR`
        - 节点文字控制在 3-4 个单词以内，保持简洁
        - 使用 `classDef` 为不同层级的节点定义样式

        ### 时序图（sequenceDiagram）
        - 支持完整语法：`->>`（同步调用）、`-->>`（异步返回）、`->x`（失败）、`-)`（异步消息）、`--)`（异步返回）等
        - 使用 `activate`/`deactivate`（或 `+`/`-`）标记生命线激活
        - 使用 `box...end` 对参与者分组
        - 使用 `loop`、`alt`、`opt`、`par`、`critical`、`break` 表达控制结构
        - 使用 `Note over` / `Note right of` 添加注释
        - 使用 `autonumber` 自动编号

        ### 类图（classDiagram）
        - 展示核心类及其属性和方法
        - 标注继承关系（`<|--`）、组合关系（`*--`）、依赖关系（`<..`）

        ## 样式规范

        - 使用 `> **提示**：` 格式的引用块放置重要说明
        - 使用 `> **注意**：` 格式放置注意事项
        - 使用 `> **警告**：` 格式放置风险提示
        - 使用 `> **深入阅读**：` 格式添加扩展阅读链接
        - 表格上方必须有表格标题（用粗体文本标注）

        ## 技术准确性要求（重要）

        - **严格基于上述提供的源代码片段撰写文档。不得编造任何不存在的类名、方法名、API 名称或代码逻辑。**
        - 代码示例必须从上述提供的源代码片段中提取，不得自行编造示例代码
        - 如果提供的代码片段不足以完整描述某个方面，请明确注明"未在代码中找到对应实现"
        - 源文件引用格式：`Sources: [filename.ext:start_line-end_line]()`
        - **禁止**使用任何形式的"示例代码"、"示例实现"等虚构内容——所有代码引用必须来自真实源文件
        """;

    private const string WikiPageMarkdownRulesTemplate = """
        ## 严格的格式规则

        请遵守以下所有规则，违反任何一条都会导致生成结果被视为不合格：

        1. **禁止**用 ` ``` ``` ` ` ``` ` 围栏包裹整个回答。你的回答直接以 `<details>` 标签开头。
        2. **禁止**在回答中包含你的思考过程或推理过程。只输出最终文档内容。
        3. **禁止**在 Markdown 中写出 `\` 转义特殊字符（如 `[`、`]`、`{`、`}`）。直接书写即可。
        4. **禁止**在管道符 `|` 前面加转义反斜杠。表格中的 `|` 直接书写。
        5. **禁止**在代码块开头或结尾添加多余的空行。
        6. 内联代码使用**单反引号** `` ` `` 包裹，代码块使用**三反引号** ` ``` ` ` ``` ` 并标注语言。
        7. 所有 Mermaid 图表放在单独的 ` ```mermaid ` 代码块中，不要添加额外的 HTML 标签包裹。
        8. 列表项末尾保持一致的标点风格（中英文混排时中文使用句号，英文不加句号）。
        9. 不要包含不存在的 URL 或链接。所有外部引用仅限提供的源文件。

        请直接开始撰写，以 `<details>` 开头。
        """;

    private const string AskTemplate = """
        你是一位资深代码仓库分析专家。你将基于仓库的知识库内容回答用户的问题。

        ## 行为准则

        1. 如果知识库中有相关信息，请基于该信息给出准确、详细的回答。
        2. 如果知识库中信息不完整，请诚实说明并基于已知部分给出合理推测，同时标注推测部分。
        3. 如果知识库中完全没有相关信息，请直接告知用户，不要编造。
        4. 回答语言必须与用户提问语言一致。

        ## 仓库知识库

        {{repository_context}}

        ## 格式要求

        - 回答直接以正文开头，不要包含前言（如"好的，我来回答"）
        - 使用 Markdown 格式化：标题、列表、代码块、表格等
        - 涉及代码时提供代码片段并标注语言
        - 不要用 ` ``` ``` ` ` ``` ` 围栏包裹整个回答
        - 不要输出思考过程

        ## 用户问题（{{language}}）

        {{question}}
        """;

    private const string SlidesTemplate = """
        你是一位资深技术演讲设计师。你的任务是基于提供的 Wiki 知识库内容，创建一套结构清晰、视觉优雅的技术演示文稿。

        ## 输入材料

        {{wiki_content}}

        ## 演示文稿要求

        1. **结构**：总共 {{total_slides}} 张幻灯片（1 张标题幻灯片 + N 张内容幻灯片 + 1 张总结幻灯片）
        2. **每张幻灯片**输出一个独立的 HTML `<section>` 标签，包含以下属性：
           - 清晰的大标题
           - 3-5 个要点（Bullet Points）
           - 适合的代码片段或 Mermaid 图表
        3. **视觉风格**：
           - 使用现代简约风格，背景深色、文字浅色
           - 关键术语使用高亮色（如金色/黄色）
           - 代码片段使用带语法高亮的深色代码块
        4. **内容原则**：
           - 每张幻灯片聚焦单一主题
           - 要点简洁有力，每个要点不超过两行
           - 技术术语保持一致

        ## 语言

        内容语言：{{language}}

        ## 输出格式

        直接输出完整 HTML（无需 `<!DOCTYPE>` 和 `<html>` 标签），从 `<section>` 开始，以 `</section>` 结束。
        不要用 Markdown 围栏包裹输出。
        """;

    private const string WorkshopTemplate = """
        你是一位资深技术培训师。你的任务是基于提供的 Wiki 知识库内容，设计一个完整的开发者训练营。

        ## 输入材料

        {{wiki_content}}

        ## 训练营结构

        请按以下结构组织训练营内容：

        1. **学习目标**（3-5 个可衡量的目标）
        2. **预备知识**（学员开始前需要掌握的知识）
        3. **环境准备**（所需工具、依赖和安装步骤）
        4. **核心模块**（3-5 个动手练习模块），每个包含：
           - 概念讲解（简洁的理论背景）
           - 动手练习（具体的代码任务）
           - 预期结果（学员应该达到的效果）
           - 常见问题与解答
        5. **进阶挑战**（2-3 个可选的高阶练习）
        6. **总结与资源**（关键要点回顾 + 推荐阅读链接）

        ## 格式要求

        - 使用 Markdown 格式
        - 代码示例必须完整可运行
        - 每个练习步骤清晰编号
        - 使用 `> **提示**：` 格式给出提示

        ## 语言

        内容语言：{{language}}

        直接输出训练营文档（Markdown 格式），不要包含前言。
        """;

    private const string ChatSystemTemplate = """
        你是一位乐于助人的代码分析助手。你会根据仓库知识库回答用户关于代码的问题。用与用户提问相同的语言回答。
        回答时保持专业、准确，基于提供给你的知识库内容。如果知识库中没有相关信息，诚实告知用户，不要编造。
        直接以回答内容开头，不要写"好的，我来帮你..."之类的前言。
        """;

    private const string OllamaSystemRoleTemplate = """
        你是一位资深软件架构师和技术文档专家。你擅长分析代码仓库并生成准确、结构化的技术文档。
        请严格遵循用户提示中的输出格式要求——用户提示中指定的格式（JSON、Markdown、HTML 等）即为本次任务的唯一输出格式。
        不要在任何输出之前添加思考过程或引导语，直接输出所要求的内容。
        """;
}
