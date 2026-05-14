# Heimdall 架构升级方案 V3

## 1. 文档目的

本文档用于承接 `architecture-upgrade-plan.md`、`architecture-upgrade-planV2.md` 与 `.trae/specs/plan-architecture-upgrade-v3/spec.md`，结合 2026-05-14 当前代码现状，形成 Heimdall 下一阶段的正式升级路线图。

V3 不再追加一份“理想蓝图式”文档，而是聚焦三个问题：

1. 已经落地的 V2 能力，哪些是真正可用的，哪些只是“表结构已到位但链路未闭环”
2. 当前最影响可用性的缺陷，应该先如何止血，避免继续叠加复杂度
3. 面向“50 页以上、多层复杂嵌套、复杂编排的仓库 Wiki”长期目标，底座应该先稳定什么，再增强什么

本文档的结论优先级高于口头判断，后续实现、拆任务、验收与回归测试均应以此为准。

## 2. 输入依据

本方案基于以下材料综合整理，而不是仅复述需求原文：

- `doc/architecture/architecture-upgrade-plan.md`
- `doc/architecture/architecture-upgrade-planV2.md`
- `doc/architecture/backend-architecture.md`
- `doc/architecture/frontend-architecture.md`
- `doc/architecture/audit-checklist.md`
- `.trae/specs/upgrade-architecture-v2/tasks.md`
- `.trae/specs/plan-architecture-upgrade-v3/spec.md`
- `.trae/specs/plan-architecture-upgrade-v3/tasks.md`
- 当前前后端与数据层代码，包括控制器、任务服务、版本服务、前端页面与组件实现

## 3. V3 核心判断

### 3.1 已完成的 V2 能力不应被忽略

V2 不是没有落地。以下能力已经真实存在，应作为 V3 的基础继续复用：

- 前端主路由已经从 `/{owner}/{repo}` 迁移到 `/repositories/{repositoryId}`
- 首页已经通过 `POST /api/repositories/import` 先导入仓库，再跳转到 `repositoryId` 路由
- `repositories`、`repository_versions`、`wiki_spaces`、`wiki_versions`、`wiki_page_relations`、`code_embedding_chunks`、`wiki_embedding_chunks` 已经有实体、仓储与迁移
- `RepositoryVersionsController`、`WikiVersionController`、`WikiCompareController` 已经具备初始接口
- `VersionSwitcher`、`RefreshPanel`、按 `repositoryId` 读取项目列表与 Wiki 的前端页面已经存在

结论：V3 不是推倒重来，而是要把“已落地但未闭环”的能力从半成品拉到稳定可用。

### 3.2 当前最大问题不是“缺少新表”，而是“读写链路仍处于 V1/V2 混合态”

虽然 V2 版本表与页面关系表已经落地，但当前真实运行链路仍然存在明显混用：

- Wiki 主生成链路仍然先生成 XML 结构，再逐页生成 Markdown
- `WikiTaskService` 仍然先写入旧 `Wiki` 记录，再事后补建 `WikiVersion`
- `WikiVersionController.GetPages` 仍通过旧 `Wiki` 读取页面，再用 `WikiVersionId` 过滤
- `WikiVersionController.GetVersionById` 使用 `GetByWikiIdAsync(version.WikiSpaceId)` 读取页面，实际把 `WikiSpaceId` 当成 `WikiId` 使用
- Ask、Slides、Workshop 仍直接读取旧 `Wiki` 数据，而不是明确绑定 `RepositoryVersion` / `WikiVersion`

结论：当前最大的技术债不是“缺少抽象”，而是“已经定义好的版本模型没有成为唯一事实来源”。

### 3.3 当前任务可靠性仍未达标，不能把 Agent Framework 放到更前面

当前任务执行链路存在以下问题：

- `TaskQueueService` 虽然是 `BackgroundService`，但 `ExecuteAsync` 只消费队列并记录日志，没有真正执行任务
- `TasksController.GenerateWiki` 仍使用 `Task.Run` 直接调用 `WikiTaskService.ExecuteAsync`，绕过统一队列
- `WikiTaskService` 内部又有两处 `Task.Run`，将代码嵌入与 Wiki 嵌入作为脱离主任务生命周期的“非致命后台动作”
- `RefreshOrchestrationService` 当前只判断“应复用/应排队/无变化”，但没有真正把刷新结果接到统一执行器
- 任务完成态先于双向量与中间工件闭环，导致“任务完成”与“可检索、可恢复、可复用”并不等价

