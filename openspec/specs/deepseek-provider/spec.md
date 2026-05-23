## ADDED Requirements

### Requirement: DeepSeek Provider 注册与配置
系统 SHALL 支持将 DeepSeek 作为新的 Chat Provider 注册到 ProviderRegistry，配置信息来源于 `generator.json` 的 `providers.deepseek` 节点。支持的模型 SHALL 至少包含 `deepseek-v4-pro` 和 `deepseek-v4-flash`。

#### Scenario: 系统启动时注册 DeepSeek Provider
- **WHEN** `generator.json` 中 `providers.deepseek` 配置段存在且包含有效的 `ApiKey` 和 `ApiBase`
- **THEN** 系统自动注册 `DeepSeekChatProvider` 到 ProviderRegistry，ProviderId 为 `deepseek`
- **AND** Provider 在 DI 容器中正确解析

#### Scenario: 配置缺失时优雅降级
- **WHEN** `generator.json` 中不存在 `providers.deepseek` 配置段
- **THEN** 系统不注册 DeepSeek Provider，不影响其他 Provider 正常运行
- **AND** 启动日志输出 Warning 级别提示

### Requirement: DeepSeek Chat Completion 非流式调用
DeepSeekChatProvider SHALL 兼容 OpenAI Chat Completions 协议，实现非流式 `GenerateAsync` 方法。请求体中 SHALL 包含 `thinking` 配置节点（`type: "enabled"`），`max_tokens` SHALL 根据模型的 `MaxOutputTokens` 元数据设置。

#### Scenario: 非流式调用成功返回
- **WHEN** 调用 `DeepSeekChatProvider.GenerateAsync` 传入有效的 messages 和模型名
- **THEN** Provider 向 `https://api.deepseek.com/chat/completions` 发送 POST 请求
- **AND** 请求体包含 `"thinking": {"type": "enabled"}` 和 `"max_tokens": <MaxOutputTokens>`
- **AND** 返回 JSON 响应中 `choices[0].message.content` 作为生成文本
- **AND** `choices[0].message.reasoning_content` 记录到调试日志

#### Scenario: API 返回错误
- **WHEN** DeepSeek API 返回非 2xx 状态码
- **THEN** Provider 抛出包含状态码和错误消息的异常，调用方通过重试策略处理

### Requirement: DeepSeek Chat Completion 流式调用
DeepSeekChatProvider SHALL 实现 `GenerateWithMetricsAsync` 方法，支持 SSE 流式响应。流式解析 SHALL 同时处理 `delta.content`（生成内容）和 `delta.reasoning_content`（推理过程），最终返回合并后的完整内容和 Token 用量。

#### Scenario: 流式调用收集完整内容
- **WHEN** 调用 `GenerateWithMetricsAsync` 且 `request.Stream = true`
- **THEN** Provider 逐步读取 SSE 事件流，拼接所有 `delta.content` 片段为完整回答文本
- **AND** 收集所有 `delta.reasoning_content` 片段为完整推理过程
- **AND** 解析最后一条 chunk 的 `usage` 字段获取实际 Token 用量

#### Scenario: 流式调用处理 DONE 标记
- **WHEN** SSE 流收到 `data: [DONE]` 行
- **THEN** Provider 停止读取流，返回已拼接的完整内容
- **AND** 若之前未获取到 `usage`，使用 TokenCounter 估算用量

#### Scenario: 流式调用时 reasoning_content 为空
- **WHEN** 模型不需要推理或 thinking 配置为 disabled
- **THEN** `delta.reasoning_content` 为 null 或空，Provider 正常处理仅收集 `delta.content`

### Requirement: DeepSeek 模型默认元数据
系统 SHALL 为 DeepSeek 模型提供默认元数据，包含：`MaxContextTokens=1048576`（1M）、`MaxOutputTokens=384000`（384K）、`ContextFillRatio=0.85`、`ContextWarningThreshold=0.90`、`SupportsCaching=true`。

#### Scenario: 首次使用 DeepSeek 模型时自动创建元数据
- **WHEN** 系统首次使用 `deepseek-v4-pro` 或 `deepseek-v4-flash` 模型且数据库中无对应记录
- **THEN** 系统回退到 generator.json 中定义的默认值，行为与其他 Provider 一致

#### Scenario: DeepSeek 大窗口模型填充策略
- **WHEN** 使用 DeepSeek 模型进行 Wiki 页面生成且 MaxContextTokens=1048576
- **THEN** 系统按照 ContextFillRatio=0.85 计算输入预算为约 891K tokens
- **AND** max_tokens 参数设置为 MaxOutputTokens=384000
