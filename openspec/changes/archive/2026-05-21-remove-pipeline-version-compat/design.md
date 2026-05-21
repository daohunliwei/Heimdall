## Context

当前 `WikiTaskService` 中通过 `isV7Pipeline` 布尔变量控制 10+ 处分叉逻辑，同时 `HeimdallConfigService.GetWikiPipelineVersion()` 读取 `HEIMDALL_WIKI_PIPELINE_VERSION` 环境变量决定走 v6 还是 v7 路径。用户明确表示不需要这种版本兼容——每次迭代就是最终产物，不需要保留旧路径。

## Goals / Non-Goals

**Goals:**
- 删除所有 v6/v7 双分支逻辑，V7 代码路径成为唯一路径
- 移除 `HEIMDALL_WIKI_PIPELINE_VERSION` 环境变量及其读取方法
- 合并 `CalculateRecommendedPageCount` 和 `CalculateRecommendedPageCountV7` 为单一方法

**Non-Goals:**
- 不改变 10 阶段管线的功能行为
- 不修改其他 Provider/配置相关的代码（Provider 计费策略与管线版本无关）
- 不修改前端代码（前端不依赖 pipelineVersion）

## Decisions

### 策略：全量删除 isV7Pipeline 分支，保留 V7 侧代码

**选择**: 将所有 `if (isV7Pipeline) { V7_CODE }` 改为直接执行 V7_CODE，删除 else 分支中的旧逻辑。

**替代方案**: 保留兼容开关但默认 V7。**否决**——用户的立场是迭代不需要兼容，保留开关本身就违反了"代码不应体现迭代版本"的原则。

**具体变更点**:

| 位置 | 当前代码 | 变更后 |
|------|---------|--------|
| `WikiTaskService:263` | `var isV7Pipeline = ...; if (isV7Pipeline) { ... }` | 直接执行深度代码理解阶段 |
| `WikiTaskService:350` | `isV7Pipeline ? BuildWikiStructurePromptV7(...) : BuildWikiStructurePrompt(...)` | 始终调用 `BuildWikiStructurePromptV7` |
| `WikiTaskService:444` | `if (isV7Pipeline) { 拓扑序排序 }` | 始终执行拓扑序排序 |
| `WikiTaskService:462` | `if (isV7Pipeline) { CodingPlan 大批次 }` | 始终执行 CodingPlan 批次逻辑 |
| `WikiTaskService:524` | `if (isV7Pipeline && ...) { 父页面上下文 }` | 始终注入父页面上下文 |
| `WikiTaskService:557` | `isV7Pipeline ? ContextPackingService : 20_000` | 始终使用 ContextPackingService |
| `WikiTaskService:881` | `if (isV7Pipeline) { LLM 指标汇总 }` | 始终执行 LLM 指标汇总 |

### 策略：V7 方法重命名为不带版本后缀

`CalculateRecommendedPageCountV7` → `CalculateRecommendedPageCount`（删除旧的无 V7 后缀的版本）。

`BuildWikiStructurePromptV7` → 保留原名，移除旧 `BuildWikiStructurePrompt`。后续可考虑去掉 V7 后缀，但涉及 TaskPromptService 内部多个方法，本次先清理调用方。

## Risks / Trade-offs

- **风险**: 旧 `BuildWikiStructurePrompt` 方法删除后不再可达。**缓解**: 确认无其他调用方后删除。
- **风险**: 如果 `ILlmObservabilityService` 未注册 DI，881 行的指标汇总可能抛异常。**缓解**: 已有 try-catch 保护。
