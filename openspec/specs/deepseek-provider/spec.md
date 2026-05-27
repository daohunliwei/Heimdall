## Purpose

DeepSeek Provider 集成——通过 OpenAI 兼容协议接入 DeepSeek API，支持非流式与流式调用，包含模型元数据默认配置。
## Requirements
### Requirement: DeepSeek Provider 注册与配置
系统 SHALL 通过 `OpenAiCompatibleClientFactory.CreateDeepSeek` 创建 OpenAI 兼容客户端，指向 `https://api.deepseek.com/v1`，在 `Program.cs` 中通过 `ChatClientBuilder` 管道注册为 Keyed DI 服务（Key = `"deepseek"`）。支持的模型 SHALL 至少包含 `deepseek-v4-pro` 和 `deepseek-v4-flash`。

#### Scenario: 系统启动时注册 DeepSeek Provider
- **WHEN** `generator.json` 中 `providers.deepseek` 配置段存在且包含有效的 `ApiKey`
- **THEN** 系统通过 `OpenAiCompatibleClientFactory` 创建 `OpenAIClient`，经 `ChatClientBuilder` 管道构建后注册到 DI

#### Scenario: 配置缺失时优雅降级
- **WHEN** `generator.json` 中不存在 `providers.deepseek` 配置段
- **THEN** 系统不注册 DeepSeek Provider，不影响其他 Provider 正常运行

### Requirement: DeepSeek Chat Completion 调用
DeepSeek 的 IChatClient 实例 SHALL 兼容 OpenAI Chat Completions 协议，支持非流式 `GetResponseAsync` 和流式 `GetStreamingResponseAsync`。

#### Scenario: 非流式调用
- **WHEN** 通过 DeepSeek 的 IChatClient 调用 `GetResponseAsync`
- **THEN** 返回标准 `ChatResponse`，包含 `Messages`、`Usage`（InputTokenCount/OutputTokenCount）

#### Scenario: 流式调用
- **WHEN** 通过 DeepSeek 的 IChatClient 调用 `GetStreamingResponseAsync`
- **THEN** 返回 `IAsyncEnumerable<ChatResponseUpdate>`，最后一个 chunk 包含 `UsageDetails`

#### Scenario: API 返回错误
- **WHEN** DeepSeek API 返回非 2xx 状态码
- **THEN** 底层 `OpenAIClient` 抛出包含状态码和错误消息的异常

### Requirement: DeepSeek 模型默认元数据
系统 SHALL 为 DeepSeek 模型提供默认元数据：`MaxContextTokens=1048576`（1M）、`MaxOutputTokens=384000`（384K）、`ContextFillRatio=0.85`、`ContextWarningThreshold=0.90`（类默认值）、`SupportsCaching=true`。

#### Scenario: 首次使用 DeepSeek 模型时自动创建元数据
- **WHEN** 系统首次使用 `deepseek-v4-pro` 或 `deepseek-v4-flash` 且数据库中无对应记录
- **THEN** `HeimdallConfigService.InferDefaultMetadata` 使用上述默认值创建元数据

#### Scenario: DeepSeek 大窗口模型填充策略
- **WHEN** 使用 DeepSeek 模型进行 Wiki 页面生成
- **THEN** 系统按照 ContextFillRatio=0.85 计算输入预算约 891K tokens，max_tokens 设置为 MaxOutputTokens=384000
