# Heimdall 架构升级 V3 Spec

## Why

Heimdall 已完成 V1 与 V2 两轮升级，但当前仍存在前后端契约失配、Wiki 生成链路过度依赖 XML/HTML、任务执行与数据落库不可靠、V2 未完成项悬而未决等问题，已经开始直接影响可用性与后续演进。当前需要一份新的 V3 架构升级方案，在优先解决阶段性可用性问题的同时，为“50 页以上、多层复杂嵌套、复杂编排的仓库 Wiki”长期目标建立稳定底座。

## What Changes

- 新增一份 `doc/architecture/architecture-upgrade-planV3.md` 架构升级方案文档，替代零散的口头判断，形成可执行的 V3 路线图
- 系统性盘点 V2 已完成能力、未完成清单、当前代码现实与长期目标之间的差距
- 明确前端优先修复方向：统一页面主键、版本切换模型、刷新交互、任务进度与后端返回契约
- 明确生成链路重构方向：从“生成 XML 结构 + 生成 HTML/Markdown 内容”升级为“结构化规划 + Markdown 优先编排 + 后处理渲染”
- 明确复杂页面编排方案：使用 Markdown、Frontmatter、结构块元数据、页面关系与后处理布局，而不是直接要求大模型输出大量原始 HTML
- 明确 Agent Loop 演进方案：先建设可观测、可恢复、可增量的编排底座，再决定是否引入 `Microsoft Agent Framework`
- 增加技术选型结论：给出 `Microsoft Agent Framework` 的适用边界、引入时机、收益、成本、替代方案与建议结论
- 明确数据库与任务可靠性改造：事务边界、幂等、任务工件、阶段状态、失败恢复、审计与重试
- 将 V2 未完成项纳入 V3 分阶段实施顺序，避免继续形成“文档已规划、代码未闭环”的状态

## Impact

- Affected specs:
  - `upgrade-architecture-v2`
- Affected code:
  - `doc/architecture/architecture-upgrade-plan.md`
  - `doc/architecture/architecture-upgrade-planV2.md`
  - `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs`
  - `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs`
  - `backend/Heimdall.Core/Services/Tasks/TaskQueueService.cs`
  - `backend/Heimdall.Core/Services/Tasks/TaskProgressService.cs`
  - `backend/Heimdall.Repository/Repositories/TaskRepository.cs`
  - `backend/Heimdall.Api/Controllers/WikiVersionController.cs`
  - `backend/Heimdall.Api/Controllers/WikiCacheController.cs`
  - `frontend/src/app/repositories/[repositoryId]/page.tsx`
  - `frontend/src/components/VersionSwitcher.tsx`
  - `frontend/src/components/RefreshPanel.tsx`
  - `frontend/src/components/Ask.tsx`
  - `frontend/src/app/repositories/[repositoryId]/slides/page.tsx`
  - `frontend/src/app/repositories/[repositoryId]/workshop/page.tsx`

## ADDED Requirements

### Requirement: 产出 V3 架构升级方案文档

系统 SHALL 产出 `doc/architecture/architecture-upgrade-planV3.md`，作为 Heimdall 下一阶段升级改造的正式指导文档。该文档必须基于现有代码、既有升级文档、V2 未完成项与长期目标进行整合，而不是仅对用户原始想法做简单转述。

#### Scenario: 基于现状形成正式方案

- **WHEN** 产出 V3 文档
- **THEN** 文档必须明确引用当前系统的真实问题来源，包括前端契约问题、生成链路问题、落库可靠性问题与 V2 遗留问题
- **AND** 文档必须给出分阶段改造路径，而不是只给目标蓝图

### Requirement: V3 方案必须优先解决当前可用性问题

系统 SHALL 将“前端可用性、任务执行可靠性、数据库落库正确性、版本读取一致性”定义为 V3 的第一优先级，并在方案中明确短期止血项与中期结构化改造项。

#### Scenario: 定义阶段性优先级

- **WHEN** 文档定义实施顺序
- **THEN** 第一阶段必须优先覆盖前端后端契约统一、任务真实入队执行、关键写库事务化、版本与页面读取链路一致化
- **AND** 不得将复杂 Agent 框架引入排在这些基础稳定性问题之前

### Requirement: V3 方案必须重构内容生成与编排模型

系统 SHALL 将 Wiki 生成链路从“让模型直接产出 XML 结构和大量 HTML 标签内容”重构为“结构规划、页面草案、全局收敛、渲染后处理”四类职责分离的生成体系，并以 Markdown 作为首要内容表达格式。

