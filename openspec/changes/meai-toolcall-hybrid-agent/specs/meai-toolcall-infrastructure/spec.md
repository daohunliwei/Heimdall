## ADDED Requirements

### Requirement: TaskLlmService Tool Call 重载
系统 SHALL 在 `TaskLlmService` 中新增 `GenerateWithToolsAsync` 方法，接收 `IEnumerable<AIFunction>` 参数，封装 MEAI `ChatOptions.Tools` 的 Tool Call 往返逻辑。方法 SHALL 支持最大 5 轮工具调用往返，超出后 SHALL 回退到无工具模式并记录 Warning 日志。

#### Scenario: 单轮 Tool Call 成功
- **WHEN** 调用 `GenerateWithToolsAsync` 传入 `ReadCodeFile` 工具，LLM 决定调用工具获取 `UserService.cs` 内容
- **THEN** 系统执行第一轮 LLM 调用，解析 `ChatMessage` 中的 `FunctionCallContent`
- **AND** 系统调用 `ReadCodeFile("UserService.cs")` 获取文件内容
- **AND** 系统将工具返回结果包装为 `FunctionResultContent` 追加到消息历史
- **AND** 系统执行第二轮 LLM 调用，LLM 基于工具返回内容生成最终响应
- **AND** 返回 `ChatResponse`，总计 2 轮往返

#### Scenario: 多轮 Tool Call 成功
- **WHEN** LLM 先调用 `SearchSymbols` 获取符号列表，再调用 `ReadCodeFile` 读取具体文件
- **THEN** 系统执行 3 轮往返（第1轮触发搜索 → 第2轮触发读取 → 第3轮生成响应）
- **AND** 每轮都正确记录到 `TaskLlmCallLog`

#### Scenario: 超出最大轮数回退
- **WHEN** LLM 在第 5 轮工具调用往返后仍要求调用工具
- **THEN** 系统忽略工具调用请求，将当前消息历史截断后继续生成
- **AND** 记录 Warning 日志：`Tool call max rounds (5) exceeded, falling back to no-tool mode`

#### Scenario: 工具执行异常处理
- **WHEN** 某个工具方法抛出异常（如文件不存在）
- **THEN** 系统捕获异常，将异常信息作为 `FunctionResultContent` 返回给 LLM
- **AND** LLM 可以基于异常信息调整策略（如换一个文件路径）
- **AND** 不中断整个 Tool Call 往返循环

#### Scenario: Provider 不支持 Tool Call 降级
- **WHEN** `IChatClient` 在接收到包含 `Tools` 的 `ChatOptions` 时抛出 `NotSupportedException` 或返回错误
- **THEN** 系统降级为无工具的 `GenerateTextAsync` 调用
- **AND** 记录 Warning 日志：`Provider does not support tool calls, falling back to standard generation`

### Requirement: 工具调用可观测性
系统 SHALL 在每次 Tool Call 往返中记录结构化日志，包含：工具名称、调用参数（脱敏后）、返回结果长度、往返耗时（毫秒）、当前轮数。

#### Scenario: 工具调用日志记录
- **WHEN** LLM 调用 `ReadCodeFile("/src/UserService.cs")` 且工具返回 150 行代码
- **THEN** 日志记录：`Tool=ReadCodeFile, Args={filePath:/src/UserService.cs}, ResultLength=150lines, RoundMs=320, Round=1/5`
- **AND** 日志关联到当前 `TaskLlmCallLog.ToolCallLogs` 集合

### Requirement: 配置开关控制
系统 SHALL 通过 `SystemSetting` 表提供三个 Tool Call 配置项：`ToolCall.Enabled`（全局开关，默认 `false`）、`ToolCall.Stage3.Enabled`（Stage 3 开关，默认 `false`）、`ToolCall.Stage5.Enabled`（Stage 5 开关，默认 `false`）。三个开关均为 `AND` 关系，全局开关优先。

#### Scenario: 全局开关关闭
- **WHEN** `ToolCall.Enabled` 为 `false`
- **THEN** 即使 `ToolCall.Stage5.Enabled` 为 `true`，Stage 5 也不启用 Tool Call
- **AND** 所有 `GenerateWithToolsAsync` 调用降级为 `GenerateTextAsync`

#### Scenario: 阶段级开关独立控制
- **WHEN** `ToolCall.Enabled` 为 `true`，`ToolCall.Stage3.Enabled` 为 `true`，`ToolCall.Stage5.Enabled` 为 `false`
- **THEN** Stage 3 使用 Tool Call 增强，Stage 5 使用传统模式