结论：在任务编排、工件持久化、失败恢复、状态一致性没有稳定之前，不应优先引入复杂 Agent 框架。

### 3.4 当前前后端契约已经开始进入第二阶段，但仍有关键断点

当前前端已经有版本切换与刷新面板，但契约还没有真正稳定：

- 刷新 API 返回对象在前端实际使用中仍有字段名与语义不一致问题
- 刷新面板发起了 `/wiki/refresh`，但页面随后又回退到旧 `generateWikiTask` 轮询流程，形成双链路并存
- 版本切换器能够展示 `WikiVersion` 与 `RepositoryVersion`，但页面正文加载仍依赖旧 `Wiki` 聚合结果
- Ask、Slides、Workshop 页面仍按“即时请求 -> 同步返回结果”工作，未纳入统一任务状态模型

结论：V3 的第一阶段必须先统一“浏览态、刷新态、任务态、版本态”的契约，而不是继续新增页面功能。

### 3.5 复杂页面编排问题不能再依赖“让模型直接写大量 HTML”

当前系统已经证明：

- 页面正文生成 Markdown 是可行的
- 结构规划直接使用 XML，稳定性与维护成本都偏高
- Slides 仍以“计划文本 + 单页 HTML”直接输出
- 复杂编排、跨页面关系、全局收敛与布局映射并没有独立模型承载

结论：V3 必须把“结构规划”“页面草案”“关系工件”“渲染后处理”拆开，Markdown 作为主表达格式，HTML 退化为少量白名单扩展而非默认输出。

## 4. V3 总体目标与边界

### 4.1 阶段性目标

V3 的阶段性目标是先把 Heimdall 从“可以跑出结果”升级为“结果可解释、可恢复、可切换、可持续演进”的系统。短期必须完成：

1. 统一任务入口、执行入口与状态入口，避免同一类任务走多套后台逻辑
2. 让 `RepositoryVersion` 与 `WikiVersion` 真正成为读写链路的主锚点
3. 让前端页面、刷新、版本切换、Ask/Slides/Workshop 共享同一套版本选择模型
4. 把 Wiki 生成改造成“结构规划 DTO + Markdown 页面草案 + 关系工件 + 后处理”的稳定管道
5. 建立最小可用的任务工件、阶段状态、失败恢复与审计能力

### 4.2 长期目标

在不推翻 V3 设计的前提下，持续演进到以下能力：

- 支持 50 页以上的大规模 Wiki 分批生成与增量修复
- 支持 3 层以上目录、页面关系导航、前置阅读推荐与交叉引用补全
- 支持版本回看、版本比较、生成回归与发布回滚
- 支持从稳定 Wiki 继续派生 Ask、Slides、Workshop、FAQ、训练营材料
- 支持在小模型场景下通过多轮规划、批次执行、全局收敛完成大型仓库解读

### 4.3 明确不在 V3 第一阶段完成的事项

以下事项不是当前第一优先级：

- 全面引入 `Microsoft Agent Framework`
- 追求复杂前端可视化 diff 页面
- 追求多视角、多语言、多主题 Wiki 的产品化扩展
- 追求页面块级数据库模型的一次性完全落地

这些能力都建立在“版本与任务底座先稳定”的前提之上。

## 5. 当前现状盘点

### 5.1 已落地能力

| 维度 | 当前状态 | 判断 |
|------|----------|------|
| 主标识统一 | `repositoryId` 路由、导入接口、仓库详情接口已存在 | 基本完成 |
| 版本表结构 | `RepositoryVersion`、`WikiSpace`、`WikiVersion`、`WikiPageRelation` 已落地 | 已完成 |
| 双向量表结构 | `CodeEmbeddingChunk`、`WikiEmbeddingChunk` 已落地 | 已完成 |
| 刷新与版本接口 | 版本发现、Wiki 刷新、发布、比较接口已存在 | 已有骨架 |
| 前端版本 UI | `VersionSwitcher`、`RefreshPanel` 已存在 | 已有骨架 |
| 任务状态查询 | `TaskStatusController` 与任务表已存在 | 已有骨架 |

### 5.2 未闭环问题

