## Why

当前结构规划唯一依赖 LLM 输出 JSON，M2.7 等推理模型存在 JSON 不稳定、延迟高（90秒）、成本浪费（$0.04/次）的问题。实际上代码索引已产出完整的调用图、依赖拓扑、模块结构、入口文件等数据，可以直接用确定性算法生成页面结构。

提供三种策略（确定性算法 / LLM 自由格式 / LLM 增强算法）供用户按需选择，默认使用零成本的确定性算法。

## What Changes

- **新增**：`StructurePlanningStrategy` 枚举，三种值：`Deterministic` / `LlmJson` / `LlmEnhanced`
- **新增**：`DeterministicStructurePlanner` — 基于 CodeIndexResult 直接生成 WikiStructureDto，零延迟零成本
- **修改**：`WikiTaskService` — 结构规划阶段根据策略分发，默认 Deterministic
- **修改**：`appsettings.json` — 新增 `StructurePlanning:Strategy` 配置项
- **保留**：现有 LLM JSON 解析逻辑不变（作为 `LlmJson` 策略）
- **不影响**：页面生成阶段——只改结构规划产出方式，下游消费不受影响

## Capabilities

### New Capabilities
- `structure-planning-strategies`: 三种可配置的结构规划策略，产品级对比效果

### Modified Capabilities
- `wiki-generation-pipeline`: 结构规划阶段改为策略驱动，不影响页面生成

## Impact

- **基础设施**：`WikiTaskService` 新增策略分发逻辑，约 30 行改动
- **配置**：`appsettings.json` 新增 `StructurePlanning.Strategy` 配置项
- **无新依赖**：确定性算法只用现有 CodeIndexResult 数据
- **向下兼容**：`LlmJson` 策略等同于当前行为
