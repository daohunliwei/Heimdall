## MODIFIED Requirements

### Requirement: Slides 演示文稿生成（DB 驱动提示词 + 结构化消息）
系统 SHALL 通过 `SlidesTaskService` 基于仓库代码分析结果生成演示文稿。提示词 SHALL 从 DB 通过 `IPromptMergeService` 加载（Category="slides"）。LLM 调用 SHALL 使用结构化 `List<ChatMessage>` 消息列表（System/User 角色分离）。

#### Scenario: 创建 Slides 任务
- **WHEN** 用户通过 `POST /api/tasks/slides` 提交请求
- **THEN** 系统创建 Slides 任务，管线通过 `TaskPromptService`（协调层）→ `IPromptMergeService` 获取 DB 提示词
- **AND** LLM 调用使用结构化消息：System 消息=角色+格式约束，User 消息=代码上下文

### Requirement: Workshop 训练营材料生成（DB 驱动提示词 + 结构化消息）
系统 SHALL 通过 `WorkshopTaskService` 基于仓库代码分析结果生成训练营材料。提示词 SHALL 从 DB 通过 `IPromptMergeService` 加载（Category="workshop"）。LLM 调用 SHALL 使用结构化 `List<ChatMessage>` 消息列表。

#### Scenario: 创建 Workshop 任务
- **WHEN** 用户通过 `POST /api/tasks/workshop` 提交请求
- **THEN** 系统创建 Workshop 任务，管线通过 `IPromptMergeService` 获取 DB 提示词
- **AND** LLM 调用使用结构化消息

### Requirement: 版本化知识库服务
系统 SHALL 通过 `VersionedKnowledgeService` 为 Ask、Slides、Workshop 三种派生任务提供统一的版本锚点、页面和工件解析。Slides 和 Workshop 的上下文构建 SHALL 使用结构化消息，知识库内容作为独立 `ChatRole.User` 消息追加，不与系统指令混合。
