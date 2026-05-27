## Purpose

统一提示词系统——涵盖提示词的五层结构化架构、数据库存储与动态拼装、Provider 层集成、模型感知变体、管理 API 及 SQL 初始化脚本。
## Requirements
### Requirement: 提示词五层结构化架构
所有预设提示词 SHALL 采用"角色定义 → 上下文注入 → 分步指令 → 输出约束 → 质量自查清单"五层结构。每层 SHALL 以 Markdown 标题明确分隔，LLM 可清晰识别各层边界。

#### Scenario: 结构规划提示词含五层结构
- **WHEN** 系统构建结构规划提示词
- **THEN** 提示词包含：`## 角色`（你是有 X 年经验的软件架构师）、`## 上下文`（仓库分析数据）、`## 分步指令`（1.分析架构 2.设计层级 3.规划页面 4.分配文件 5.建立关联）、`## 输出约束`（JSON Schema）、`## 质量自查清单`（5-8 条自检项）

#### Scenario: 页面生成提示词含五层结构
- **WHEN** 系统构建页面生成提示词
- **THEN** 提示词包含：`## 角色`（技术文档专家）、`## 上下文`（父页面摘要、代码片段、仓库元数据）、`## 分步指令`（1.理解主题 2.分析代码 3.构建大纲 4.撰写内容 5.插入图表）、`## 输出约束`（Markdown 格式、代码引用规范、图表语法）、`## 质量自查清单`（验证代码引用真实性等）

### Requirement: 提示词数据库存储与种子数据
所有系统提示词 SHALL 存储于 `prompt_templates` 数据库表中，包含 `Category`（任务类别）、`SubCategory`（子类别：base/format/provider_system）、`Priority`（拼接顺序）、`ApplicableProviders`（适用 Provider 列表）字段。Provider 和 Controller 层 SHALL 通过 `IPromptMergeService` 获取提示词，不再硬编码。`TaskPromptService` 中的管线提示词模板（结构规划、页面生成等）当前仍通过 `PromptSeedData` 从数据库加载，后续逐步迁移。

#### Scenario: 管理员创建新提示词模板
- **WHEN** 管理员通过 `POST /api/admin/prompt-templates` 提交包含 Category、SubCategory、TemplateContent 的请求
- **THEN** 系统创建新的 PromptTemplate 记录，Version=1，并写入 PromptTemplateHistory

#### Scenario: 系统启动时种子数据初始化
- **WHEN** 应用启动且 `prompt_templates` 表为空或缺少系统模板
- **THEN** 系统通过 `PromptSeedData` 将预设提示词模板（wiki_structure、wiki_page、chat、ask、slides、workshop、quality_review 等）写入数据库，标记 `IsSystem=true`

#### Scenario: 运行时通过 Category 查询模板
- **WHEN** 调用 `IPromptTemplateRepository.GetByCategoryAsync("wiki_page")`
- **THEN** 系统返回所有 Category 为 "wiki_page" 的模板，按 Priority 升序排列

### Requirement: 提示词片段合并引擎
系统 SHALL 提供 `IPromptMergeService`，根据 `[Category] + [Provider] + [OutputFormat]` 动态拼装最终提示词，合并逻辑为：Base 模板 → Format 指令 → Provider 个性片段（按 ApplicableProviders 过滤），按 Priority 排序拼接，最后执行变量插值 `{{variable}}`。

#### Scenario: Wiki 页面生成任务的提示词拼装
- **WHEN** 调用 `PromptMergeService.BuildPrompt("wiki_page", "ollama", "json", variables)`
- **THEN** 系统返回包含 Base 模板 + JSON 格式指令 + Ollama Provider 个性片段的合并后的完整提示词字符串，其中 `{{page_title}}` 等占位符已被替换为实际值
- **AND** Ollama 个性片段的 `ApplicableProviders` 包含 "ollama"，其他 Provider 的片段被排除

#### Scenario: Provider 不支持 system role 时的降级处理
- **WHEN** Provider 不支持独立的 system role（如 Gemini）
- **THEN** system prompt 片段自动合并到 user prompt 正文前方

### Requirement: Provider 层与 ChatController 接入提示词管理
所有 ChatProvider SHALL 从 `ChatResponse` 消息流中读取 system prompt，不硬编码角色指令。`ChatController.StreamChat` SHALL 通过 `IPromptMergeService.BuildChatPromptAsync` 获取 system prompt。

