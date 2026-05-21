## Context

Heimdall 当前运行在 V5/V6 架构上，具备 8 阶段线性 Wiki 生成管线（仓库准备 → 代码索引 → 结构规划 → 页面生成 → 质量审查 → 渲染后处理 → 持久化 → 向量嵌入）。实际产出为 8-12 页扁平 Wiki，内容质量不足以支撑"仓库知识库"定位。

**当前痛点**：
1. 结构规划 prompt 硬编码 "8-12 pages"，无法产生 50+ 页深度内容
2. 页面生成为简单批量并行，子页面不继承父页面上下文，导致内容缺乏层次递进
3. 代码索引仅提取符号和文件元数据，缺少调用关系和设计模式等结构化理解
4. HybridSearchService 的向量检索半路未实现（仅 BM25 单路），检索精度受限
5. Provider 无计费模型元数据，无法区分 CodingPlan（按调用次数收费）和 TokenPlan（按 Token 收费），导致无法优化调用策略
6. LLM 交互缺乏 Token 消耗、缓存命中等可观测性数据

**约束**：
- .NET 10 运行时，PostgreSQL + pgvector
- 保持现有 API 向后兼容（不破坏前端契约）
- 不引入 Python 业务代码
- 所有文档中文

## Goals / Non-Goals

**Goals:**
- G1: Wiki 生成产出从 8-12 页提升至 30-80+ 页，支持 3-5 层目录嵌套
- G2: 引入深度代码理解（调用图、依赖拓扑、设计模式），让 Wiki 内容有真正的代码洞察
- G3: Provider 元数据完善，根据计费模型自动优化 LLM 调用策略
- G4: 上下文窗口智能填充，CodingPlan 单次调用吃满 60-70% 上下文
- G5: LLM 交互全链路可观测：Token 消耗、缓存命中、进度追踪、成本估算
- G6: 完成 HybridSearchService 向量检索集成，实现真正双路混合检索
- G7: 渐进式页面生成（父先子后），确保内容层次递进和交叉引用网络

**Non-Goals:**
- 本阶段不实现多语言 Wiki 生成（保持中文单语言）
- 不实现实时协作编辑 Wiki 功能
- 不引入外部文档导入（如 Confluence/Notion 导入）
- 不重构前端为新框架（保持 Next.js 16）
- 不实现 Wiki 增量更新（仍为全量重生成模式）
- 不实现 LLM 流式生成页面内容（保持完整响应模式）

## Decisions

### D1: 管线架构——从线性流水线到渐进式多阶段

**决定**：将管线从 8 阶段线性结构升级为 10 阶段渐进式架构：

```
阶段 1: 仓库准备（不变）
阶段 2: 代码结构索引（增强——增加调用图和依赖拓扑）
阶段 3: 深度代码理解（新增——LLM 辅助的架构分析）
阶段 4: 层级结构规划（重构——输出 3-5 层嵌套结构，50+ 页面）
阶段 5: 拓扑序渐进式页面生成（重构——父先子后，继承上下文）
阶段 6: 交叉引用编织（新增——自动页间链接和术语追踪）
阶段 7: 质量审查（增强——更严格的评分标准）
阶段 8: 渲染后处理（不变）
阶段 9: 持久化（不变）
阶段 10: 向量嵌入（不变）
```

**理由**：线性管线无法产生有深度递进关系的内容。拓扑序生成确保子页面"站在父页面的肩膀上"，交叉引用编织确保页面间形成知识网络而非孤立文档。

**备选方案**：
- A) 保持线性管线但增加二次遍历修复——复杂度相当但修复质量不如从源头按拓扑序生成
- B) 全异步事件驱动管线——过度设计，当前顺序执行的可调试性更重要

### D2: 深度代码理解——本地分析 + LLM 辅助混合方案

**决定**：分两层实现代码理解：
- **本地层**（不耗费 LLM 调用）：调用图构建（基于正则匹配方法调用）、模块依赖拓扑（基于 import/using 分析）、文件角色分类
- **LLM 辅助层**（1-2 次 LLM 调用）：对本地分析结果进行架构层面的高级理解——识别架构模式（MVC/微服务/CQRS）、识别关键数据流路径、识别设计模式

**理由**：纯本地分析无法理解"为什么这样设计"，但纯 LLM 分析成本过高且不稳定。混合方案用本地分析提供事实，LLM 提供洞察。

**备选方案**：
- A) 纯本地启发式分析——速度快但无法识别高层次架构意图
- B) 每个文件都用 LLM 分析——成本过高且冗余

