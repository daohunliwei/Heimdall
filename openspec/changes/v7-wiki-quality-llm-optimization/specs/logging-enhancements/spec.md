## MODIFIED Requirements

### Requirement: 任务结构化进度日志
Wiki 生成任务 SHALL 在每个关键步骤输出结构化进度日志。V7 中 SHALL 新增 Token 消耗追踪、缓存命中统计和累计成本展示。

#### Scenario: 页面生成步骤的进度日志（V7 增强）
- **WHEN** Wiki 生成完成一个页面
- **THEN** 系统输出日志：`[WikiTask] 进度: 8/52 页 | Token: 125K 48K | 缓存: 32% | 成本: 0.42 | 页面: 核心服务架构 | LLM: openai/gpt-4o | 耗时: 12.3s`
- **AND** 日志级别为 `Information`，包含 Token 输入/输出/缓存命中统计

#### Scenario: 深度理解阶段日志
- **WHEN** 深度代码理解阶段完成
- **THEN** 系统输出：`[WikiTask] 深度理解完成 | 调用图: 234 条边 | 设计模式: 5 个 | 依赖拓扑: 8 模块 | LLM 辅助: 1 次调用 | Token: 8K 3K`

#### Scenario: 交叉引用编织阶段日志
- **WHEN** 交叉引用编织完成
- **THEN** 系统输出：`[WikiTask] 交叉引用编织完成 | 插入链接: 89 个 | 符号追踪: 34 个 | 术语引用: 12 个`

#### Scenario: 任务完成的汇总日志（V7 增强）
- **WHEN** Wiki 生成任务全部完成
- **THEN** 系统输出：`[WikiTask] 生成完成 | 总页数: 52 | 深度: 4 层 | 总耗时: 45m12s | LLM 调用: 58 次 | Token: 850K 320K | 缓存命中: 28% | 总成本: 3.42`

### Requirement: LLM 调用详细日志
系统 SHALL 为每次 LLM 调用输出详细的调用日志，包含调用前后的关键信息。V7 中 SHALL 明确记录 Token 消耗、是否来自缓存、计费类型影响的策略选择。

#### Scenario: 调用前日志
- **WHEN** 系统即将发起 LLM 调用
- **THEN** 输出日志：`[LLM] 调用开始 | Stage: page_generation | Provider: openai/gpt-4o | BillingType: TokenPlan | PromptTokens(est): 45K | 策略: 单页独立调用`

#### Scenario: 调用后日志
- **WHEN** LLM 调用完成
- **THEN** 输出日志：`[LLM] 调用完成 | InputTokens: 44832 | OutputTokens: 3241 | CacheHit: 12000 | Latency: 8.3s | Cost: 0.08`

#### Scenario: CodingPlan 合并调用日志
- **WHEN** CodingPlan 模型执行合并调用（3 页合并为 1 次调用）
- **THEN** 输出日志：`[LLM] 合并调用 | Stage: page_generation | Pages: 3 | Provider: ollama/gemma4:e2b | BillingType: CodingPlan | PromptTokens(est): 85K | 策略: 合并调用(3页)`

### Requirement: 日志级别分类展示
系统 SHALL 在控制台输出中对不同严重级别的日志使用视觉区分。V7 中新增 `[LLM]` 前缀用于所有 LLM 调用日志，`[Metrics]` 前缀用于指标汇总日志。

#### Scenario: LLM 日志与业务日志区分
- **WHEN** LLM 调用日志和业务流程日志同时输出
- **THEN** LLM 日志带有 `[LLM]` 前缀，业务流程日志带有 `[WikiTask]` 前缀，指标汇总带有 `[Metrics]` 前缀

#### Scenario: 限频重试警告日志
- **WHEN** LLM 调用触发限频重试
- **THEN** 系统输出 Warning 级别日志：`[LLM]  限频重试 | Provider: openai/gpt-4o | RetryAfter: 30s | Attempt: 2/3`
