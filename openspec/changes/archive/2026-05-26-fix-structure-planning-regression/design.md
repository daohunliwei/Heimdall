## Context

`structure-planning-strategies`（a465e7d）引入三种策略后，将默认值设为 `Deterministic`。其 `BuildStructure` 算法对每个源文件创建独立 Page，导致：
- libgit2sharp（1520 文件）→ 1453 页（推荐值 80 页）
- 页面生成阶段需要 1453 次 LLM 调用（~16 小时，高额 API 费用）
- Wiki 质量极差：测试文件、资源文件、配置文件全部独立成页

而原始的 `LlmJson` 策略（V9 行为）通过 LLM 智能分组，实际产出 30-80 页，且 `BuildWikiStructurePromptV7` 已经注入了 Tree-sitter AST 产出的 deep code understanding 数据（调用图、设计模式、架构层次、模块依赖拓扑）。问题不在提示词——在于提示词根本没被调用。

## Goals / Non-Goals

**Goals:**
- 将默认策略改回 `LlmJson`，恢复 V9 的 LLM 智能分组能力
- 修复 `Deterministic` 的 `BuildStructure` 算法，使产出页面数贴近 `recommendedPageCount` 推荐值
- 验证 libgit2sharp 仓库的结构规划产出在 50-100 页范围内
- 通过实际运行验证（MiniMax + PostgreSQL 10.189.10.252）

**Non-Goals:**
- 不修改 `WikiStructureDto` 数据结构
- 不修改页面生成阶段代码
- 不改动 Tool Call / Function Calling 配置
- 不新增 LLM 调用（LlmJson 本身已包含 1 次结构规划调用）

## Decisions

### 决策 1：默认策略从 Determistic 改为 LlmJson

**选择**: `ResolveStructurePlanningStrategy()` 默认返回 `LlmJson`，移除 `appsettings.json` 中的硬编码默认值。

**理由**:
- `LlmJson` 是 V9 验证过的稳定行为，LLM 智能分组产出 30-80 页
- `Deterministic` 的 "1 文件 = 1 页" 算法对小仓库（< 50 文件）可接受，但对大中型仓库是灾难
- AST 的 deep code understanding 数据已经在 `BuildWikiStructurePromptV7` 中被注入提示词，LLM 能充分利用这些数据做分组决策
- 环境变量 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY` 仍可覆盖

**替代方案**: 保持 Determistic 默认但修复算法 —— 不采用，LLM 分组的质量天花板远高于确定性算法，AST 数据的最佳消费者是 LLM 而非规则引擎。

### 决策 2：Deterministic 聚合算法——按目录分组

**选择**: 将 `BuildStructure` 的 "逐文件 → Page" 改为 "按目录 → Page"，目标产出 ~`recommendedPageCount` 页。

聚合规则：
```
1. 入口文件 → Overview Section（保持）
2. 每个模块 → Section
3. Section 内按文件目录层级分组：
   - 同一目录下文件数 ≤ 3 → 合并为一页（"X 目录工具集"）
   - 同一目录下文件数 > 3 → 按重要性分数排序，top-3 独立成页，其余合并
   - 测试目录（*Tests*, *test*, *Test*）→ 合并为一页（"测试概览"）
   - 配置文件（*.json, *.xml, *.config, *.csproj）→ 跳过
4. 高重要性文件（Importance >= 8）→ Architecture Section（保持）
5. 若最终页数仍超过 recommendedPageCount × 1.5 → 进一步合并低重要性页面
```

**理由**: 目录结构是代码组织的最自然边界。一个目录通常对应一个功能单元。测试目录和配置文件的页面价值极低。

**替代方案**: 用调用图聚类算法（Louvain/社区检测）—— 过度工程，对小仓库无收益。

### 决策 3：LlmJson 提示词增强——注入 CodeIndexResult 摘要

**选择**: 在 `BuildWikiStructurePromptV7` 的参数中新增 `CodeIndexResult` 摘要信息：
- 模块列表 + 文件数量
- 入口文件列表
- 推荐页面数（recommendedPageCount）
- 已注入的 deep code understanding 数据保持不变

**理由**: 当前提示词已注入 `CodeUnderstandingResult`（调用图、设计模式、架构层次），但缺少 `CodeIndexResult` 的模块文件分布数据。补充这些数据让 LLM 能更精准地判断每个模块需要多少页面。

### 决策 4：不修改 LlmEnhanced 策略

**选择**: `LlmEnhanced` 策略同步受益于 BuildStructure 修复，但 `PolishSectionsWithLlm` 实现不变。

**理由**: `LlmEnhanced` 仍然从 BuildSkeleton（= 修复后的 BuildStructure）出发，骨架的页面数已受控。LLM 润色只改标题/描述，不影响页面数。

## Risks / Trade-offs

- **[风险] LlmJson 的 1 次 LLM 调用增加延迟（~90s）和成本（~$0.04）** → 缓解：相比 1453 次页面生成调用的成本，1 次结构规划调用可忽略不计。用户可显式设置 Deterministic 降级。
- **[风险] Deterministic 目录聚合可能过度合并** → 缓解：聚合规则保守（≤3 文件才合并），且仅作为降级方案，非默认行为。
- **[风险] libgit2sharp 测试文件占比高，聚合后页数可能仍然偏多** → 缓解：测试目录直接合并为单页，配置文件跳过。