| 维度 | 当前问题 | 影响 |
|------|----------|------|
| 任务执行 | 队列存在但不执行，控制器直接 `Task.Run` | 状态不一致、无法统一恢复 |
| 刷新语义 | 刷新结果与实际执行未贯通 | 前端提示与后台行为可能不一致 |
| 版本读取 | 仍通过旧 `Wiki` 聚合读取页面 | 版本化能力名义上存在、运行时不纯 |
| 版本详情 | 部分读取逻辑混用 `WikiId` / `WikiSpaceId` | 版本页数据存在错读风险 |
| Ask/Slides/Workshop | 未按版本消费知识底座 | 难以保证与当前浏览版本一致 |
| 双向量检索 | 已写入服务，但问答主链路未真正切换到稳定的版本锚点 | 检索收益没有完全释放 |
| 生成工件 | 缺少规范化任务工件与恢复点 | 大型 Wiki 无法真正断点续跑 |
| 全局收敛 | 结构规划后没有独立的收敛/纠偏阶段 | 复杂 Wiki 可读性不可控 |

### 5.3 V2 未完成项在 V3 中的处理结论

V2 中的未完成项不再单独作为“补尾工程”处理，而是并入 V3：

- 统一任务执行入口与任务进度闭环：提升为 P0
- 刷新语义、版本切换、版本读取一致性：提升为 P0
- 双向量检索在 Ask 中真正落地：提升为 P1
- 全局收敛阶段、任务工件、失败恢复：提升为 P1
- 前置阅读导航、页面关系增强：提升为 P2
- 版本对比页面增强：降为 P2

## 6. V3 核心架构决策

### AD1：`RepositoryVersion` 与 `WikiVersion` 成为唯一运行时版本锚点

从 V3 开始，以下逻辑必须强制绑定版本：

- 页面读取
- Ask 检索
- Slides 生成
- Workshop 生成
- 发布与回滚
- 对比与审计

旧 `Wiki` 实体在过渡期可保留，但角色调整为兼容层或聚合缓存，不再作为版本化读路径的主信源。

### AD2：所有长任务统一走“创建记录 -> 入队 -> 执行器推进 -> 阶段持久化 -> 完成校验”链路

统一执行原则如下：

1. 控制器只负责校验、创建任务、返回 `task_id`
2. 后台执行器负责推进阶段与阶段状态
3. 每个阶段都必须有明确的输入、输出与恢复点
4. 只有当核心数据与关键工件都落盘成功后，任务才能标记为完成
5. 不允许控制器或业务服务内部再随意使用脱离主任务生命周期的 `Task.Run`

### AD3：Wiki 生成主链路重构为四段式

V3 正式将生成链路定义为四段：

1. 结构规划：输出结构 DTO / 规划工件，不再以 XML 作为长期主格式
2. 页面草案：按批次生成 Markdown 页面草案
3. 全局收敛：检查重复、遗漏、前置阅读、目录平衡、标题风格与交叉引用
4. 渲染后处理：将 Markdown、Frontmatter、关系元数据转成前端稳定消费结构

这里的关键不是“多做一步”，而是把当前耦合在一个 `WikiTaskService` 里的职责拆开。

### AD4：Markdown 是主内容格式，HTML 是受控扩展

V3 的页面正文和结构化内容优先采用：

- Markdown
- 表格、列表、引用块
- Frontmatter
- Mermaid
- 结构块元数据

原始 HTML 的定位调整为：

- 仅用于受控白名单扩展
- 仅用于最终渲染阶段或少量导出场景
- 不再作为复杂 Wiki 内容生成的默认输出

这一定义同样适用于 Slides 与 Workshop：应优先从稳定 Markdown / 结构工件派生，而不是直接要求大模型产出大段 HTML。

### AD5：任务工件必须成为 V3 的一等公民

V3 至少需要以下工件类型：

- `planning_artifact`：结构规划结果
- `page_batch_artifact`：页面批次生成结果
- `relation_artifact`：页面关系与前置阅读关系
- `quality_report_artifact`：全局收敛检查结果
- `render_artifact`：供前端消费的稳定结果快照

短期可以先通过新增 `task_artifacts` 表实现，必要时保留 `TaskRecord.ResultJson` 作为摘要字段，但不能继续只靠单一 `ResultJson` 承载复杂执行结果。

## 7. 目标链路设计

### 7.1 统一任务流

