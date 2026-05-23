## ADDED Requirements

### Requirement: 基于 IChatClient.GetStreamingResponseAsync 的真流式输出
系统 SHALL 使用 `IChatClient.GetStreamingResponseAsync()` 的 `IAsyncEnumerable<ChatResponseUpdate>` 实现真逐 Token 流式输出，废弃当前的"先生成后分块"假流式方案。

#### Scenario: Provider 层真流式调用
- **WHEN** 系统需要流式 LLM 响应
- **THEN** 调用 `await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, ct))`
- **AND** 每个 `ChatResponseUpdate` 携带增量文本（`update.Text`）和可选的 `FinishReason`、`Usage`
- **AND** 调用方将每段增量文本以 SSE 格式发送给客户端

#### Scenario: 流式请求取消处理
- **WHEN** 客户端断开 SSE 连接（CancellationToken 触发）
- **THEN** 服务器取消 `IAsyncEnumerable` 枚举，释放底层 HTTP 连接

### Requirement: Chat 控制器 MEAI 流式 SSE 响应
`ChatController` 的 `POST /chat/completions/stream` 接口 SHALL 改为基于 `IChatClient.GetStreamingResponseAsync()` 的真流式方案。

#### Scenario: Chat SSE 流式响应
- **WHEN** 客户端请求 `POST /chat/completions/stream`
- **THEN** 服务器设置 `Content-Type: text/event-stream`
- **AND** 通过 `await foreach` 消费 `IChatClient.GetStreamingResponseAsync()` 的 `ChatResponseUpdate` 流
- **AND** 每个 update 以 SSE 格式 `data: {"content": "<增量文本>"}\n\n` 发送

#### Scenario: Chat 流式结束信号
- **WHEN** 流式响应全部完成
- **THEN** 服务器发送 `event: done\ndata: [DONE]\n\n`

### Requirement: Ask 流式端点
系统 SHALL 新增 `POST /tasks/ask/stream` 端点，基于 `IChatClient.GetStreamingResponseAsync()` 提供 Ask 功能的 SSE 流式响应。

#### Scenario: Ask 流式端点到请求
- **WHEN** 客户端请求 `POST /tasks/ask/stream` 并传入 `AskRequest`
- **THEN** 服务器以 SSE 流式返回 Ask 回答内容的每个增量 chunk

#### Scenario: 用户取消流式 Ask
- **WHEN** 客户端在流式响应中断开连接
- **THEN** 系统取消 LLM 调用 Token，保留已生成内容并更新任务状态

### Requirement: Provider 无流式支持时回退到非流式
对于无法原生支持流式的 Provider（通过 `ChatOptions.AdditionalProperties["SupportsStreaming"] == false` 标记），系统 SHALL 自动回退。

#### Scenario: 流式不支持时回退
- **WHEN** 某 Provider 不支持流式输出
- **THEN** 系统降级调用 `GetResponseAsync()` 非流式方法
- **AND** 将完整结果包装为单次 `ChatResponseUpdate` yield 返回
- **AND** 记录 Warning 级别日志

### Requirement: 前端 Ask 页面 SSE 流式渲染
Ask 页面 SHALL 支持 SSE 流式读取，使用 `EventSource` 或 `fetch` + `ReadableStream` 逐 chunk 追加渲染 Markdown 内容。

#### Scenario: 流式 Markdown 渲染
- **WHEN** 用户提问并触发流式 Ask
- **THEN** 前端 SSE 读取每个 chunk 的内容
- **AND** 实时追加渲染为 Markdown（不等待全部完成）
