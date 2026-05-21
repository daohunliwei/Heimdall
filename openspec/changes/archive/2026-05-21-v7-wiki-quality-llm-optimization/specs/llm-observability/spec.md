## ADDED Requirements

### Requirement: LLM 调用级指标收集
系统 SHALL 对每次 LLM 调用记录结构化指标，包含：TaskId、Stage（管线阶段名）、Provider、Model、InputTokens、OutputTokens、CacheHitTokens、LatencyMs、FirstTokenLatencyMs、Success（bool）、ErrorType、Timestamp。指标 SHALL 持久化到 `llm_call_metrics` 数据库表。

#### Scenario: 成功调用记录指标
- **WHEN** WikiTaskService 完成一次页面生成 LLM 调用
- **THEN** 系统记录指标：TaskId=当前任务ID，Stage=page_generation，InputTokens=从响应 usage 提取，OutputTokens=从响应 usage 提取，LatencyMs=调用耗时，Success=true

#### Scenario: Provider 不返回 usage 时估算
- **WHEN** Ollama Provider 响应中不包含 token usage 信息
- **THEN** 系统使用 TokenCounter 对 prompt 文本估算 InputTokens，对响应文本估算 OutputTokens，标注 `IsEstimated=true`

#### Scenario: 失败调用记录指标
- **WHEN** LLM 调用因超时或 API 错误失败
- **THEN** 系统记录指标：Success=false，ErrorType=具体错误类型（Timeout/RateLimit/ServerError），保留 InputTokens 估算值

#### Scenario: 缓存命中记录
- **WHEN** Provider 响应中包含 cached_tokens 或 prompt_cache_hit 信息
- **THEN** 系统记录 CacheHitTokens 字段，CacheHitTokens > 0 表示有提示词缓存命中

### Requirement: 任务级指标聚合
系统 SHALL 提供按 TaskId 聚合的指标视图，包含：TotalCalls（总调用次数）、TotalInputTokens、TotalOutputTokens、TotalCacheHitTokens、CacheHitRate（缓存命中率）、AverageLatencyMs、MaxLatencyMs、FailedCalls、EstimatedCost（基于 ProviderModelMetadata 价格计算的预估成本）。

#### Scenario: 任务完成时汇总指标
- **WHEN** Wiki 生成任务完成（成功或失败）
- **THEN** 系统计算并持久化该任务的聚合指标，包含所有阶段的 Token 消耗总计和成本估算

#### Scenario: 成本估算公式
- **WHEN** 任务使用 TokenPlan 模型（InputTokenPrice=2.50/M，OutputTokenPrice=10.00/M）
- **THEN** EstimatedCost = (TotalInputTokens / 1_000_000 * 2.50) + (TotalOutputTokens / 1_000_000 * 10.00)

#### Scenario: CodingPlan 成本估算
- **WHEN** 任务使用 CodingPlan 模型（CallPrice=0.05）
- **THEN** EstimatedCost = TotalCalls * CallPrice

### Requirement: 控制台实时进度仪表盘
系统 SHALL 在 Wiki 生成过程中向控制台输出实时进度信息，格式为单行更新式仪表盘，包含：进度条、已完成/总页数、Token 消耗汇总、缓存命中率、累计成本、已用时间。

#### Scenario: 页面生成进度输出
- **WHEN** 第 8 个页面（共 12 个）生成完成
- **THEN** 控制台输出：`[WikiTask:abc123]  8/12 页 | Token: 125K 48K | 缓存: 32% | 成本: 0.42 | 耗时: 3m12s`

#### Scenario: 阶段切换输出
- **WHEN** 管线从"结构规划"阶段切换到"页面生成"阶段
- **THEN** 控制台输出阶段切换日志：`[WikiTask:abc123]  阶段切换: 结构规划  页面生成 | 规划页数: 52 `

#### Scenario: 错误和重试输出
- **WHEN** LLM 调用遇到 429 限频并触发重试
- **THEN** 控制台输出：`[WikiTask:abc123]  限频重试 | Provider: openai/gpt-4o | 等待: 30s | 第 2/3 次重试`

### Requirement: LLM 指标查询 API
系统 SHALL 提供 API 端点供前端查询 LLM 调用指标，包含：按任务查询聚合指标（`GET /api/tasks/{taskId}/metrics`）、按时间范围查询历史指标（`GET /api/admin/llm-metrics`）。

#### Scenario: 查询任务指标
- **WHEN** 前端请求 `GET /api/tasks/{taskId}/metrics`
- **THEN** 系统返回该任务的聚合指标 JSON：totalCalls、totalInputTokens、totalOutputTokens、cacheHitRate、estimatedCost、stages 数组（每阶段独立汇总）

#### Scenario: 查询历史指标
- **WHEN** 管理员请求 `GET /api/admin/llm-metrics?from=2026-05-01&to=2026-05-20`
- **THEN** 系统返回时间范围内所有任务的指标汇总：totalTasks、totalCost、averageCostPerTask、topProviders 排名

### Requirement: Provider 响应标准化
系统 SHALL 为所有 ChatProvider 定义统一的响应模型 `ChatCompletionResponse`，包含：Content（文本内容）、Usage（InputTokens/OutputTokens/CacheHitTokens）、FinishReason、LatencyMs。各 Provider 实现 SHALL 从各自的 API 响应格式中提取并映射到统一模型。

#### Scenario: OpenAI 响应映射
- **WHEN** OpenAI API 返回包含 `usage.prompt_tokens` 和 `usage.completion_tokens` 的响应
- **THEN** 系统映射为 Usage.InputTokens 和 Usage.OutputTokens

#### Scenario: Ollama 响应映射
- **WHEN** Ollama API 返回包含 `eval_count` 和 `prompt_eval_count` 的响应
- **THEN** 系统映射为 Usage.OutputTokens=eval_count 和 Usage.InputTokens=prompt_eval_count

#### Scenario: 无 usage 信息时的估算
- **WHEN** Provider 响应中无 token 用量信息
- **THEN** 系统使用 TokenCounter 估算，并在 Usage 中设置 `IsEstimated=true` 标志
