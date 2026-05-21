## Why

当前 Heimdall V5/V6 的 Wiki 生成管线虽然实现了本地代码索引+检索增强的架构，但实际产出质量不达标：生成页面仅 8-12 页且层次扁平（单层目录）、缺乏对代码仓库的深度理解分析（调用关系、设计模式、数据流）、内容缺乏复杂的文档编排（没有交叉引用网络、没有渐进式深入结构）。与此同时，LLM Provider 交互层缺乏计费模型元数据（CodingPlan 按次收费 vs TokenPlan 按量收费）、未实现上下文窗口填充优化、交互日志缺失 Token 消耗/缓存命中/调用进度等关键指标。V7 需要从"能生成 Wiki"跨越到"生成高质量、多层次、深度解读的仓库知识库"。

## What Changes

- **BREAKING**: 重构 Wiki 生成管线为多阶段渐进式生成架构——引入"仓库理解分析"预处理阶段（调用图提取、设计模式识别、数据流追踪）、"层级结构规划"阶段（支持 3-5 层目录嵌套，50+ 页面）、"渐进式页面生成"（按依赖拓扑序生成，子页面继承父页面上下文）、"交叉引用编织"后处理（自动生成页间链接网络）
- 新增 Provider 计费模型元数据系统：每个 Provider/Model 组合声明 `BillingType`（CodingPlan/TokenPlan）、`MaxContextTokens`、`MaxOutputTokens`、`RateLimit` 等属性，运行时自动选择最优调用策略
- 新增上下文窗口智能填充引擎：CodingPlan 模型单次调用上下文填充至 60-70%（减少调用次数）；TokenPlan 模型按需分批但同样尽量吃满上下文（提升输出质量）；统一的 429 限频退避与重试机制
- 新增 LLM 交互可观测性系统：结构化记录每次 LLM 调用的 Token 输入/输出消耗、缓存命中率、调用耗时、模型响应延迟、按任务汇总的成本估算；控制台实时展示生成进度仪表盘
- 新增深度代码理解预处理：在本地代码索引基础上增加调用图构建（方法级调用关系）、模块依赖拓扑分析、入口点追踪、设计模式启发式检测（工厂/策略/观察者等）
- 重构结构规划阶段：支持输出 3-5 层嵌套的 Wiki 目录结构，引入"章节→子章节→页面"三级编排；按仓库复杂度动态确定目标页面数量（小项目 15-25 页，中型 30-50 页，大型 50-80+ 页）
- 重构页面生成为拓扑序渐进式生成：父页面先于子页面生成，子页面继承父页面的架构上下文；同层页面并行生成；引入"概览页→详解页→附录页"三级内容深度模型
- 新增交叉引用后处理阶段：分析所有已生成页面，自动插入"相关页面"链接块、"另见"引用、术语交叉引用、代码符号跨页面追踪链接
- 实现 HybridSearchService 的向量检索半路（当前仅 BM25），完成真正的双路混合检索

## Capabilities

### New Capabilities
- `deep-code-understanding`: 深度代码理解预处理能力——在本地代码索引基础上构建调用图（方法级）、模块依赖拓扑、数据流路径追踪、设计模式启发式检测，为后续结构规划和页面生成提供结构化的仓库理解模型
- `provider-billing-strategy`: Provider 计费策略与上下文优化系统——为每个 Provider/Model 声明计费模型（CodingPlan/TokenPlan）、上下文窗口限制、速率限制等元数据；根据计费模型自动选择调用策略（CodingPlan 合并调用填满上下文/TokenPlan 按需调用）；统一的限频退避与重试机制
- `llm-observability`: LLM 交互全链路可观测性——结构化记录每次调用的 Token 消耗（输入/输出/缓存命中）、调用耗时、模型参数、成功/失败状态；按任务维度汇总成本与性能指标；控制台进度仪表盘实时输出
- `wiki-deep-structure`: 深层 Wiki 结构编排引擎——支持 3-5 层目录嵌套、50+ 页面规划、"章节→子章节→页面"三级层次模型、按仓库复杂度动态确定规模、拓扑序渐进式生成（父先于子）、交叉引用编织后处理

### Modified Capabilities
- `wiki-generation-pipeline`: 管线整体流程重构——从 8 阶段线性流水线升级为多阶段渐进式生成架构，新增深度理解预处理和交叉引用后处理阶段，页面生成改为拓扑序渐进式
- `hybrid-code-retrieval`: 实现向量检索半路——当前仅 BM25 单路，需完成 pgvector 向量搜索集成，实现真正的 RRF 双路融合排序
- `code-indexing`: 索引深度扩展——在现有符号提取基础上增加调用图、依赖拓扑、设计模式检测等结构化理解信息
- `logging-enhancements`: 日志体系扩展——在现有结构化进度日志基础上增加 Token 消耗追踪、缓存命中统计、成本估算展示

## Impact

- **后端 Core 层（重大变更）**:
  - `Services/Repository/` 新增 `CallGraphBuilder`、`DependencyTopologyService`、`DesignPatternDetector` 等深度代码理解服务
  - `Services/Tasks/WikiTaskService.cs` 管线流程重构，新增深度理解阶段、拓扑序生成逻辑、交叉引用后处理阶段
  - `Services/Tasks/TaskPromptService.cs` 重写结构规划 prompt（支持多层嵌套输出）和页面生成 prompt（分级内容深度）
  - `Interfaces/Services/` 新增 `ICodeUnderstandingService`、`ILlmObservabilityService`、`IContextPackingService`
  - `Models/` 新增 `CallGraph`、`DependencyTopology`、`WikiDeepStructureDto`（支持多层嵌套）

- **后端 Infrastructure 层（中等变更）**:
  - `Providers/` 新增 `ProviderMetadata` 模型（BillingType、MaxContextTokens、RateLimit 等）
  - `Providers/ProviderRegistry.cs` 扩展元数据查询能力和调用策略选择
  - 新增 `ContextPacking/` 目录：上下文窗口填充引擎
  - 新增 `Observability/` 目录：LLM 调用指标收集与展示
  - `Search/` HybridSearchService 完成向量检索集成

- **后端 Repository 层**:
  - 新增迁移：扩展 `code_index_entries` 表增加调用图和依赖关系字段
  - 新增 `llm_call_metrics` 表存储 LLM 调用指标
  - `WikiPage` 实体扩展支持更深层级的 parentId 关系

- **后端 API 层**:
  - `Controllers/` 新增 Provider 元数据查询端点、LLM 指标查询端点
  - 现有 Wiki 刷新接口返回值扩展（预估页面数量、预估成本）

- **前端（中等变更）**:
  - Wiki 树组件支持 3-5 层深度折叠展示
  - 新增 LLM 调用进度/成本实时展示面板
  - Wiki 刷新弹窗增加预估页面数和成本提示

- **数据库**:
  - 新增 EF Core 迁移：`llm_call_metrics` 表、`code_index_entries` 表扩展、`provider_metadata` 配置表
  - `wiki_pages` 支持更深层级嵌套（已有 parentId 字段，无 schema 变更）

- **配置**:
  - `config/generator.json` 扩展 Provider 定义结构，增加 billingType、maxContextTokens 等字段
  - 新增环境变量：`HEIMDALL_CONTEXT_FILL_RATIO`（上下文填充比例，默认 0.65）
