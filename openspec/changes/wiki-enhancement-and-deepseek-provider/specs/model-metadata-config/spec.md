## MODIFIED Requirements

### Requirement: 上下文窗口警戒阈值
每个模型 SHALL 支持配置 ContextWarningThreshold（默认 0.90）。当单次调用的 prompt Token 数超过 `MaxContextTokens * ContextWarningThreshold` 时，系统 SHALL 输出警告日志，并自动截断低优先级内容（如跨页面上下文）。

**变更**：警告阈值计算 SHALL 仅基于 `MaxContextTokens`（输入上下文窗口），不再与 `MaxOutputTokens` 混淆。当 prompt 估算 Token 超过阈值时，截断逻辑 SHALL 按优先级递减顺序裁剪：跨页面上下文 → 仓库文档片段 → 低相关性代码片段 → 基础提示词模板。

#### Scenario: 预警触发
- **WHEN** 模型 MaxContextTokens=128000，ContextWarningThreshold=0.90，某次调用 prompt 估算为 120000 tokens (>115200)
- **THEN** 系统输出 Warning 日志并依次裁剪：先尝试截断跨页面上下文，若仍超阈值则截断仓库文档片段

#### Scenario: 正常调用不触发
- **WHEN** 模型 MaxContextTokens=204800，ContextWarningThreshold=0.90，prompt 估算为 100000 tokens (<184320)
- **THEN** 系统正常执行，不截断任何内容

### Requirement: 按模型动态获取上下文预算
系统 SHALL 在页面生成阶段根据当前使用的模型的 MaxContextTokens 和 ContextFillRatio 动态计算允许的代码片段检索量，而非使用固定值。

**变更**：预算计算 SHALL 同时考虑输入和输出分离。输入预算 = `MaxContextTokens * ContextFillRatio`（控制 prompt 大小），输出上限 = `MaxOutputTokens`（控制 Provider API 的 max_tokens 参数）。二者独立计算，互不影响。

#### Scenario: 大窗口模型最大化检索
- **WHEN** 使用 MiniMax-M2.7（MaxContextTokens=204800, ContextFillRatio=0.65）
- **THEN** 代码片段可填充至约 204800 * 0.65 ≈ 133120 tokens 的输入预算上限
- **AND** Provider API 调用时 `max_tokens` 设置为模型的 MaxOutputTokens 值

#### Scenario: 小窗口模型适度检索
- **WHEN** 使用 Ollama gemma4:e2b（典型 MaxContextTokens=8192 tokens, MaxOutputTokens=4096）
- **THEN** 代码片段预算自动缩减至约 5325 tokens，优先保留高相关性片段
- **AND** Provider API 调用时 `max_tokens` 设置为 4096

#### Scenario: DeepSeek 超大窗口模型
- **WHEN** 使用 DeepSeek deepseek-v4-pro（MaxContextTokens=1048576, ContextFillRatio=0.85, MaxOutputTokens=384000）
- **THEN** 代码片段输入预算约为 891K tokens，系统尽可能填充高价值代码和文档内容
- **AND** Provider API 调用时 `max_tokens` 设置为 384000

## ADDED Requirements

### Requirement: 模型输出长度独立配置
系统 SHALL 在调用 LLM Provider 时，将模型的 MaxOutputTokens 作为 max_tokens 参数独立传递给 Provider API。该值 SHALL 不受 MaxContextTokens 或 ContextFillRatio 的影响，仅受模型元数据记录中 MaxOutputTokens 字段控制。

#### Scenario: 页面生成使用模型的 MaxOutputTokens
- **WHEN** 系统为某 Wiki 页面生成调用 LLM，当前模型的 MaxOutputTokens=384000
- **THEN** 请求体中的 `max_tokens` 参数设置为 384000
- **AND** 不使用 MaxContextTokens 或任何比例计算值作为输出限制

#### Scenario: 管理员更新 MaxOutputTokens 后即时生效
- **WHEN** 管理员通过 API 将某模型的 MaxOutputTokens 从 8192 更新为 16384
- **THEN** 下一次 LLM 调用立即使用新的 max_tokens=16384，无需重启服务
