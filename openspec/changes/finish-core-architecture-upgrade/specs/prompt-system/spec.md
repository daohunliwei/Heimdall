## MODIFIED Requirements

### Requirement: 提示词数据库存储与种子数据
所有管线提示词 SHALL 存储于 `prompt_templates` 数据库表中。Wiki（结构规划、页面生成、质量审查）、Slides、Workshop、Chat、Ask 等全部类别的提示词 SHALL 通过 `PromptSeedData` 写入数据库。`TaskPromptService` SHALL 不再包含任何提示词模板文本——仅作为管线协调层，通过 `IPromptMergeService` 获取 DB 模板后进行变量替换和 AST 上下文拼装。

#### Scenario: 系统启动时种子数据初始化
- **WHEN** 应用启动且数据库缺少系统模板
- **THEN** `PromptSeedData` 将全部类别模板写入 `prompt_templates`，SubCategory="system" 的片段独立存储为 System 角色消息模板

#### Scenario: 结构规划提示词从 DB 加载并注入 AST 数据
- **WHEN** Wiki 管线需要结构规划提示词
- **THEN** `TaskPromptService.BuildWikiStructurePromptAsync` 通过 `IPromptMergeService` 获取 DB 模板 → 变量替换 → 注入 AST L1 层数据（类型层级、调用拓扑、设计模式证据）→ 返回 `(systemPrompt, userPrompt)` 元组

### Requirement: Provider 层与管线接入提示词管理
所有管线服务（WikiTaskService、SlidesTaskService、WorkshopTaskService、ChatController、AskTaskService）SHALL 通过 `IPromptMergeService` 或 `TaskPromptService`（作为协调层）获取提示词。

#### Scenario: Wiki 页面生成获取 DB 提示词
- **WHEN** `WikiTaskService` 执行页面生成阶段
- **THEN** `TaskPromptService.BuildWikiPagePromptAsync` 通过 `IPromptMergeService` 从 DB 获取模板 → 注入 AST L2 层上下文 → 返回结构化消息

### Requirement: 提示词管理 API 与缓存
系统 SHALL 为 `IPromptMergeService` 添加 `IMemoryCache` 缓存（10 分钟 TTL），管理员修改提示词后缓存自动失效。

## REMOVED Requirements

### Requirement: TaskPromptService 硬编码提示词
**Reason**: TaskPromptService 中 8 个方法 ~500 行硬编码提示词模板，与 DB 驱动提示词系统并行运行。迁移至 DB 后 TaskPromptService 仅保留管线协调逻辑。
**Migration**: 删除所有 `$"""` / `"""` 模板文本。`PromptTemplateService`（死代码，~112 行）一并删除。
