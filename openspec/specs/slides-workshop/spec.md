## Purpose

派生任务类型——涵盖 Slides（演示文稿）和 Workshop（训练营材料）的生成管线，以及支撑三种派生任务（Ask/Slides/Workshop）的版本化知识库服务。
## Requirements
### Requirement: Slides 演示文稿生成
系统 SHALL 通过 `SlidesTaskService` 基于仓库代码分析结果生成演示文稿（PPTX 格式）。生成管线 SHALL 包含：内容规划 → 逐页 LLM 生成 → 质量审查 → PPTX 渲染。

#### Scenario: 创建 Slides 任务
- **WHEN** 用户通过 `POST /api/tasks/slides` 提交包含 RepositoryId、WikiVersionId、Provider、Model 的请求
- **THEN** 系统创建 Slides 任务并入队执行

#### Scenario: Slides 页面展示
- **WHEN** 用户访问 `/repositories/{id}/slides` 页面
- **THEN** 前端展示 Slides 生成结果，包含模型选择器（UserSelector 组件）

### Requirement: Workshop 训练营材料生成
系统 SHALL 通过 `WorkshopTaskService` 基于仓库代码分析结果生成训练营材料。生成管线 SHALL 包含：内容规划 → 逐模块 LLM 生成 → 质量审查 → 结构化输出。

#### Scenario: 创建 Workshop 任务
- **WHEN** 用户通过 `POST /api/tasks/workshop` 提交包含 RepositoryId、WikiVersionId、Provider、Model 的请求
- **THEN** 系统创建 Workshop 任务并入队执行

#### Scenario: Workshop 页面展示
- **WHEN** 用户访问 `/repositories/{id}/workshop` 页面
- **THEN** 前端展示 Workshop 生成结果，包含模型选择器

### Requirement: 版本化知识库服务
系统 SHALL 通过 `VersionedKnowledgeService` 为 Ask、Slides、Workshop 三种派生任务提供统一的版本锚点、页面和工件解析。该服务 SHALL 根据 RepositoryVersionId 和 WikiVersionId 加载对应的知识库上下文。

#### Scenario: 派生任务获取版本上下文
- **WHEN** Ask、Slides 或 Workshop 任务执行
- **THEN** `VersionedKnowledgeService` 根据当前版本锚点加载关联的 Wiki 页面和工件作为上下文

#### Scenario: 版本切换影响派生任务
- **WHEN** 用户切换到历史 WikiVersion
- **THEN** 后续的 Ask、Slides、Workshop 任务基于该历史版本的知识库执行