### D3: Provider 计费策略——元数据驱动的调用优化

**决定**：在 `ProviderDefinition` 模型中扩展元数据字段：

```csharp
public class ProviderModelMetadata
{
    public BillingType BillingType { get; set; } // CodingPlan | TokenPlan
    public int MaxContextTokens { get; set; }    // 模型上下文窗口大小
    public int MaxOutputTokens { get; set; }     // 最大输出 Token 数
    public int? RateLimitPerMinute { get; set; }  // 速率限制（次/分钟）
    public decimal? InputTokenPrice { get; set; }  // 输入 Token 价格（每百万）
    public decimal? OutputTokenPrice { get; set; } // 输出 Token 价格（每百万）
    public decimal? CallPrice { get; set; }        // 单次调用价格（CodingPlan）
    public bool SupportsCaching { get; set; }      // 是否支持 prompt 缓存
}

public enum BillingType { CodingPlan, TokenPlan }
```

运行时根据 BillingType 选择调用策略：
- **CodingPlan**：合并多个页面的上下文到单次调用，填充至 MaxContextTokens * 0.65，减少总调用次数
- **TokenPlan**：每页单独调用但尽量填充上下文（相关代码片段最大化），关注 429 限频退避

**理由**：CodingPlan 模型（如 DeepSeek Pro 按次收费、Ollama 本地部署）的成本与调用次数成正比，应尽量减少调用次数；TokenPlan 模型（如 OpenAI、Google）的成本与 Token 消耗成正比，应关注输入 Token 的有效利用率。

**备选方案**：
- A) 统一策略不区分——浪费 CodingPlan 模型的每次调用上下文容量
- B) 用户手动选择策略——认知负担过重，应自动化

### D4: 上下文窗口智能填充——ContextPackingEngine

**决定**：实现 `IContextPackingService` 统一管理 prompt 组装时的上下文预算分配：

```
总预算 = MaxContextTokens * ContextFillRatio (默认 0.65)
分配策略：
  - 系统提示词：固定预算（约 2000 tokens）
  - 页面元数据（标题/描述/父页面上下文）：固定预算（约 1500 tokens）
  - 代码片段检索结果：动态预算（总预算 - 固定部分 - 保留输出空间）
  - 跨页面上下文摘要：弹性预算（按剩余空间自适应截断）
```

对于 CodingPlan 模型，当多页面共享相似检索上下文时，合并为一次调用生成多页内容（Batch Page Generation）。

**理由**：当前硬编码 `maxTotalTokens: 20_000` 既浪费大窗口模型的能力，也可能超出小窗口模型的限制。动态预算分配适配所有模型。

### D5: LLM 可观测性——分层指标收集

**决定**：建立三层可观测性体系：

1. **调用级指标**（每次 LLM 调用记录）：
   - InputTokens, OutputTokens, CacheHitTokens
   - Latency, FirstTokenLatency
   - Success/Failure, ErrorType
   - Provider, Model, Stage（在哪个管线阶段）

2. **任务级聚合**（每个 Wiki 生成任务汇总）：
   - TotalCalls, TotalInputTokens, TotalOutputTokens
   - CacheHitRate, AverageLatency
   - EstimatedCost（基于 ProviderModelMetadata 的价格计算）
   - ProgressPercent（已完成/总计页面数）

3. **控制台实时输出**：
   ```
   [WikiTask:abc123] ████████░░ 8/12 页 | Token: 125K↓ 48K↑ | 缓存: 32% | 成本: ¥0.42 | 耗时: 3m12s
   ```

**理由**：当前 `LogLlmCallAsync` 仅记录 prompt/response 文本和耗时，缺少 Token 消耗等关键指标（因为不是所有 Provider 都返回 usage 信息）。需要从 Provider 响应中提取 usage 数据。

### D6: 层级结构规划——3-5 层嵌套输出

**决定**：重写结构规划 prompt，要求 LLM 输出树形结构：

```json
{
  "wikiTitle": "仓库名 - 技术文档",
  "targetPageCount": 52,
  "maxDepth": 4,
  "sections": [
    {
      "id": "architecture",
      "title": "系统架构",
      "children": [
        {
          "id": "arch-overview",
          "title": "架构总览",
          "pageType": "overview",
          "children": [
            { "id": "arch-layers", "title": "分层架构详解", "pageType": "article" },
            { "id": "arch-patterns", "title": "设计模式应用", "pageType": "article" }
          ]
        }
      ]
    }
  ]
}
```

页面数量动态计算公式升级：
```
targetPages = max(15, min(80, modules * 3 + entryPoints * 2 + complexity_bonus))
complexity_bonus = designPatternCount * 2 + callGraphDepth * 3
```

