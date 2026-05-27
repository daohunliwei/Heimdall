## Purpose

可观测性——涵盖 OpenTelemetry 自动追踪、LLM 调用指标收集与持久化、结构化进度日志、任务监控页面及 Token 估算。
## Requirements
### Requirement: MEAI OpenTelemetry 自动追踪
系统 SHALL 通过 ChatClientBuilder.UseOpenTelemetry() 中间件自动记录 LLM 调用的 Trace 和 Metrics，替代自定义 LoggingChatClient。

#### Scenario: Trace span 自动创建
- **WHEN** 任意通过 ChatClientBuilder 管道发起的 LLM 调用
- **THEN** OpenTelemetry 中间件自动创建 span，携带 gen_ai.system、gen_ai.request.model、gen_ai.usage.input_tokens 等标准属性

#### Scenario: 与 LlmCallMetrics 表共存
- **WHEN** MEAI OpenTelemetry 中间件记录 Trace
- **THEN** 系统同时将指标持久化到 llm_call_metrics 表

### Requirement: LLM 调用级指标收集
系统 SHALL 对每次 LLM 调用记录结构化指标。指标数据来源从 ChatResponse.Usage（UsageDetails）读取。

#### Scenario: UsageDetails 导入指标
- **WHEN** LLM 调用完成并返回 ChatResponse
- **THEN** 从 UsageDetails 读取 InputTokenCount、OutputTokenCount、CachedInputTokenCount

### Requirement: 流式调用指标与 Token 估算
系统 SHALL 在流式调用完成后立即记录指标。Provider 不返回 usage 时使用 TokenCounter 估算，估算过程异常不影响主流程。

#### Scenario: 流式无 usage 时估算
- **WHEN** 流式调用完成但无 UsageDetails
- **THEN** 系统使用 TokenCounter 对完整 prompt 和 response 文本估算 Token，标记 IsEstimated=true

#### Scenario: 流式有 usage 时直接使用
- **WHEN** 流式调用的最后一个 ChatResponseUpdate 包含 UsageDetails
- **THEN** 系统直接采用，不做估算

#### Scenario: 流式调用耗时记录
- **WHEN** 流式调用成功完成
- **THEN** 系统记录 LatencyMs 和 FirstTokenLatencyMs

#### Scenario: Token 估算异常安全
- **WHEN** TokenCounter.EstimateTokenCount 抛出异常
- **THEN** 系统 catch 异常，UsageDetails 设为默认值（Token Count 为 0），流式响应正常返回

### Requirement: 任务结构化进度日志
Wiki 生成任务 SHALL 在每个关键步骤输出结构化进度日志，包含 Token 消耗追踪、缓存命中统计和累计成本展示，日志前缀区分类型。

#### Scenario: 页面生成步骤的进度日志
- **WHEN** Wiki 生成完成一个页面
- **THEN** 系统输出 `[WikiTask] 进度: 8/52 页 | Token: 125K↓ 48K↑ | 缓存: 32% | ¥0.42`

#### Scenario: LLM 调用日志
- **WHEN** LLM 调用完成
- **THEN** 输出 `[LLM] 调用完成 | Provider: xxx | Input: xxx | Output: xxx | Latency: x.xs`

#### Scenario: 任务完成的汇总日志
- **WHEN** Wiki 生成任务全部完成
- **THEN** 输出汇总日志包含总页数、深度、总耗时、LLM 调用次数、Token 总量、缓存命中率、成本

### Requirement: Tool Call 日志追踪
系统 SHALL 在 TaskLlmCallLog 中通过 ToolCallLogsJson 字段记录每次 LLM 调用的 Tool Call 详情。

#### Scenario: Tool Call 日志持久化
- **WHEN** Stage 5 页面生成，LLM 调用 2 次工具
- **THEN** TaskLlmCallLog.ToolCallLogsJson 包含 JSON 数组，记录每次工具调用的 ToolName、Arguments（脱敏）和结果

### Requirement: 任务监控页面
前端 /admin/tasks 页面 SHALL 包含统计卡片行（总任务数、Token 消耗、总成本、缓存命中率）和增强任务表格（每行显示 TaskId、类型、状态、进度、Token、缓存命中、成本、Provider、耗时）。

#### Scenario: 统计卡片展示
- **WHEN** 管理员打开 /admin/tasks 页面
- **THEN** 顶部展示累计任务数、Token 消耗、成本、平均缓存命中率

#### Scenario: 任务详情展开
- **WHEN** 管理员点击某任务的"详情"按钮
- **THEN** 展开 LLM 调用明细表：每次调用的 Stage、Provider、Model、Tokens、Latency、Cost、Success
