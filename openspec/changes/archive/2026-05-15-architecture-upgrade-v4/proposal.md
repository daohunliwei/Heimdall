## Why

V3 成功建立了版本化底座、统一任务执行与四段式生成管道，但三个关键问题仍严重制约 Heimdall 向"50 页以上复杂 Wiki"的长期目标演进：

1. **前端与后端严重脱节**：前端 UI 大量报错、无法正确展示数据、样式粗糙，用户体验举步维艰
2. **提示词管理缺乏系统化**：提示词硬编码在服务中，无全局管理、无仓库级覆写、无版本追踪，调优极其困难
3. **代码分析深度不足**：当前仅靠 file tree + README 做结构规划，远不如 Claude Code/Codex 等工具的深度代码理解能力，导致生成的 Wiki 内容浅薄

## What Changes

### 前端稳定化与 UX 重构
- 全面修复前端与后端 API 契约不一致导致的报错
- 重构仓库详情页布局、Wiki 浏览体验、侧边栏导航
- 统一加载态、错误态、空态的交互模式
- 优化 Ask/Slides/Workshop 页面的版本上下文传递

### 提示词管理系统
- 新增 `PromptTemplate` 实体与管理 API，支持全局模板 CRUD
- 支持仓库级提示词覆写/合并策略（override / merge / append）
- 提示词版本化，支持 A/B 测试与回滚
- 管理后台提示词编辑界面

### 深度代码分析引擎
- **BREAKING**: Wiki 结构规划不再仅依赖 file tree，引入多轮代码分析管道
- 新增代码索引阶段：依赖关系提取、符号表构建、模块边界识别
- 引入分层摘要策略：文件级 → 模块级 → 系统级渐进式理解
- 支持大仓库分批分析与增量索引
- 基于代码语义图驱动 Wiki 结构规划，生成更深度、更有层次的内容

### 生成编排增强
- 支持条件化页面生成（根据代码复杂度动态调整页面数量与深度）
- 引入页面间上下文传递机制（前置页面摘要注入后续页面生成）
- 支持生成后自动质量评估与弱页面重生成

## Capabilities

### New Capabilities
- `frontend-stabilization`: 前端 API 契约修复、错误处理统一、布局与 UX 全面重构
- `prompt-management`: 提示词模板管理系统——全局 CRUD、仓库级覆写、版本化、A/B 测试
- `deep-code-analysis`: 深度代码分析引擎——依赖图提取、符号索引、分层摘要、语义驱动规划
- `generation-orchestration-v2`: 增强生成编排——条件化页面生成、跨页面上下文传递、自动质量评估与重生成

### Modified Capabilities
<!-- 无需修改已有 spec -->

## Impact

**后端**：
- `Heimdall.Core/Entities/` 新增 `PromptTemplate`、`PromptOverride`、`CodeIndex` 等实体
- `Heimdall.Core/Services/` 新增 `PromptManagementService`、`CodeAnalysisService`、`CodeIndexService`
- `Heimdall.Api/Controllers/` 新增 `PromptTemplatesController`
- `TaskPromptService` 重构为消费 `PromptManagementService` 的组合模板
- `WikiTaskService` 的结构规划阶段重构为多轮分析管道

**前端**：
- `frontend/src/app/repositories/[repositoryId]/page.tsx` 大幅重构
- 新增/重写多个组件的错误处理与加载态
- 管理后台新增提示词管理页面

**数据库**：
- 新增 `prompt_templates`、`prompt_overrides`、`code_index_entries` 表
- 需要 EF Core 增量迁移

**外部依赖**：
- 无新增外部服务依赖，仍复用现有 LLM Provider 体系