**理由**：当前 `CalculateRecommendedPageCount` 公式 `max(8, min(60, moduleCount*2 + entryPointCount))` 过于保守且不考虑代码复杂度。

### D7: 拓扑序渐进式页面生成

**决定**：页面生成按树形拓扑序执行：

1. 先生成所有顶层 overview 页面（Level 0-1）
2. 再生成 section 页面（Level 2），每个 section 页面的 prompt 包含其父 overview 页面的摘要
3. 最后生成 article 页面（Level 3-4），每个 article 页面的 prompt 包含其父 section 页面的摘要和同级页面的标题列表
4. 同层级页面仍可并行生成（使用现有 batch 机制）

**理由**：当前扁平批量生成导致子页面不知道父页面写了什么，容易产生内容重复或断裂。拓扑序确保"由粗到细"的渐进深入。

### D8: 向量检索集成——完成 HybridSearchService

**决定**：利用现有 pgvector 基础设施，在 `HybridSearchService` 中实现向量检索半路：

1. 在代码嵌入阶段（Stage 10）已生成的向量数据基础上，新增针对当前任务的"即时嵌入"路径
2. 页面生成时对搜索 query 执行向量化，通过 pgvector 执行 cosine similarity 搜索
3. 使用 Reciprocal Rank Fusion (RRF) 合并 BM25 和向量搜索结果：`score = Σ 1/(k + rank_i)`，k=60

**理由**：当前 HybridSearchService 注释写明"BM25 only; vector search later"，本次需补全。纯 BM25 无法处理语义相似但关键词不同的检索场景。

## Risks / Trade-offs

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 50+ 页面生成时间过长（可能超过 1 小时） | 用户体验差，任务超时 | 实现进度持久化和断点续传；前端实时展示进度；设置阶段性超时而非全局超时 |
| CodingPlan 合并多页调用时 LLM 输出质量下降 | 合并后每页内容质量不如单独生成 | 设置合并上限（最多 3 页/次调用），质量审查阶段检测并回退到单页生成 |
| 调用图本地构建不准确（正则匹配有局限） | 结构理解信息错误导致 Wiki 内容偏差 | 标注置信度，低置信度调用关系不注入 prompt；LLM 辅助层可修正 |
| Provider 不返回 usage 信息（如部分 Ollama 模型） | Token 统计缺失 | 使用 tiktoken 估算输入 token；输出 token 按响应长度估算；标注为"估算值" |
| 向量检索依赖嵌入数据，首次生成时嵌入尚未完成 | 首次 Wiki 生成无法使用向量检索 | 首次生成降级为纯 BM25；嵌入完成后标记，后续刷新启用双路 |
| 深度嵌套结构过于复杂导致小仓库生成怪异结构 | 小项目被强制套入 4 层结构 | 根据代码复杂度动态决定最大深度：< 50 文件最多 2 层，50-200 文件最多 3 层，> 200 文件允许 4-5 层 |

## Migration Plan

1. **Phase 1（数据层）**：新增 EF Core 迁移，扩展 code_index_entries、新增 llm_call_metrics 表，不删除现有数据
2. **Phase 2（Infrastructure 层）**：实现 ProviderModelMetadata、ContextPackingEngine、向量检索集成、LLM 可观测性收集器
3. **Phase 3（Core 层）**：实现深度代码理解服务、重构 WikiTaskService 管线、重写 prompt 模板
4. **Phase 4（API + 前端）**：新增元数据查询端点、进度展示面板、深层 Wiki 树组件

**回滚策略**：每个 Phase 独立可部署。若 Phase 3 出现问题，可回退 WikiTaskService 到旧管线逻辑（保留 Phase 1-2 的基础设施不回滚）。新管线通过环境变量 `HEIMDALL_WIKI_PIPELINE_VERSION=v7` 启用，默认仍走旧逻辑直到验证通过。

## Open Questions

1. **CodingPlan 多页合并的最佳批量大小？** 需要实验确定——初步设为 3 页/次，后续根据质量反馈调整
2. **调用图构建是否需要支持跨语言调用？** 当前仅支持同语言内调用关系，跨语言（如 C# 调 TypeScript API）暂不处理
3. **向量嵌入的 chunk 粒度是否需要调整？** 当前按 80 行分块，函数/类边界分块效果可能更好但实现复杂度更高
4. **大仓库（5000+ 文件）的子代理模式如何与新的拓扑序生成结合？** 子代理按模块生成子树，主代理协调跨模块交叉引用
