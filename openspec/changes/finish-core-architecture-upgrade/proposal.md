## Why

前序迭代（Tree-sitter AST 迁移、提示词数据库化、MEAI Role-Based 消息升级）在 Chat/Ask 路径上完整落地，但 **Wiki 生成管线（核心路径）、Slides/Workshop 派生任务** 仍停留在旧架构——AST 分析被正则替代、提示词硬编码在代码中、LLM 消息扁平化为单字符串。审计确认 12 处严重不符均源自这三项架构迁移的半途而废。本次变更严格完成所有既定目标，消除 spec 与代码之间的全部严重差异。

## What Changes

- **BREAKING**: 删除 `TaskPromptService` 中所有硬编码提示词（~500 行），全部迁移至数据库 `prompt_templates` 表
- **BREAKING**: 删除 `CallGraphBuilder` 中所有正则调用提取逻辑，改为基于 Tree-sitter AST 的调用图构建
- **BREAKING**: 删除 `DesignPatternDetector` 中所有类名正则匹配逻辑，改为基于 AST 节点关系检测
- `WikiTaskService`、`SlidesTaskService`、`WorkshopTaskService` 改为通过 `IPromptMergeService` 获取提示词
- `WikiTaskService`、`SlidesTaskService`、`WorkshopTaskService` 改为使用结构化 `List<ChatMessage>` 消息列表调用 LLM
- `CodeUnderstandingService` 改为使用结构化消息
- `TaskPromptService` 精简为纯管线协调层（不再包含提示词文本）
- `PromptSeedData` 扩展，覆盖所有管线阶段提示词模板
- 删除死代码 `PromptTemplateService`

## Capabilities

### Modified Capabilities
- `code-analysis`: 调用图构建从正则改为 Tree-sitter AST 方法级调用关系提取；设计模式检测从类名正则匹配改为 AST 节点关系识别
- `prompt-system`: TaskPromptService 全部提示词迁移至数据库，所有服务统一通过 IPromptMergeService 获取；删除 CodeSummaryService 等已废弃引用
- `wiki-generation-pipeline`: 所有 LLM 调用改为结构化 ChatMessage 列表（System/User 角色分离），替代单字符串扁平化
- `structure-planning`: 结构规划提示词改为从 DB 加载，通过 IPromptMergeService 拼装
- `slides-workshop`: Slides 和 Workshop 管道改为 DB 驱动提示词 + 结构化消息，与 Wiki 管线对齐
- `llm-tools`: QueryCallGraph、RetrieveClassDefinition 等工具的后端数据源从正则结果切换为 AST 结果

## Impact

- **后端**: `TaskPromptService`（重写）、`CallGraphBuilder`（重写）、`DesignPatternDetector`（重写）、`WikiTaskService`（修改 LLM 调用路径）、`SlidesTaskService`（修改）、`WorkshopTaskService`（修改）、`CodeUnderstandingService`（修改）、`PromptSeedData`（扩展）、`TreeSitterAnalyzer`（扩展调用图提取能力）
- **数据库**: `prompt_templates` 表新增/更新种子数据
- **Spec**: `code-analysis`、`prompt-system`、`wiki-generation-pipeline`、`structure-planning`、`slides-workshop`、`llm-tools` 六个 spec 需更新
- **无前端影响**: 所有变更限于后端管线和提示词系统
