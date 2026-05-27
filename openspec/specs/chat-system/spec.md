## Purpose

Chat 与 Ask 对话系统——涵盖真流式 SSE 输出、多角色消息建模（system/user/assistant/tool）、证据上下文分离及前端流式渲染。
## Requirements
### Requirement: 基于 IChatClient.GetStreamingResponseAsync 的真流式输出
系统 SHALL 使用 IChatClient.GetStreamingResponseAsync() 的 IAsyncEnumerable<ChatResponseUpdate> 实现真逐 Token 流式输出，废弃"先生成后分块"假流式方案。

#### Scenario: Provider 层真流式调用
- **WHEN** 系统需要流式 LLM 响应
- **THEN** 调用 `await foreach (var update in chatClient.GetStreamingResponseAsync(...))`
- **AND** 每个 ChatResponseUpdate 携带增量文本，以 SSE 格式发送给客户端

#### Scenario: 流式请求取消处理
- **WHEN** 客户端断开 SSE 连接（CancellationToken 触发）
- **THEN** 服务器取消 IAsyncEnumerable 枚举，释放底层 HTTP 连接

### Requirement: Chat 与 Ask 的多角色消息建模
系统 SHALL 使用 List<ChatMessage> 保留 system/user/assistant/tool 的角色边界，不得再把历史轮次、证据上下文和当前问题压平为一条"大 Prompt"用户消息。

#### Scenario: Chat 流式接口保留完整对话历史
- **WHEN** 客户端请求 POST /chat/completions/stream 并提交多轮消息
- **THEN** 系统按原始顺序保留历史中的 system、user、assistant 消息

#### Scenario: 证据上下文与当前问题分离
- **WHEN** Ask 或 Wiki 需要绑定到特定 RepositoryVersion 与 WikiVersion
- **THEN** 系统使用 System 消息声明版本约束，不与用户问题正文混写

#### Scenario: Tool Call 产生的消息由 MEAI 负责回写
- **WHEN** 模型调用工具
- **THEN** FunctionInvokingChatClient 自动追加 FunctionCallContent 与 FunctionResultContent，业务层无需手写 Tool 往返消息

### Requirement: Chat 与 Ask SSE 流式端点
ChatController 的 POST /chat/completions/stream 和 Ask 的 POST /tasks/ask/stream SHALL 基于 IChatClient.GetStreamingResponseAsync() 提供 SSE 流式响应。

#### Scenario: Chat SSE 流式响应
- **WHEN** 客户端请求流式端点
- **THEN** 服务器设置 Content-Type: text/event-stream，通过 await foreach 消费 ChatResponseUpdate 流
- **AND** 每个 update 以 SSE 格式发送，完成后发送 event: done

#### Scenario: 用户取消流式 Ask
- **WHEN** 客户端在流式响应中断开连接
- **THEN** 系统取消 LLM 调用 Token，保留已生成内容

### Requirement: 前端 SSE 流式渲染
Ask 页面 SHALL 支持 SSE 流式读取，使用 fetch + ReadableStream 逐 chunk 追加渲染 Markdown 内容。

#### Scenario: 流式 Markdown 渲染
- **WHEN** 用户提问并触发流式 Ask
- **THEN** 前端 SSE 读取每个 chunk 的内容，实时追加渲染为 Markdown