```text
POST /api/repositories/{repositoryId}/wiki/refresh
    -> 创建 TaskRecord
    -> 写入 refresh request / target version / config hash
    -> 入统一队列
    -> Worker 解析版本
    -> 生成结构规划工件
    -> 分批生成 Markdown 页面
    -> 执行全局收敛
    -> 写入 WikiVersion / WikiPage / WikiPageRelation
    -> 写入双向量
    -> 发布或保留为 ready
    -> 标记 completed
```

### 7.2 前端消费流

```text
仓库主页
    -> 读取仓库详情
    -> 读取 published wiki version / 指定 wiki version
    -> 读取版本化页面列表
    -> Ask / Slides / Workshop 默认继承当前 wiki version
    -> 刷新操作只发 refresh request，不直接删除缓存再重跑
```

### 7.3 页面数据模型

V3 在当前表结构基础上优先稳定以下页面字段语义：

- `WikiPage.WikiVersionId`：必填，作为页面归属锚点
- `WikiPage.PageType`：用于渲染与导航
- `WikiPage.NavTitle`：用于侧边栏与版本比较
- `WikiPage.OutlineJson`：用于目录与前置阅读推导
- `WikiPage.SourceCoverageJson`：用于溯源与质量检查
- `WikiPage.Status`：用于阶段推进和恢复

如果后续复杂编排继续增强，再新增块级表；第一阶段不要求一次性做块级持久化。

## 8. 分阶段路线图

### 阶段 0：止血与契约收敛

目标：

- 解决当前最影响可用性的断点，避免继续用双链路跑任务

范围：

- `TasksController` 不再直接 `Task.Run` 执行 Wiki 任务
- `TaskQueueService` 正式承担执行职责，真正调用任务处理器
- `RefreshOrchestrationService` 只负责版本决策，不负责“假排队”
- `/wiki/refresh` 返回结果与前端消费字段统一
- 前端仓库页刷新后不再回退到“删除缓存 + 重新生成”的旧逻辑
- `WikiVersionController.GetPages`、`GetVersionById` 修正为直接按 `WikiVersionId` 读取页面

验收标准：

- `POST /api/repositories/{repositoryId}/wiki/refresh` 始终返回稳定结果：`task_id`、`repository_version_id`、`wiki_version_id`、`result_type`、`change_status`
- 刷新任务只会被创建一次，不会由控制器和刷新流程各自再起一套后台执行
- 页面切换到指定版本后，Ask/Slides/Workshop 能拿到同一个 `wikiVersionId`

回滚思路：

- 保留旧页面读取接口作为只读兼容层
- 新旧响应字段并行一个短周期，前端完成切换后再删除旧字段

### 阶段 1：任务可靠性与版本闭环

目标：

- 让“任务完成”真正等于“核心结果已落库且可恢复”

范围：

- 新增 `task_artifacts` 与阶段状态模型
- 将结构规划、页面批次、关系补全、收敛报告写为工件
- 页面写库、版本写库、发布态更新定义清晰事务边界
- 取消主任务链路中的火忘式 `Task.Run`
- 代码向量与 Wiki 向量写入纳入可观测阶段，而不是非致命后台动作

验收标准：

- 任务失败后可定位失败阶段和最后成功工件
- 任务重试时可从最近恢复点继续，而不是强制整链路重跑
- `TaskRecord.ResultWikiVersionId`、`ResolvedRepositoryVersionId` 与真实落库结果一致

回滚思路：

- 工件表新增不影响既有主表
- 失败时可继续保留旧版本作为浏览默认值

### 阶段 2：生成模型重构

目标：

- 把 XML 主导的生成方式升级为结构化规划与 Markdown 优先的生成方式

范围：

- 结构规划从 XML 切换到 JSON DTO 或严格结构化文本
- 页面草案输出标准 Markdown，辅以 Frontmatter 与块级元数据
- 新增全局收敛服务：重复检测、拆页合页、标题风格统一、引用修补
- Slides 与 Workshop 改为消费 `WikiVersion` 与页面工件，不再直接绕过版本层

验收标准：

- 结构工件可在不重新调用结构规划模型的前提下重复驱动页面生成
- 页面正文不再依赖原始 HTML 作为主表达格式
- 复杂 Wiki 出现结构问题时可以通过收敛阶段修复，而不是重跑整份 Wiki

回滚思路：

- 结构规划可保留 XML 兼容解析器一个版本周期
- 新旧生成器可以按生成档位并行灰度

### 阶段 3：大规模 Wiki 编排与增量能力