#### Scenario: SSE 聊天请求获取 system prompt
- **WHEN** 用户发送聊天请求到 `POST /chat/completions/stream`
- **THEN** 系统通过 `IPromptMergeService.BuildChatPromptAsync` 获取合并后的提示词

#### Scenario: System prompt 为空时
- **WHEN** 数据库中没有匹配的 chat 类提示词模板
- **THEN** Provider 仅发送 `role: "user"` 的 Prompt 内容，不附加 system 级别消息

### Requirement: 提示词管理 API
系统 SHALL 提供统一的提示词管理 API（`/api/admin/prompt-templates`），支持 CRUD、版本历史、回滚、仓库级覆盖管理。

#### Scenario: 查看模板版本历史
- **WHEN** 管理员请求 `GET /api/admin/prompt-templates/{id}/history`
- **THEN** 系统返回该模板的所有历史版本，按 Version 降序排列

#### Scenario: 回滚到指定版本
- **WHEN** 管理员请求 `POST /api/admin/prompt-templates/{id}/rollback` 并指定 `targetVersion`
- **THEN** 系统从 PromptTemplateHistory 恢复指定版本的内容，Version 递增

#### Scenario: 设置仓库级覆盖
- **WHEN** 管理员请求 `POST /api/admin/prompt-templates/{id}/overrides` 包含 RepositoryId、OverrideContent、Strategy
- **THEN** 系统创建或更新 RepositoryPromptOverride 记录，Strategy 为 "override"（完全覆盖）、"merge"（合并变量）、或 "append"（尾部追加）

#### Scenario: 管理员编辑提示词实时生效
- **WHEN** 管理员在管理界面修改提示词模板
- **THEN** 下次对应阶段使用修改后的提示词，无需重启服务
- **AND** 数据库无记录时回退到代码中的默认模板

### Requirement: 页面生成提示词使用真实代码
Wiki 页面生成的提示词 SHALL 包含从当前检索链路获取的真实代码片段，而非代码摘要。提示词 SHALL 要求 LLM 基于提供的源代码撰写技术文档。

#### Scenario: 提示词注入代码片段
- **WHEN** 系统构建页面生成提示词
- **THEN** 提示词中包含格式化后的真实源代码块，附带文件路径和行号

#### Scenario: 提示词禁止虚构
- **WHEN** 页面生成提示词构建完成
- **THEN** 提示词中包含指令"严格基于上述源代码撰写文档。不得编造不存在的类、方法或 API"

### Requirement: 模板变量替换
`wiki-structure-planning` 模板 SHALL 使用 `{{repo_structure}}`（目录树+入口点+技术栈）替代 `{{code_summary}}`。`wiki-page-generation` 模板 SHALL 使用 `{{retrieved_code_snippets}}`（真实代码片段）替代 `{{file_summaries}}`。旧 `code-summary-*` 模板及其播种逻辑 SHALL 直接删除。

#### Scenario: 结构规划模板使用新变量
- **WHEN** 使用结构规划模板
- **THEN** 模板变量使用 `{{repo_structure}}`，内容为仓库目录树和入口文件列表

#### Scenario: 页面生成模板使用新变量
- **WHEN** 使用页面生成模板
- **THEN** 模板变量使用 `{{retrieved_code_snippets}}`，内容为当前检索链路返回的真实代码片段

#### Scenario: 旧模板已删除
- **WHEN** 系统启动并播种提示词模板
- **THEN** code-summary-* 模板不在数据库中出现

### Requirement: 模型感知的提示词变体
系统 SHALL 根据使用的模型能力自动调整提示词。对于能力较弱的模型（如 7-14B 参数），提示词 SHALL 包含更严格的约束和更少的要求项。

#### Scenario: 小模型提示词调整
- **WHEN** 用户配置 7B 参数模型作为页面生成模型
- **THEN** 提示词中增加"每次只分析一个函数"、"不要输出超过 500 字"等约束，并减少同时要求的任务数量

#### Scenario: 强模型提示词
- **WHEN** 用户配置 Claude Sonnet 或 GPT-4o 级别模型
- **THEN** 提示词包含完整的代码分析要求（函数调用链、设计模式识别、性能考量）

### Requirement: 按内容深度级别差异化提示词
系统 SHALL 根据页面的 ContentDepthLevel 提供差异化的提示词指令。overview 页面侧重架构全景和模块关系；section 页面侧重模块边界和数据流分析；article 页面侧重实现细节和代码深挖。

