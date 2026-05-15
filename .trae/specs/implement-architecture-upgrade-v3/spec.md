# Heimdall 架构升级 V3 实施 Spec

## Why

`architecture-upgrade-planV3.md` 已经完成了方向与决策设计，但当前仓库尚未按该方案完成真正的系统改造。为了把 Heimdall 从“V1/V2 混合态”推进到“版本主导、任务可恢复、Markdown 优先、结构与渲染解耦”的稳定系统，需要一份面向落地实施的规格，确保所有必要改动、验证路径与验收标准都被完整覆盖。

## What Changes

- 按 `architecture-upgrade-planV3.md` 的路线，新增一份面向落地实施的完整规格
- 优先实施 V3 阶段 0 与阶段 1：任务入口统一、后台执行器闭环、版本读取修正、刷新契约统一、任务工件与阶段状态落地
- 同步实施 V3 阶段 2：Wiki 生成链路从 XML 主导迁移到结构化规划 + Markdown 页面草案 + 全局收敛 + 渲染后处理
- 将 Ask、Slides、Workshop 全部并入 `RepositoryVersion` / `WikiVersion` 主锚点，避免继续直接读取旧 `Wiki` 聚合数据
- 修复数据库初始化、迁移治理、落库事务边界、失败恢复和双向量写入的一致性
- 以提供的 PostgreSQL、Ollama 与目标仓库作为联调验证环境，补齐构建、迁移、生成、检索与页面联调验证
- 将 `Microsoft Agent Framework` 保持为后置增强能力，仅在前置条件满足后预留局部试点，不作为本轮主链路实施内容

## Impact

- Affected specs:
  - `plan-architecture-upgrade-v3`
  - `upgrade-architecture-v2`
- Affected code:
  - `backend/Heimdall.Api/Program.cs`
  - `backend/Heimdall.Api/Controllers/TasksController.cs`
  - `backend/Heimdall.Api/Controllers/TaskStatusController.cs`
  - `backend/Heimdall.Api/Controllers/WikiVersionController.cs`
  - `backend/Heimdall.Api/Controllers/WikiCacheController.cs`
  - `backend/Heimdall.Api/Controllers/WikiCompareController.cs`
  - `backend/Heimdall.Core/Services/Tasks/TaskQueueService.cs`
  - `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs`
  - `backend/Heimdall.Core/Services/Tasks/TaskProgressService.cs`
  - `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs`
  - `backend/Heimdall.Core/Services/Rag/RagContextService.cs`
  - `backend/Heimdall.Repository/Repositories/TaskRepository.cs`
  - `backend/Heimdall.Repository/Repositories/WikiRepository.cs`
  - `backend/Heimdall.Repository/Repositories/WikiPageRepository.cs`
  - `backend/Heimdall.Repository/Migrations/*`
  - `frontend/src/app/repositories/[repositoryId]/page.tsx`
  - `frontend/src/components/VersionSwitcher.tsx`
  - `frontend/src/components/RefreshPanel.tsx`
  - `frontend/src/components/Ask.tsx`
  - `frontend/src/app/repositories/[repositoryId]/slides/page.tsx`
  - `frontend/src/app/repositories/[repositoryId]/workshop/page.tsx`
  - `doc/architecture/architecture-upgrade-planV3.md`

## ADDED Requirements

### Requirement: 统一任务执行主链路

系统 SHALL 将 Wiki 生成、刷新、状态追踪、阶段推进统一到单一后台任务主链路，禁止继续通过控制器内 `Task.Run` 或服务内部火忘式后台任务绕过统一执行器。

#### Scenario: 创建并执行 Wiki 刷新任务

- **WHEN** 用户调用 `POST /api/repositories/{repositoryId}/wiki/refresh`
- **THEN** 控制器只创建任务记录并返回稳定的任务响应
- **AND** 后台执行器负责实际消费任务、推进阶段、写入状态和工件
- **AND** 同一条任务不会被两套不同入口重复启动

### Requirement: 版本模型成为唯一运行时事实来源

系统 SHALL 以 `RepositoryVersion` 与 `WikiVersion` 作为页面读取、Ask、Slides、Workshop、比较与发布回滚的唯一运行时版本锚点。旧 `Wiki` 聚合数据仅允许作为兼容层存在，不得继续作为主读取路径。

#### Scenario: 读取指定 Wiki 版本

- **WHEN** 前端选择某个 `wikiVersionId`
- **THEN** 页面正文、页面树、相关关系、Ask、Slides 与 Workshop 都基于同一个 `wikiVersionId` 读取或生成
- **AND** 不再回退到旧 `Wiki` 记录聚合结果

### Requirement: 任务工件与阶段状态持久化