目标：

- 为 50 页以上、多层嵌套 Wiki 提供低耦合、可恢复的编排底座

范围：

- 按模块分批规划与分页生成
- 建立页面级变更影响分析
- 引入批次重跑、页面级重跑、关系重算
- Ask 支持基于 `RepositoryVersion` + `WikiVersion` 的双向量联合召回

验收标准：

- 50 页以上 Wiki 可以分批生成并支持失败恢复
- Ask/Slides/Workshop 与当前浏览版本保持一致
- 版本对比可正确识别新增页、删除页、显著变化页

### 阶段 4：Agent Loop 增强与局部试点

目标：

- 在底座稳定后，再引入更强的多轮编排能力

范围：

- 对规划、审查、收敛三个子任务做 Loop-like 编排
- 在单个阶段内试点多 Agent 协同，而不是替换整个任务系统
- 基于真实观测数据决定是否引入 `Microsoft Agent Framework`

验收标准：

- 即使关闭 Agent 能力，主链路仍可独立运行
- Agent 试点只影响特定阶段，不影响基础任务可靠性

## 9. Microsoft Agent Framework 选型结论

### 9.1 结论

V3 当前阶段的明确建议是：**延后引入，待底座稳定后做局部试点，不做全面接入。**

### 9.2 原因

`Microsoft Agent Framework` 能提供的价值主要在于：

- 多 Agent 协作抽象
- 更清晰的工具调用与工作流组织
- 在复杂规划、审查、反思场景下提供更自然的编排方式

但 Heimdall 当前最大的痛点并不在这里，而在于：

- 任务执行入口不统一
- 版本化读写链路尚未成为唯一事实来源
- 阶段状态、工件持久化、失败恢复没有闭环
- 复杂页面布局本质上是内容模型与渲染模型问题，不是 Agent 框架问题

如果现在全面引入，结果大概率是：

- 在不稳定底座上再叠一层抽象
- 难以判断故障来自业务链路还是 Agent 编排
- 把“该先解决的数据与任务问题”继续后移

### 9.3 适合的引入时机

满足以下前置条件后，才适合局部试点：

1. 统一队列执行已上线并稳定
2. 任务工件与阶段状态已完善
3. `RepositoryVersion` / `WikiVersion` 已成为真实运行主锚点
4. 结构规划、收敛、质量检查已拆成可独立调用的阶段服务

### 9.4 适合的试点边界

优先试点以下子场景，而非整链路替换：

- 结构规划 Agent
- 全局收敛 / 质量审查 Agent
- 重复检测与交叉引用修补 Agent

不建议一开始就让 Agent Framework 直接负责：

- 页面最终落库
- 发布态切换
- 任务状态推进
- 前后端契约编排

## 10. 关键实施清单

### 10.1 P0 止血项

- 统一 Wiki 刷新、任务创建与后台执行入口
- 修正版本读取接口，去掉 `WikiId` / `WikiSpaceId` 混用
- 统一前后端刷新返回字段与语义
- 让 Ask/Slides/Workshop 明确继承当前 `wikiVersionId`
- 禁止新的火忘式后台任务继续进入主链路

### 10.2 P1 结构改造项

- 新增任务工件持久化
- 生成流程重构为四段式
- 双向量写入和读取纳入主链路阶段
- 建立全局收敛与质量报告

### 10.3 P2 增强项

- 页面级增量重建
- 前置阅读与关系导航增强
- 版本对比页面增强
- Agent Loop 局部试点

## 11. 验证与验收方式

V3 文档本身的验证分两层：

### 11.1 一致性验证

应确认以下判断与当前代码一致：

- 新 `repositoryId` 路由和导入接口已存在
- `RepositoryVersion`、`WikiVersion`、双向量实体与迁移已存在
- `TaskQueueService` 当前尚未承担真正执行职责
- `TasksController` 与 `WikiTaskService` 仍存在绕开统一队列的 `Task.Run`
- 页面读取、Ask、Slides、Workshop 仍存在旧模型与新模型混用

### 11.2 实施后验收

阶段性改造完成后，应至少验证：

- 刷新任务是否立即返回 `task_id`
- 任务状态是否与真实版本落库一致
- 切换 `wikiVersionId` 后页面内容、Ask、Slides、Workshop 是否一致
- 失败后是否可从结构工件或批次工件恢复
- 复杂 Wiki 是否能在不依赖原始 HTML 的前提下稳定生成

