## Purpose

使用 MEAI `FunctionInvokingChatClient` 中间件实现 LLM Tool Call 功能，并集成 `ToolCallConfigurationService` 统一管理阶段开关与配置。
## Requirements
### Requirement: FunctionInvokingChatClient 集成
系统 SHALL 在所有 Provider 的 `ChatClientBuilder` 管道中通过 `UseFunctionInvocation()` 注册 `FunctionInvokingChatClient` 中间件。该中间件 SHALL 自动处理 Tool Call 往返逻辑：解析 LLM 响应中的 `FunctionCallContent` → 执行对应 `AIFunction` → 包装结果 → 追加消息历史 → 自动循环调用直到 LLM 返回最终文本响应或达到最大轮数限制。

#### Scenario: 自动处理单轮 Tool Call
- **WHEN** LLM 返回的 `ChatResponse` 包含一个 `FunctionCallContent`（要求调用 `ReadCodeFile("/src/UserService.cs")`）
- **THEN** `FunctionInvokingChatClient` 自动调用 `ReadCodeFile` 工具
- **AND** 将工具返回的代码文本包装为 `FunctionResultContent`
- **AND** 将结果追加到消息历史
- **AND** 自动发起下一轮 `GetResponseAsync`，将工具结果发送给 LLM
- **AND** LLM 基于工具结果生成最终文本响应
- **AND** 返回调用方的是最终文本响应（中间 Tool Call 过程对调用方透明）

#### Scenario: 自动处理多轮 Tool Call
- **WHEN** LLM 先调用 `SearchSymbols` 搜索符号，根据搜索结果再调用 `ReadCodeFile` 读取具体文件
- **THEN** `FunctionInvokingChatClient` 自动执行 2-3 轮往返
- **AND** 调用方只需调用一次 `GetResponseAsync`，获得最终结果

#### Scenario: 达到最大轮数限制
- **WHEN** LLM 在 `MaximumIterationsPerRequest`（默认 5 轮）后仍要求调用工具
- **THEN** `FunctionInvokingChatClient` 抛出 `InvalidOperationException` 或返回最后一条响应
- **AND** 系统记录 Warning 日志

#### Scenario: 工具执行异常
- **WHEN** 某个 AIFunction 执行时抛出异常（如文件不存在）
- **THEN** `FunctionInvokingChatClient` 将异常信息作为 `FunctionResultContent` 的 Error 属性返回给 LLM
- **AND** LLM 可基于错误信息调整策略（如换一个文件路径）
- **AND** 不中断整个往返循环

### Requirement: TailoredFunctionInvokingChatClient 自定义配置
系统 SHALL 通过 `UseFunctionInvocation(configure: o => { ... })` 配置以下参数：`MaximumIterationsPerRequest` 设为 8（Stage 5 页面生成复杂场景需要更多轮）、`AllowConcurrentInvocation` 设为 `true`（允许并发工具调用）、`TerminateOnUnknownCalls` 设为 `true`（严格模式，未知函数调用立即终止）、`MaximumConsecutiveErrorsPerRequest` 设为 5。

#### Scenario: 自定义最大轮数
- **WHEN** Stage 5 页面生成，LLM 连续调用 6 轮工具
- **THEN** `MaximumIterationsPerRequest = 8` 允许继续执行
- **AND** 不触发轮数超限

#### Scenario: 并发工具调用
- **WHEN** LLM 单轮请求同时调用多个工具（如同时搜索多个符号）
- **THEN** `AllowConcurrentInvocation = true` 允许并发执行
- **AND** 不阻塞独立工具调用

#### Scenario: 严格模式终止未知调用
- **WHEN** LLM 请求调用未注册的工具函数
- **THEN** `TerminateOnUnknownCalls = true` 立即终止该轮请求
- **AND** 避免 LLM 反复尝试调用不存在的工具

### Requirement: ChatOptions.Tools 注入
系统 SHALL 继续通过 `ChatOptions.Tools` 注入 Tool Call 所需的 `AIFunction` 列表，但 Tool Call 的配置读取、默认值处理和阶段开关判定 SHALL 统一通过 `ToolCallConfigurationService` 完成。

#### Scenario: Stage 3 开关统一由配置服务判定
- **WHEN** `WikiTaskService` 需要判断 Stage 3 是否开启 Tool Call
- **THEN** 系统调用 `ToolCallConfigurationService` 的语义化接口获取结果
- **AND** 不直接在 `WikiTaskService` 中读取 `ToolCall.Stage3.Enabled` 键值

#### Scenario: Stage 5 开关统一由配置服务判定
- **WHEN** `WikiTaskService` 需要判断 Stage 5 是否开启 Tool Call
- **THEN** 系统调用 `ToolCallConfigurationService` 的语义化接口获取结果
- **AND** `ToolCall.Enabled` 的全局总开关由同一服务一并处理

### Requirement: Tool Call 日志追踪
系统 SHALL 在 `TaskLlmCallLog` 中通过 `ToolCallLogsJson` 字段（JSON 字符串）记录每次 LLM 调用的 Tool Call 详情。系统 SHALL 从 `ChatResponse.Messages` 中提取 `FunctionCallContent`（工具名、参数脱敏、轮次）和 `FunctionResultContent`（调用 ID、返回长度、异常状态），序列化为 JSON 后持久化。

#### Scenario: Tool Call 日志持久化
- **WHEN** Stage 5 页面生成，LLM 调用了 2 次工具（`SearchSymbols` + `ReadCodeFile`）
- **THEN** `TaskLlmCallLog.ToolCallLogsJson` 包含 JSON 数组，记录每次工具调用和结果
- **AND** 每条 FunctionCall 记录包含 `ToolName`、`Arguments`（超过 500 字符自动截断）、`Round`、`CallId`
- **AND** 每条 FunctionResult 记录包含 `ResultLength`、`HasError`、`CallId`（通过 CallId 关联调用与结果）

### Requirement: Tool Call 配置的唯一事实来源
系统 SHALL 以 `ToolCallConfigurationService` 作为唯一的 Tool Call 配置事实来源。任何新入口若需要 Tool Call 阶段控制，也必须复用该服务，而不是自行读取 `SystemSetting`。

#### Scenario: 配置读取失败时统一降级
- **WHEN** `SystemSetting` 读取失败、缺失或格式非法
- **THEN** `ToolCallConfigurationService` 统一返回”全部关闭”或等价安全降级结果
- **AND** 调用侧不再自行实现降级逻辑

#### Scenario: 后续新入口复用同一配置口
- **WHEN** 未来 `Ask`、`Chat` 或其他任务入口需要增加 Tool Call 开关
- **THEN** 直接扩展 `ToolCallConfigurationService`
- **AND** 不新增第二套配置读取实现

