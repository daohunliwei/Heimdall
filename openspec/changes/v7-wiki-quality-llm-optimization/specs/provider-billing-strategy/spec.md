## ADDED Requirements

### Requirement: Provider 计费模型元数据
系统 SHALL 为每个 Provider/Model 组合维护计费模型元数据，包含：`BillingType`（CodingPlan 按调用次数收费 / TokenPlan 按 Token 量收费）、`MaxContextTokens`（模型上下文窗口大小）、`MaxOutputTokens`（最大输出 Token 数）、`RateLimitPerMinute`（速率限制）、`InputTokenPrice`（输入 Token 价格/百万）、`OutputTokenPrice`（输出 Token 价格/百万）、`CallPrice`（单次调用价格，仅 CodingPlan）、`SupportsCaching`（是否支持 prompt 缓存）。

#### Scenario: Ollama 本地模型标注为 CodingPlan
- **WHEN** 配置 Ollama Provider 的 `gemma4:e2b` 模型
- **THEN** 元数据中 BillingType=CodingPlan，CallPrice=0（本地免费但受限于并发），MaxContextTokens=131072，RateLimitPerMinute=5

#### Scenario: OpenAI 模型标注为 TokenPlan
- **WHEN** 配置 OpenAI Provider 的 `gpt-4o` 模型
- **THEN** 元数据中 BillingType=TokenPlan，InputTokenPrice=2.50，OutputTokenPrice=10.00，MaxContextTokens=128000，SupportsCaching=true

#### Scenario: 元数据 API 查询
- **WHEN** 前端请求 `GET /api/providers/metadata`
- **THEN** 系统返回所有已配置 Provider 的模型元数据列表，包含 BillingType、MaxContextTokens 等字段

#### Scenario: 自定义模型元数据配置
- **WHEN** 用户通过 `config/generator.json` 为自定义 API 端点配置元数据
- **THEN** 系统从配置文件加载元数据并在运行时供策略引擎使用

### Requirement: CodingPlan 调用策略
当模型 BillingType 为 CodingPlan 时，系统 SHALL 采用"合并调用、填满上下文"策略：将多个独立的 LLM 调用请求合并为单次调用，单次调用的 prompt 内容 SHALL 填充至 MaxContextTokens 的 60%-70%（可通过环境变量 `HEIMDALL_CONTEXT_FILL_RATIO` 配置）。

#### Scenario: 多页面合并为单次调用
- **WHEN** 使用 CodingPlan 模型生成 Wiki 页面，当前批次有 5 个页面待生成
- **THEN** 系统计算每页 prompt 的 Token 数，将可以装入 60-70% 上下文窗口的页面合并为一次调用（如 3 页合并为 1 次调用），输出要求为 JSON 数组
- **AND** 剩余 2 页归入下一次合并调用

#### Scenario: 合并上限控制
- **WHEN** CodingPlan 模型单次调用尝试合并超过 3 个页面
- **THEN** 系统 SHALL 限制单次合并不超过 3 页，即使上下文窗口仍有空间，以确保每页输出质量

#### Scenario: 合并后质量检测回退
- **WHEN** 合并调用产生的某页内容质量评分低于阈值（60 分）
- **THEN** 系统在质量审查阶段将该页标记为需重生成，重生成时使用单页独立调用

### Requirement: TokenPlan 调用策略
当模型 BillingType 为 TokenPlan 时，系统 SHALL 采用"单页调用、最大化上下文利用"策略：每页独立调用但 prompt 中代码片段检索量 SHALL 填充至 MaxContextTokens 的 60-70%，以提升每次调用的信息密度和输出质量。

#### Scenario: TokenPlan 模型上下文填充
- **WHEN** 使用 TokenPlan 模型（如 GPT-4o）生成页面，模型 MaxContextTokens=128000
- **THEN** 系统为该页检索代码片段直至 prompt 总 Token 数达到 128000 * 0.65 约 83200 tokens
- **AND** 代码片段按相关性排序填充，低相关性片段不强行填入

#### Scenario: TokenPlan 429 限频退避
- **WHEN** TokenPlan 模型返回 HTTP 429 Too Many Requests
- **THEN** 系统执行指数退避重试（初始 2s，最大 60s，最多 5 次），期间不阻塞其他非限频的调用

### Requirement: 统一限频退避与重试机制
系统 SHALL 为所有 Provider 提供统一的速率限制管理和重试机制，基于 ProviderModelMetadata 中的 RateLimitPerMinute 进行主动限流（令牌桶算法），避免触发 Provider 侧 429 错误。

#### Scenario: 主动限流
- **WHEN** Provider 配置 RateLimitPerMinute=10 且 1 分钟内已调用 9 次
- **THEN** 第 10 次调用正常执行，第 11 次调用被限流队列延迟至下一分钟窗口

#### Scenario: 限流队列超时
- **WHEN** 调用在限流队列中等待超过 120 秒
- **THEN** 系统抛出 `RateLimitTimeoutException` 并记录告警日志

#### Scenario: 被动 429 重试
- **WHEN** Provider 返回 429 且响应头包含 `Retry-After: 30`
- **THEN** 系统等待 30 秒后重试，最多重试 3 次

### Requirement: 上下文窗口智能填充引擎
系统 SHALL 提供 `IContextPackingService` 接口，根据模型的 MaxContextTokens 和 ContextFillRatio 动态分配 prompt 各部分的 Token 预算。预算分配策略 SHALL 为：系统提示词（固定 ~2000 tokens） 页面元数据（固定 ~1500 tokens） 代码片段（动态填充至预算上限） 跨页面上下文（弹性空间）。

#### Scenario: 大窗口模型预算分配
- **WHEN** 模型 MaxContextTokens=128000，ContextFillRatio=0.65
- **THEN** 总预算 83200 tokens，系统提示词 2000 + 页面元数据 1500 + 代码片段可用空间约 75000 tokens + 跨页面上下文 4700 tokens

#### Scenario: 小窗口模型预算分配
- **WHEN** 模型 MaxContextTokens=8192，ContextFillRatio=0.65
- **THEN** 总预算 5325 tokens，系统提示词压缩至 1000 + 页面元数据 800 + 代码片段 3000 tokens + 跨页面上下文 525 tokens

#### Scenario: Token 计数方法
- **WHEN** 系统需要估算文本 Token 数量
- **THEN** 使用 tiktoken cl100k_base 编码器（或兼容的 .NET 实现）进行 Token 计数，精度误差 SHALL  5%