#### Scenario: Overview 页面获得架构侧重指令
- **WHEN** 生成 ContentDepthLevel=overview 的页面
- **THEN** 提示词分步指令强调：不要深入代码实现、聚焦模块间关系、必须包含 Mermaid 架构图、页面间导航引用

#### Scenario: Article 页面获得代码深挖指令
- **WHEN** 生成 ContentDepthLevel=article 的页面
- **THEN** 提示词分步指令强调：必须以真实代码片段为核心、逐方法分析关键逻辑、使用表格对比参数/配置、必须包含 Mermaid 时序图

### Requirement: 代码理解结果注入结构规划
结构规划提示词 SHALL 将深度代码理解结果（CodeUnderstandingResult）作为上下文注入段的核心输入。注入内容 SHALL 包含：架构模式识别结果、模块依赖拓扑摘要、识别到的设计模式列表、关键数据流路径描述。

#### Scenario: 架构模式注入
- **WHEN** CodeUnderstandingResult 识别到"分层架构"模式
- **THEN** 结构规划提示词上下文段包含："该系统采用分层架构（Controller → Service → Repository），请据此设计 Wiki 层级结构"

#### Scenario: 设计模式注入
- **WHEN** CodeUnderstandingResult 包含多个设计模式
- **THEN** 提示词上下文段列出所有模式及其参与类，要求为每个模式创建独立 article 页面或归入相关 section

### Requirement: 输出格式与质量硬约束
所有提示词 SHALL 包含明确的输出格式约束和质量自查清单。约束 SHALL 包括：禁止虚构代码、禁止空泛描述、禁止裸露元数据、Mermaid 图必须用 ` ```mermaid ` 包裹、代码必须用 ` ```语言标识 ` 包裹。

#### Scenario: 代码真实性约束
- **WHEN** LLM 生成页面内容
- **THEN** 提示词明确："代码引用必须来自下方提供的代码片段，禁止编造类名、方法名或 API"

#### Scenario: Mermaid 图表强制包裹
- **WHEN** LLM 需要在页面中插入图表
- **THEN** 图表必须放在独立的 ` ```mermaid ` 代码围栏中，语法节点文字 ≤ 4 个词

#### Scenario: 代码块强制语言标记
- **WHEN** LLM 需要在页面中引用源代码片段
- **THEN** 代码必须放在带语言标识的围栏代码块中，代码块前后必须有空行

#### Scenario: 禁止裸露元数据
- **WHEN** LLM 生成页面正文
- **THEN** JSON 元数据字段不得出现在正文 Markdown 中，正文以 `<details><summary>源文件参考</summary>` 开头

#### Scenario: 质量自查清单执行
- **WHEN** LLM 完成页面生成
- **THEN** 提示词最后一段为 8 项自查清单：代码引用真实性、Mermaid 包裹、语言标记、折叠块开头、图表和表格、节点文字长度、裸文本检查、标题内容

### Requirement: 全新中文提示词内容
系统提示词 SHALL 基于 deepwiki-open 原始英文提示词重写为中文版本，保持专业性的同时提升兼容性。

#### Scenario: 结构规划提示词语言和格式
- **WHEN** 系统加载 Category 为 `wiki_structure` 的提示词模板
- **THEN** 模板内容为中文，角色设定为"你是一位资深技术文档专家和软件架构师"
- **AND** 输出格式要求为 JSON（与 `WikiStructureDto` 对齐）

#### Scenario: 页面生成提示词包含样式规范
- **WHEN** 系统加载 Category 为 `wiki_page` 的提示词模板
- **THEN** 模板包含：`<details>` 源文件引用、Mermaid 图表语法、表格规范、禁止事项

### Requirement: SQL 初始化脚本双重保障
所有系统提示词 SHALL 同时以纯 SQL 脚本形式存储在 `backend/Heimdall.Repository/Data/SeedScripts/`，数据库清空后可直接执行恢复，不依赖应用启动。

#### Scenario: 使用 SQL 脚本恢复提示词
- **WHEN** 数据库 `prompt_templates` 表被清空
- **THEN** 执行 SQL 脚本将所有系统提示词恢复至数据库
- **AND** 每条 INSERT 使用 `ON CONFLICT (slug) DO NOTHING` 避免重复插入

#### Scenario: 应用启动与 SQL 脚本同步
- **WHEN** 应用每次启动
- **THEN** `PromptSeedData.SeedAsync()` 执行与 SQL 脚本内容一致的 Upsert 逻辑
