# Heimdall 架构专题：Wiki 生成管线

> 文档类型：专题文档
>
> 所属分组：运行时
>
> 最后更新：2026-05-28
>
> 返回入口页：[`architecture.md`](../architecture.md)
>
> 顺序导航：上一篇 [`领域模型`](../overview/domain-model.md) ｜ 下一篇 [`AI Provider 架构`](../runtime/ai-provider-architecture.md)

## 文档范围

本文聚焦 Wiki 生成运行时主链路，描述 8 阶段流水线、结构规划策略、BM25 检索注入、Tool Call 增强、代码理解、大仓库分治策略以及 Workspace 文件存储集成。数据库表细节与 Provider 适配细节分别下沉到持久化与 AI Provider 专题。

## 核心职责

| 阶段 | 核心服务 | 职责 |
|------|------|------|
| 仓库准备 | `RefreshOrchestrationService`、仓库源服务 | 判定是否复用快照、拉取代码到 `workspace/repos/`、准备文件树 |
| 代码索引 | `CodeIndexService`、`TreeSitterAnalyzer` | Tree-sitter AST 解析（10 字段符号提取 + 调用图 + 设计模式检测）、构建 BM25 倒排索引 |
| 代码理解 | `CodeUnderstandingService` | 从文件、模块、系统三个层级提炼摘要；可选 Tool Call 增强（`QueryCallGraph`/`RetrieveClassDefinition`） |
| 结构规划 | `DeterministicStructurePlanner` / LLM 策略 | 生成 Wiki 页面结构骨架（`WikiStructureDto`），结构 JSON 写入 `workspace/wiki/{id}/structure.json` |
| 页面生成 | `WikiPageGenerationService` | 按页面维度组合 Prompt 和 BM25 检索上下文，可选 Tool Call 增强（`ReadCodeFile`/`SearchSymbols`），产出 Markdown 写入 `workspace/wiki/{id}/pages/` |
| 质量审查 | `WikiGlobalConvergenceService` | 算法评分（50 分起评，内容长度/代码块/表格/标题/关联页面/源文件加分），弱页（< 60 分）标记重生成 |
| 渲染后处理 | `WikiRenderPostProcessor` | 修正文档结构、Frontmatter、Mermaid 和链接 |
| 持久化 | 持久化事务服务 | 写入 `WikiVersion`（含 `structure_file_path` 和 `ast_version_id`）、页面树、关系和任务结果 |

## 关键流程

```mermaid
flowchart TD
    Start[提交 Wiki 刷新] --> Discover[解析或发现 RepositoryVersion]
    Discover --> AstCheck{可复用 AstVersion 存在?}
    AstCheck -->|否| AstParse[执行 AST 解析]
    AstParse --> AstWrite[写入 workspace/ast/{id}/]
    AstWrite --> Queue
    AstCheck -->|是| Queue[统一后台任务队列]
    Queue --> S1[Stage 1 仓库准备<br/>clone → workspace/repos/]
    S1 --> S2[Stage 2 代码索引<br/>Tree-sitter AST + BM25]
    S2 --> S3[Stage 3 代码理解<br/>可选 Tool Call 增强]
    S3 --> S4[Stage 4 结构规划<br/>三策略 → workspace/wiki/{id}/structure.json]
    S4 --> S5[Stage 5 页面生成<br/>BM25 检索 + 可选 Tool Call → workspace/wiki/{id}/pages/]
    S5 --> S6[Stage 6 质量审查<br/>算法评分 + 弱页重生成（最多 1 轮）]
    S6 --> S7[Stage 7 渲染后处理]
    S7 --> S8[Stage 8 持久化<br/>DB 元数据 + 路径引用]
    S8 --> Done[绑定 TaskRecord 与 WikiVersion]
```

### 结构规划三策略

| 策略 | 适用场景 | Token 消耗 | 特点 |
|------|------|------|------|
| `LlmJson`（默认） | 小到中型仓库、需要较强语义归纳 | 高（一次 LLM 调用） | LLM 生成完整 JSON，提示词注入 AST 数据（调用图、模块依赖、设计模式），解析为 `WikiStructureDto` |
| `Deterministic` | 安全、低成本、快速 | 零 | 基于 `CodeIndexResult` 目录级聚合算法生成，同目录文件 ≤3 合并为一页，>3 按重要性排序，测试目录合并，配置文件跳过 |
| `LlmEnhanced` | 既要稳定骨架又要增强语义 | 低（每 Section ~500 tokens） | 先用聚合算法生成骨架（id/depth/pages），再逐 Section 调用 LLM 润色 title/description/navTitle |