系统 SHALL 为长任务建立显式的阶段状态、恢复点与工件持久化机制，至少覆盖结构规划、页面批次生成、关系补全、全局收敛与渲染结果。

#### Scenario: 长任务执行中失败

- **WHEN** Wiki 生成任务在中途失败
- **THEN** 系统可以准确记录失败阶段、最近成功工件与恢复点
- **AND** 任务重试时可以从合适的阶段继续，而不是只能整链路重跑

### Requirement: 生成链路重构为 Markdown 优先的四段式

系统 SHALL 将 Wiki 生成重构为“结构规划、页面草案、全局收敛、渲染后处理”四段式流程，并以 Markdown 作为主内容格式，HTML 仅作为受控扩展。

#### Scenario: 生成复杂 Wiki 页面

- **WHEN** 系统为仓库生成页面内容
- **THEN** 页面草案以 Markdown、Frontmatter、Mermaid 与结构块元数据为主
- **AND** 全局收敛阶段负责修正重复、遗漏、前置阅读与交叉引用
- **AND** 最终渲染结果稳定供前端消费

### Requirement: Ask、Slides、Workshop 并轨到统一知识底座

系统 SHALL 让 Ask、Slides、Workshop 继承当前选中的 `RepositoryVersion` / `WikiVersion`，并优先消费版本化页面、双向量检索与页面关系，而不是继续直接拼接旧 `Wiki` 内容。

#### Scenario: 基于当前浏览版本派生内容

- **WHEN** 用户在仓库页切换到某个指定版本后发起 Ask、Slides 或 Workshop
- **THEN** 派生结果基于当前版本知识底座生成
- **AND** 不会因为旧缓存读取链路而出现版本错位

### Requirement: 数据库与向量写入闭环

系统 SHALL 在 PostgreSQL 迁移、主数据写库、版本写库、页面写库、双向量写入与任务完成态之间建立可验证的一致性约束，避免“任务已完成但数据未完整落库”的状态。

#### Scenario: 任务成功完成

- **WHEN** Wiki 任务被标记为 `completed`
- **THEN** `TaskRecord`、`RepositoryVersion`、`WikiVersion`、`WikiPage`、关键工件和必要向量数据必须已按设计落盘
- **AND** 相关页面与任务状态可被接口正确读取和验证

### Requirement: 使用调试环境完成联调验证

系统 SHALL 使用用户提供的 PostgreSQL、Ollama 与目标仓库作为联调环境，验证迁移、版本发现、Wiki 生成、双向量检索与前端页面联调结果。

#### Scenario: 使用调试仓库进行端到端验证

- **WHEN** 使用 `http://gitlab.beisencorp.com/AppCenter/Beisen.AppCenter.Ops` 作为验证目标
- **THEN** 系统能够完成导入、刷新、任务执行、页面读取、Ask/Slides/Workshop 派生与关键状态验证
- **AND** 验证结果可用于确认 V3 改造是否真正闭环

## MODIFIED Requirements

### Requirement: Wiki 刷新与生成入口

**原行为**：刷新接口、控制器直接任务启动、旧页面轮询链路并存，前端刷新后可能又回退到旧的 `generateWikiTask` 流程。

**新行为**：刷新接口成为唯一正式入口，返回统一的任务与版本响应；前端根据该响应进行状态追踪与页面更新，不再使用旧的旁路生成流程。

#### Scenario: 前端点击刷新

- **WHEN** 用户在仓库页执行刷新
- **THEN** 前端调用统一刷新接口并根据稳定返回字段更新页面状态
- **AND** 后端只通过统一执行器处理任务

### Requirement: 页面读取与版本切换

**原行为**：部分接口仍通过旧 `Wiki` 记录聚合页面，版本详情逻辑中存在 `WikiId` 与 `WikiSpaceId` 混用。

**新行为**：页面与版本详情一律按 `WikiVersionId` 读取，接口返回稳定页面标识与页面树结构，前端版本切换逻辑只基于版本模型工作。

#### Scenario: 访问版本详情页

- **WHEN** 用户请求指定版本详情或页面列表
- **THEN** 后端直接按 `WikiVersionId` 返回页面与关系数据
- **AND** 不再通过旧 `Wiki` 中转过滤

## REMOVED Requirements

### Requirement: 本轮全面引入 Microsoft Agent Framework

**Reason**: 当前阶段的主矛盾仍是任务可靠性、版本一致性、前后端契约与生成模型重构，全面引入 Agent 框架会抬高复杂度并延后真正的基础问题修复

**Migration**: 本轮仅预留接口与阶段边界，待统一队列执行、任务工件、阶段状态和版本主锚点稳定后，再对结构规划、收敛审查等局部环节做 Agent Framework 试点
