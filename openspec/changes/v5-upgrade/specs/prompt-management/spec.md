## ADDED Requirements

### Requirement: 提示词数据库存储

所有系统提示词 SHALL 存储于 `prompt_templates` 数据库表中，包含 `Category`（任务类别）、`SubCategory`（子类别：base/format/provider_system）、`Priority`（拼接顺序）、`ApplicableProviders`（适用 Provider 列表）字段。不再允许在 Provider、Controller、Service 中硬编码提示词文本。

#### Scenario: 管理员创建新提示词模板
- **WHEN** 管理员通过 `POST /api/admin/prompt-templates` 提交包含 Category、SubCategory、TemplateContent 的请求
- **THEN** 系统创建新的 PromptTemplate 记录，Version=1，并写入 PromptTemplateHistory

#### Scenario: 系统启动时种子数据初始化
- **WHEN** 应用启动且 `prompt_templates` 表为空或缺少系统模板
- **THEN** 系统通过 `PromptSeedData` 将 TaskPromptService、ChatController、CodeSummaryService 中的完整提示词内容迁移入库，标记 `IsSystem=true`

#### Scenario: 运行时通过 Category 查询模板
- **WHEN** 调用 `IPromptTemplateRepository.GetByCategoryAsync("wiki_page")`
- **THEN** 系统返回所有 Category 为 "wiki_page" 的模板，按 Priority 升序排列

### Requirement: 提示词片段合并引擎

系统 SHALL 提供 `IPromptMergeService`，根据 `[Category] + [Provider] + [OutputFormat]` 动态拼装最终提示词，合并逻辑为：Base 模板 → Format 指令 → Provider 个性片段（按 ApplicableProviders 过滤），按 Priority 排序拼接，最后执行变量插值 `{{variable}}`。

#### Scenario: Wiki 页面生成任务的提示词拼装
- **WHEN** 调用 `PromptMergeService.BuildPrompt("wiki_page", "ollama", "json", variables)`
- **THEN** 系统返回包含 Base 模板 + JSON 格式指令 + Ollama Provider 个性片段的合并后的完整提示词字符串，其中 `{{page_title}}` 等占位符已被替换为实际值
- **AND** Ollama 个性片段的 `ApplicableProviders` 包含 "ollama"，其他 Provider（如 "openai"）的片段被排除

#### Scenario: Provider 不支持 system role 时的降级处理
- **WHEN** Provider 声明 `SupportsSystemRole = false`（如 Gemini）
- **THEN** system prompt 片段自动合并到 user prompt 正文前方，而非作为独立的 `role: "system"` 发送

### Requirement: Provider 层移除硬编码提示词

所有 ChatProvider（OllamaChatProvider、OpenAiCompatibleChatProvider、AzureChatProvider、GoogleChatProvider、MiniMaxChatProvider、BedrockChatProvider）SHALL 移除任何硬编码的 system prompt 或角色指令，改为从 `ChatRequest.SystemPrompt` 字段读取。

#### Scenario: OllamaChatProvider 发送带 system prompt 的请求
- **WHEN** `ChatRequest.SystemPrompt` 非空
- **THEN** OllamaChatProvider 将 `SystemPrompt` 作为 `role: "system"` 消息发送，后跟 `role: "user"` 的 `Prompt` 内容
- **AND** 不再包含 "You must respond with valid, well-formed XML only" 的硬编码指令

#### Scenario: ChatRequest.SystemPrompt 为空时
- **WHEN** `ChatRequest.SystemPrompt` 为 null 或空字符串
- **THEN** 所有 Provider 仅发送 `role: "user"` 的 `Prompt` 内容，不附加任何 system 级别消息

### Requirement: ChatController 提示词接入管理

`ChatController.StreamChat` 端点 SHALL 通过 `IPromptMergeService` 获取 system prompt，而非硬编码拼接。

#### Scenario: SSE 聊天请求获取 system prompt
- **WHEN** 用户发送聊天请求到 `POST /api/chat/stream`
- **THEN** 系统通过 `PromptMergeService.BuildPrompt("chat", provider, "text", variables)` 获取合并后的提示词
- **AND** 返回的 `ChatRequest.SystemPrompt` 包含角色设定文本，`Prompt` 包含用户原始问题

### Requirement: CodeSummaryService 提示词接入管理

`CodeSummaryService` 的三个硬编码方法（`SummarizeFileAsync`、`GenerateModuleSummaryAsync`、`BuildSystemSummaryPrompt`）SHALL 改为通过 `IPromptMergeService` 获取提示词模板，再进行变量替换。

#### Scenario: 文件摘要提示词获取
- **WHEN** `CodeSummaryService.SummarizeFileAsync` 被调用
- **THEN** 系统通过 `PromptMergeService.BuildPrompt("code_summary_file", provider, "text", new { filePath, language, content })` 获取完整提示词
- **AND** 返回的提示词包含角色设定和任务指令，变量已替换为实际的文件路径和代码内容

### Requirement: 提示词管理 API 统一

系统 SHALL 提供统一的提示词管理 API（`/api/admin/prompt-templates`），支持 CRUD、版本历史、回滚、仓库级覆盖管理。废弃旧的 `admin/prompts` 控制器。

#### Scenario: 查看模板版本历史
- **WHEN** 管理员请求 `GET /api/admin/prompt-templates/{id}/history`
- **THEN** 系统返回该模板的所有历史版本，按 Version 降序排列

#### Scenario: 回滚到指定版本
- **WHEN** 管理员请求 `POST /api/admin/prompt-templates/{id}/rollback` 并指定 `targetVersion`
- **THEN** 系统从 PromptTemplateHistory 恢复指定版本的内容，Version 递增，写入新的历史记录

#### Scenario: 设置仓库级覆盖
- **WHEN** 管理员请求 `POST /api/admin/prompt-templates/{id}/overrides` 包含 RepositoryId、OverrideContent、Strategy
- **THEN** 系统创建或更新 RepositoryPromptOverride 记录，Strategy 为 "override"（完全覆盖）、"merge"（合并变量）、或 "append"（尾部追加）
