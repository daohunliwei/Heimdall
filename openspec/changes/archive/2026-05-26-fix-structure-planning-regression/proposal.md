## Why

`structure-planning-strategies`（a465e7d）将结构规划默认策略设为 `Deterministic`，其 "1 文件 = 1 页" 算法对大中型仓库产生灾难性页面数（libgit2sharp: 推荐 80 页 → 实际 1453 页），导致 16 小时生成时间 + 巨额 API 费用。同时 `LlmJson` 策略未利用 Tree-sitter AST 产出的调用图、依赖拓扑、重要性分数等结构化数据来辅助 LLM 分组决策。需立即修复默认策略并增强 LLM 策略对 AST 数据的利用。

## What Changes

- **BREAKING**: 默认策略从 `Deterministic` 改为 `LlmJson`（即 V9 原始行为）
- `Deterministic` 策略降级为显式 opt-in 的降级方案，并修复 `BuildStructure` 按目录/模块聚合文件，目标产出 ~`recommendedPageCount` 页（而非逐文件映射）
- `LlmJson` 策略增强：在 LLM 提示词中注入 Tree-sitter AST 产出的调用图摘要、模块依赖拓扑、高重要性文件列表、设计模式等结构化上下文，辅助 LLM 做出更精准的分组决策
- `LlmEnhanced` 策略同步受益：骨架生成改用修复后的聚合算法，LLM 润色阶段也享有增强上下文
- 移除 `appsettings.json` 中 `StructurePlanning.Strategy` 默认值，由代码中 `ResolveStructurePlanningStrategy()` 统一管理默认值

## Capabilities

### New Capabilities

- `ast-aware-structure-planning`: 结构规划阶段充分利用 Tree-sitter AST 产出（调用图、依赖拓扑、重要性分数、设计模式）作为 LLM 提示词上下文，提升页面分组质量

### Modified Capabilities

- `structure-planning-strategies`: 默认策略从 Determistic 改为 LlmJson；Deterministic 算法从逐文件映射改为按目录/模块聚合，产出页面数贴近 `recommendedPageCount` 推荐值
- `wiki-generation-pipeline`: 结构规划阶段提示词构建整合 AST 结构化数据；策略默认值变更影响管线行为

## Impact

- **配置**: `appsettings.json` 移除 `StructurePlanning.Strategy` 硬编码默认值；`ResolveStructurePlanningStrategy()` 默认返回 `LlmJson`
- **业务层**: `DeterministicStructurePlanner.BuildStructure` 重写聚合逻辑；`TaskPromptService` 结构规划提示词注入 AST 上下文
- **无新依赖**: 所有数据来自已有 Tree-sitter AST 产出和 CodeIndexResult
