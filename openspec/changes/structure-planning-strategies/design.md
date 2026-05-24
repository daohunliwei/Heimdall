## Context

当前结构规划流程：
```
CodeIndexResult → BuildStructurePrompt → LLM → JSON → WikiStructureDto → 页面生成
```
问题：M2.7 推理模型 JSON 输出不稳定、延迟 90 秒、成本 $0.04/次。

代码索引已产出：129 调用图节点、114 边、32 模块依赖拓扑、11 个项目模块（含源文件数量和入口点）、入口文件列表。

## Goals / Non-Goals

**Goals:**
- 确定性算法零成本生成 WikiStructureDto
- 三种策略通过配置切换
- 策略变更不影响下游页面生成

**Non-Goals:**
- 不修改 WikiStructureDto 数据结构
- 不修改页面生成逻辑
- 不修改 LLM prompt 构建

## Decisions

### 决策 1：三种策略的职责边界

```
┌───────────────┐    ┌────────────────┐    ┌───────────────┐
│ Deterministic │    │   LlmJson      │    │ LlmEnhanced   │
│ (默认)        │    │   (当前行为)    │    │ (算法+LLM)    │
├───────────────┤    ├────────────────┤    ├───────────────┤
│ 算法 → DTO    │    │ LLM → JSON     │    │ 算法 → DTO    │
│               │    │ 解析 → DTO     │    │ LLM → 润色    │
│ 0ms, $0       │    │ 90s, $0.04     │    │ ~10s, ~$0.005 │
└───────────────┘    └────────────────┘    └───────────────┘
```

### 决策 2：确定性算法映射规则

```
模块名                     → Section Id/Title
模块依赖拓扑               → Section 排序（被依赖最多的先讲）
入口文件 (Importance≥8)    → Overview Section / Welcome Page
每个 .cs 文件              → Page
文件大小+Importance        → Page depth / importance
调用图出度/入度            → Page 间关系 (relatedPages)
目录层次                   → Page depth
```

**理由**：这些映射规则覆盖了 M2.7 产出 JSON 的全部结构化信息。LLM 的优势在于生成 title/description 文案，这可以通过 LlmEnhanced 策略单独增强。

### 决策 3：LlmEnhanced 的小成本 LLM 调用

LLM 只做润色，输入是算法已确定的 Section/Page 结构，输出自然语言润色建议：
```
输入:  { section: "architecture", pages: ["page-1", "page-2"] }
输出:  { title: "系统架构", description: "包含两层模块..." }
```
而非完整 JSON。减少 token 用量（~500 input, ~200 output），降低成本 90%。

### 决策 4：策略配置方式

`appsettings.json` / 环境变量：
```json
"StructurePlanning": {
  "Strategy": "Deterministic"  // Deterministic / LlmJson / LlmEnhanced
}
```

## Risks / Trade-offs

- **[风险] 确定性算法产出的标题不够人性化** → 缓解：LlmEnhanced 可补充润色；Deterministic 使用文件名驼峰转中文作为标题
- **[风险] 算法映射规则可能需要持续调优** → 缓解：规则作为可配置参数，初始版本覆盖 80% 场景
- **[权衡] 放弃 LLM 对"整体架构理解"的自由发挥** → 接受：调用图和依赖拓扑本身就是架构理解的确定性产出
