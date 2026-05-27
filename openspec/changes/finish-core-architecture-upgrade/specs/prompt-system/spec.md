## MODIFIED Requirements

### Requirement: 提示词数据库存储与种子数据
所有系统提示词 SHALL 存储于 `prompt_templates` 数据库表中。Wiki 管线（结构规划、页面生成）、Slides、Workshop、Chat、Ask、质量审查等全部类别的提示词 SHALL 通过 `PromptSeedData` 写入数据库。`TaskPromptService` SHALL 不再包含任何提示词模板文本——仅作为管线协调层，通过 `IPromptMergeService` 获取 DB 中的模板后进行变量替换。

#### Scenario: 系统启动时种子数据初始化
- **WHEN** 应用启动且数据库缺少系统模板
- **THEN** `PromptSeedData` 将 wiki_structure、wiki_page、slides、workshop、chat、ask、quality_review 等全部类别模板写入 `prompt_templates`，标记 `IsSystem=true`

#### Scenario: 运行时通过 Category 查询模板
- **WHEN** Wiki 管线需要结构规划提示词
- **THEN** `TaskPromptService` 调用 `IPromptMergeService.BuildPrompt("wiki_structure", provider, "json", variables)` 获取 DB 模板并完成变量替换

### Requirement: Provider 层与 Controller 层接入提示词管理
所有 ChatProvider SHALL 从 ChatMessage 流中读取 system prompt。`ChatController.StreamChat` 和 `WikiTaskService`、`SlidesTaskService`、`WorkshopTaskService` SHALL 通过 `IPromptMergeService` 或 `TaskPromptService`（作为协调层）获取 system prompt。

#### Scenario: Wiki 页面生成获取提示词
- **WHEN** `WikiTaskService` 执行页面生成阶段
- **THEN** `TaskPromptService.BuildWikiPagePromptAsync` 通过 `IPromptMergeService` 从 DB 获取提示词模板并完成变量替换

#### Scenario: Slides 生成获取提示词
- **WHEN** `SlidesTaskService` 执行 Slides 生成
- **THEN** `TaskPromptService` 通过 `IPromptMergeService` 从 DB 获取 slides 类提示词模板

### Requirement: 提示词管理 API
系统 SHALL 提供统一的提示词管理 API（`/api/admin/prompt-templates`），支持 CRUD、版本历史、回滚、仓库级覆盖管理。管理员修改后 SHALL 实时生效无需重启。

#### Scenario: 管理员编辑提示词实时生效
- **WHEN** 管理员在管理界面修改提示词模板
- **THEN** 下次对应阶段使用修改后的提示词，无需重启服务
- **AND** 提示词缓存（`IMemoryCache`，10 分钟 TTL）自动失效

## REMOVED Requirements

### Requirement: TaskPromptService 硬编码提示词
**Reason**: `TaskPromptService` 中 8 个方法包含约 500 行硬编码提示词模板，与 DB 驱动提示词系统并行运行。所有提示词内容已迁移至 `PromptSeedData` 数据库种子数据。
**Migration**: 删除 `TaskPromptService` 中所有提示词字符串常量和方法体中的 `$"""` / `"""` 模板文本。`TaskPromptService` 改为调用 `IPromptMergeService` 获取模板。`PromptTemplateService`（死代码，~112 行）一并删除。