#### Scenario: 定义结构化内容生成模式

- **WHEN** 文档描述新的生成链路
- **THEN** 必须明确区分结构工件、页面正文、页面块元数据、页面关系、布局渲染规则
- **AND** 页面正文优先使用 Markdown、表格、列表、引用块、Mermaid、Frontmatter 等可读格式
- **AND** 原始 HTML 应降级为受控白名单扩展，而不是默认主输出

### Requirement: V3 方案必须支持复杂 Wiki 的可扩展编排

系统 SHALL 为 50 页以上、多层复杂嵌套 Wiki 的生成提供可扩展的 Agent Loop 或 Loop-like 编排方案，使小模型也可以通过多轮规划、分页生成、全局修订与恢复继续逐步完成大型 Wiki。

#### Scenario: 定义大规模 Wiki 编排能力

- **WHEN** 文档设计复杂 Wiki 生成流程
- **THEN** 必须包含页面规划、批次生成、质量检查、重复检测、拆并页、交叉引用修补、失败恢复与增量重跑机制
- **AND** 必须说明如何控制上下文窗口、降低单次模型负载与提升可恢复性

### Requirement: V3 方案必须给出 Agent Framework 技术选型结论

系统 SHALL 对 `Microsoft Agent Framework` 是否适合在 Heimdall 当前阶段引入给出明确结论，包括“现在引入 / 延后引入 / 仅局部试点”三类判断之一，并说明判断依据。

#### Scenario: 形成框架选型建议

- **WHEN** 文档讨论 Agent Loop 与多 Agent 编排
- **THEN** 必须比较 `Microsoft Agent Framework` 与当前自研编排方案在复杂度、可观测性、恢复能力、与 .NET 技术栈适配、接入成本方面的差异
- **AND** 必须给出推荐时机与前置条件
- **AND** 必须说明其是否真正有助于复杂页面布局和内容组织，而不是笼统地把它视为“更高级的工作流框架”

### Requirement: V3 方案必须纳入 V2 未闭环项

系统 SHALL 在 V3 文档中显式承接 V2 尚未完成或虽已实现但未真正闭环的能力，避免形成第三份只追加不收敛的新规划文档。

#### Scenario: 处理 V2 未完成项

- **WHEN** 文档定义 V3 范围
- **THEN** 必须识别并吸收 V2 中的全局收敛阶段、失败恢复、前置阅读导航、版本对比闭环、前端构建验证、任务中间工件等未完成项
- **AND** 必须重新排序这些事项的优先级，纳入 V3 路线图

## MODIFIED Requirements

### Requirement: Wiki 生成主链路

**原行为**：系统主要依赖一次结构生成 Prompt 输出 XML，再逐页生成 Markdown 或 HTML 风格内容，任务执行、结构工件、版本工件、关系工件与前端渲染边界耦合较重。

**新行为**：系统应以“结构规划 DTO + Markdown 页面草案 + 关系工件 + 布局后处理”作为主链路，结构与渲染解耦，生成、校验、收敛、展示分别承担独立职责。

#### Scenario: 生成结构与页面内容

- **WHEN** 用户触发 Wiki 生成
- **THEN** 系统先生成结构化规划结果并保存为规范工件
- **AND** 再按批次生成 Markdown 页面内容
- **AND** 最后执行全局收敛与布局后处理，输出供前端消费的稳定结构

### Requirement: 任务执行与状态追踪

**原行为**：控制器直接 `Task.Run` 启动 Wiki 任务，队列、SSE 推送、任务工件与失败恢复能力不完整，任务完成态与真实落库状态可能不一致。

**新行为**：系统应使用统一的后台任务编排机制，显式记录阶段状态、任务工件、失败点与恢复点，保证任务状态与真实落库状态一致。

#### Scenario: 长任务执行

- **WHEN** 后端接收 Wiki 生成任务
- **THEN** 请求线程只负责创建任务并入队
- **AND** 后台执行器负责推进阶段、发布进度、写入工件、处理失败恢复
- **AND** 完成状态只能在关键工件与核心数据一致落库后才能写入

## REMOVED Requirements

### Requirement: 直接依赖原始 HTML 作为复杂布局主方案

**Reason**: 直接让大模型输出大量 HTML 标签会放大提示词复杂度、前端安全风险、可维护性问题与跨展示场景迁移成本，不利于长期演进

**Migration**: 复杂布局改由 Markdown 优先内容 + 结构块元数据 + 前端布局映射 + 少量白名单 HTML 扩展组合实现