三种策略的最终产物均为 `WikiStructureDto`，页面生成阶段无感知。策略通过 `StructurePlanning.Strategy` 配置或 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY` 环境变量切换，运行时修改无需重启。

### 页面生成：BM25 检索 + Tool Call 增强

1. 页面主题与章节上下文触发 BM25 关键词检索。
2. `HybridSearchService` 对 BM25 结果做相关性排序、关键文件加权和上下文预算截断（根据模型上下文窗口动态调整注入量）。
3. 当 `ToolCall.Stage5.Enabled` 为 `true` 时，`ChatOptions.Tools` 注入 `ReadCodeFile` 和 `SearchSymbols` 的 `AIFunction` 列表，`FunctionInvokingChatClient` 自动处理 Tool Call 往返（最大 8 轮，支持并发调用）。
4. 结果片段与系统摘要、页面约束共同注入生成 Prompt。
5. 生成完成后，页面 Markdown 写入 `workspace/wiki/{wiki_version_id[:8]}/pages/{page_order:D4}_{slug}.md`，DB `WikiPage.content_file_path` 记录路径。

### 质量审查：算法评分 + 弱页重生成

- `WikiGlobalConvergenceService.CalculatePageQualityScore()` 对每个页面执行算法评分：
  - 起始 50 分
  - 内容长度加分 ≤15
  - 代码块 +10
  - 表格 +5
  - 结构化标题 +5
  - 关联页面 ≤8
  - 源文件 ≤7
- 弱页面（评分 < 60）标记为 `needs_regeneration`，触发一轮重生成
- 重生成 prompt 包含原始内容摘要、质量评估反馈与改进指导，检索 token 预算增加 30%
- 重生成后评分仍 < 60 则保留结果、记录警告，不再触发进一步重生成（最多 1 轮）

### 差异化提示词

页面生成根据 `ContentDepthLevel` 使用差异化提示词：

| 级别 | 侧重 |
|------|------|
| `overview` | 架构全景、模块关系、Mermaid 架构图、页面间导航引用 |
| `section` | 模块边界、数据流分析、组件间交互 |
| `article` | 代码深挖、逐方法分析、Mermaid 时序图、真实代码片段为核心 |

### 大仓库分治策略

当仓库规模超过阈值（默认 2000 个源代码文件）时，`AgentOrchestratorService.ShouldUseSubAgents()` 返回 `true`。当前检测逻辑已就绪（输出日志标记），但 `AssignModules` 子代理分发和完整 Orchestrator 路径尚未激活——所有任务仍走传统 8 阶段串行管线。完整实现规划为：

- 结构规划后按模块分组分配子代理，每个子代理负责 1-2 个模块
- 最多同时运行 3 个子代理（`SemaphoreSlim` 控制）
- 子代理只读（代码搜索、文件读取），写操作由主代理统一执行
- 主代理收集所有子代理报告后执行全局一致性合并

## 模块职责

| 模块 | 输入 | 输出 | 与其他模块的关系 |
|------|------|------|------|
| `CodeIndexService` + `TreeSitterAnalyzer` | 仓库文件树 | AST 符号（10 字段）、调用图、设计模式提示、BM25 索引 | 为代码理解和页面生成提供原材料 |
| `CodeUnderstandingService` | 索引、关键文件、仓库文档 | 文件/模块/系统摘要 | 为结构规划和页面生成提供语义背景 |
| 结构规划服务 | 系统摘要、目录结构、策略配置 | `WikiStructureDto` → `workspace/wiki/{id}/structure.json` | 决定后续生成批次与页面树 |
| 页面生成服务 | 页面结构、BM25 检索结果、Prompt 模板 | Markdown 页面 → `workspace/wiki/{id}/pages/` | 把规划转化为可展示知识内容 |
| `WikiGlobalConvergenceService` | 页面草案全集 | 质量报告、修正后的页面内容 | 负责跨页术语统一与薄弱页补强 |
| `WorkspaceService` | 版本 ID | 文件路径 | 为各阶段提供统一的路径解析和目录保证 |

## 依赖关系

| 依赖项 | 作用 |
|------|------|
| 三版本模型（`RepositoryVersion`/`AstVersion`/`WikiVersion`） | 确保整个生成链路基于正确的代码快照、AST 解析和知识版本 |
| Workspace 文件系统 | Wiki 页面 Markdown 和结构 JSON 的物理存储 |
| Prompt 模板体系 | 为不同阶段提供角色、约束与质量标准（五层结构：角色→上下文→指令→约束→自查清单） |
| AI Provider 架构 | 为理解、规划、生成、审查阶段提供模型调用能力（含 `FunctionInvokingChatClient` Tool Call 支持） |
| Tool Call 配置（`ToolCallConfigurationService`） | 统一管理 Stage 3/Stage 5 的 Tool Call 开关 |
| 任务工件 | 支撑阶段间恢复、审计与调试 |

## 设计取舍

| 取舍点 | 当前选择 | 理由 |
|------|------|------|
| 执行方式 | 统一后台队列而不是同步请求内执行 | 生成链路耗时长，需要恢复、排队与状态追踪 |
| 阶段拆分 | 8 阶段细粒度管线 | 便于观测、恢复、替换单阶段策略 |
| 检索方案 | BM25 主导检索 | 优先保证符号和路径命中稳定 |
| 内容存储 | Workspace 文件系统，DB 存路径引用 | 避免 DB TEXT 列膨胀 |
| Tool Call | 配置开关控制，默认关闭 | 渐进式启用，保证向后兼容 |
| 大仓库处理 | Agent 分治作为增强策略（检测已就绪，分发待激活） | 保持普通仓库路径简单，同时为超大仓库保留扩展能力 |
| 质量审查 | 算法评分（非 LLM 审查） | 确定性、低成本、可复现 |

## 导航与关联阅读

### 返回入口

- [`architecture.md`](../architecture.md)

### 顺序导航

- 上一篇：[`领域模型`](../overview/domain-model.md)
- 下一篇：[`AI Provider 架构`](../runtime/ai-provider-architecture.md)

### 关联阅读

- [`overview/domain-model.md`](../overview/domain-model.md)
- [`runtime/ai-provider-architecture.md`](./ai-provider-architecture.md)
- [`runtime/workspace-filesystem.md`](./workspace-filesystem.md)
- [`persistence/database-design.md`](../persistence/database-design.md)
- [`governance/architecture-decisions.md`](../governance/architecture-decisions.md)