### 11.3 当前代码核验结论（2026-05-14）

基于 `.trae/specs/implement-architecture-upgrade-v3/tasks.md`、`.trae/specs/implement-architecture-upgrade-v3/checklist.md`、当前仓库代码与本次实际构建结果，可以形成以下实施对齐结论：

- 已确认落地的能力：
  - `TaskQueueService` 已真正承担 Wiki 任务执行职责，`ExecuteAsync` 会消费队列并调用 `WikiTaskService.ExecuteAsync`
  - `TasksController` 已不再直接通过控制器内 `Task.Run` 启动 Wiki 主任务，`/tasks/wiki` 与 `/wiki/refresh` 已统一收敛到 `WikiTaskSubmissionService`
  - `TaskRecord` 已补齐 `CurrentStage`、`CurrentStageStatus`、`LastSuccessfulStage`、`LastArtifactId`、`AttemptCount` 等阶段状态字段，并已通过 `20260514155446_V3Phase1TaskArtifacts` 迁移落库
  - `task_artifacts` 已实际用于写入 `planning_artifact`、`page_batch_artifact`、`quality_report_artifact`、`relation_artifact`、`render_artifact`、`code_embedding_artifact`、`wiki_embedding_artifact`
  - `WikiTaskExecutionRepository` 已在单一事务中完成 Wiki 主数据、`RepositoryVersion`、`WikiVersion`、`WikiPage`、`WikiPageRelation` 与关键工件写入，并同步回写 `TaskRecord.ResultWikiVersionId` 与 `ResolvedRepositoryVersionId`
  - 代码向量与 Wiki 向量写入已从主链路中的“火忘式后台动作”改为显式可观测阶段
  - 结构规划与页面草案已切换为“JSON DTO 优先，XML 仅兼容回退”；页面正文与渲染输出已转为 Markdown、Frontmatter 与结构元数据优先
  - `AskTaskService`、`SlidesTaskService`、`WorkshopTaskService` 已统一通过 `VersionedKnowledgeService` 继承 `RepositoryVersion` / `WikiVersion`
  - 本次会话已实际验证 `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj` 与 `frontend` 下 `npm run build` 均通过

- 当前仍未完全闭环的点：
  - 前端仓库页仍保留“`/wiki/refresh` 未返回 `task_id` 时回退调用 `/api/tasks/wiki`”的兜底分支，因此“刷新后不再回退到旧任务创建双链路”这一点不能判定为完全完成
  - `RefreshOrchestrationService` 在刷新异常时会返回 `result_type = "no_change"`，前端统一刷新流会按“可复用结果”路径继续处理，这意味着“刷新失败”和“无变化复用”在契约语义上仍存在混淆风险
  - 因上述残留问题，“唯一正式入口”与“页面态 / 刷新态 / 任务态 / 版本态完全一致”仍只能判定为部分完成，不能作为已彻底验收项

- 当前会话无法直接勾选的外部联调项：
  - PostgreSQL 迁移、任务写库、向量表能力与调试环境验证
  - Ollama 向量与生成服务的真实联调
  - 目标仓库 `http://gitlab.beisencorp.com/AppCenter/Beisen.AppCenter.Ops` 的端到端验证

- 与 Agent Framework 相关的边界：
  - 本轮仍未把 `Microsoft Agent Framework` 接入主链路
  - 其前置条件与局部试点边界已在第 9 节明确，可作为后续阶段输入

## 12. 结论

V3 的本质不是再追加一轮“大重构设想”，而是把 Heimdall 从“V1 缓存模型 + V2 表结构”的混合态，升级为“版本主导、任务可恢复、Markdown 优先、结构与渲染解耦”的稳定系统。

当前最重要的结论有三条：

1. V2 已经完成了大量基础建设，V3 不应推倒重来，而应优先闭环
2. 当前第一优先级是任务可靠性、版本一致性、前后端契约统一，而不是更复杂的 Agent 框架
3. 复杂 Wiki 的真正解法是“结构化规划 + Markdown 页面草案 + 全局收敛 + 渲染后处理”，而不是继续扩大 XML 与 HTML 的直接输出范围

只要按本方案推进，Heimdall 才能在下一阶段真正具备稳定生成大型仓库 Wiki、持续派生 Ask/Slides/Workshop、并进一步引入更强编排能力的架构基础。
