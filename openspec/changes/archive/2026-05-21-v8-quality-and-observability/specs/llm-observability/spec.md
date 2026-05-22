## MODIFIED Requirements

### Requirement: LLM 调用级指标收集
系统 SHALL 对每次 LLM 调用记录结构化指标，包含：TaskId、Stage、Provider、Model、InputTokens、OutputTokens、CacheHitTokens、LatencyMs、FirstTokenLatencyMs、Success、ErrorType、Timestamp。指标 SHALL 持久化到 `llm_call_metrics` 数据库表。**修复**：确保每次调用均记录指标，不允许跳过。

#### Scenario: 成功调用记录指标
- **WHEN** WikiTaskService 完成一次页面生成 LLM 调用
- **THEN** 系统记录指标：TaskId=当前任务ID，Stage=page_generation，InputTokens=从 ChatCompletionResponse.Usage.InputTokens 提取，OutputTokens=从 ChatCompletionResponse.Usage.OutputTokens 提取，CacheHitTokens=从 ChatCompletionResponse.Usage.CacheHitTokens 提取，Success=true
- **AND** 调用 RecordCallAsync 是必须执行的，不可被 try-catch 静默跳过

#### Scenario: Provider 不返回 usage 时估算
- **WHEN** Provider 响应中不包含 token usage 信息
- **THEN** 系统使用 TokenCounter 对 prompt 文本估算 InputTokens，对响应文本估算 OutputTokens，标注 IsEstimated=true

#### Scenario: 失败调用记录指标
- **WHEN** LLM 调用因超时或 API 错误失败
- **THEN** 系统记录指标：Success=false，ErrorType=具体错误类型，保留 InputTokens 估算值

### Requirement: 任务级指标聚合
系统 SHALL 提供按 TaskId 聚合的指标视图，包含：TotalCalls、TotalInputTokens、TotalOutputTokens、TotalCacheHitTokens、CacheHitRate、AverageLatencyMs、MaxLatencyMs、FailedCalls、EstimatedCost。**修复**：聚合值必须从 `llm_call_metrics` 表的实时数据计算，不得使用硬编码 0。

#### Scenario: 任务完成时汇总指标
- **WHEN** Wiki 生成任务完成
- **THEN** 系统从 llm_call_metrics 表查询该任务的所有调用记录，计算聚合指标并持久化
- **AND** LogTaskSummary 日志中的 Token 数据必须来自聚合查询结果

#### Scenario: Token 统计非零
- **WHEN** 任务执行了 3 次 LLM 调用，总计 Input=50000, Output=12000
- **THEN** 管理后台任务列表显示 Token 列值为 50000/12000，而非 0

### Requirement: Provider 响应标准化
系统 SHALL 为所有 ChatProvider 定义统一的响应模型 `ChatCompletionResponse`，包含：Content、Usage（InputTokens/OutputTokens/CacheHitTokens）、FinishReason、LatencyMs。**新增**：CacheHitTokens 字段从 Provider 响应中提取缓存命中信息。

#### Scenario: MiniMax 缓存命中提取
- **WHEN** MiniMax API 返回 `usage.cache_read_input_tokens` 字段值为 8000
- **THEN** 系统映射为 Usage.CacheHitTokens=8000

#### Scenario: Ollama 无缓存信息
- **WHEN** Ollama API 响应中不包含缓存相关字段
- **THEN** Usage.CacheHitTokens=0，SupportsCaching 元数据为 false

### Requirement: 缓存命中率统计
系统 SHALL 在任务聚合指标中计算缓存命中率：CacheHitRate = TotalCacheHitTokens / TotalInputTokens。前端 SHALL 在任务详情中展示缓存命中率。

#### Scenario: 缓存命中率计算
- **WHEN** 任务总计 InputTokens=100000，CacheHitTokens=32000
- **THEN** CacheHitRate=32%，前端显示"缓存命中: 32%"

## REMOVED Requirements

### Requirement: LogTaskSummary 使用硬编码零值
**Reason**: LogTaskSummary 方法存在 bug，传入硬编码 0 而非真实 LLM 指标数据。已在 commit f9c9aa0 中标记修复但实际仍调用旧签名。
**Migration**: 删除 LogTaskSummary 的旧签名调用，改用 ILlmObservabilityService.GetTaskSummaryAsync 获取真实数据后记录日志。
