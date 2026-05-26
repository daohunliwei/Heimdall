# chat-message-modeling Specification

## Purpose
TBD - created by archiving change align-meai-current-implementation. Update Purpose after archive.
## Requirements
### Requirement: Chat 与 Ask 的多角色消息建模
系统 SHALL 使用 `List<ChatMessage>` 保留 `system / user / assistant / tool` 的角色边界，不得再把历史轮次、证据上下文和当前问题压平为一条“大 Prompt”用户消息。

#### Scenario: Chat 流式接口保留完整对话历史
- **WHEN** 客户端请求 `POST /chat/completions/stream` 并提交多轮消息
- **THEN** 系统按原始顺序保留历史中的 `system`、`user`、`assistant` 消息
- **AND** 若模板系统生成额外系统约束，则作为新的 `System` 消息插入，而不是覆盖已有历史
- **AND** 不再只取最后一条用户消息作为最终 Prompt

#### Scenario: Ask 场景保留历史轮次
- **WHEN** Ask 请求携带历史对话
- **THEN** 系统把历史逐轮转换为 `ChatMessage`
- **AND** 历史中的用户问题与助手回答分别保留为 `User` 与 `Assistant` 消息
- **AND** 不再将历史渲染为 Markdown 列表后塞入单条 `User` 消息

### Requirement: 证据上下文与当前问题分离
系统 SHALL 将版本绑定规则、检索证据和当前问题拆分为职责明确的消息，而不是拼成一个大字符串。

#### Scenario: 版本规则作为系统约束
- **WHEN** Ask 或 Wiki 相关问答需要绑定到特定 `RepositoryVersion` 与 `WikiVersion`
- **THEN** 系统使用 `System` 消息声明版本约束、回答语言和证据使用规则
- **AND** 该消息不与用户问题正文混写

#### Scenario: 当前版本证据单独成消息
- **WHEN** 系统需要注入页面片段、工件摘要或文件焦点信息
- **THEN** 系统将其作为独立的补充上下文消息注入
- **AND** 当前用户问题仍作为单独一条最新 `User` 消息发送

### Requirement: Tool Call 产生的消息由 MEAI 负责回写
系统 SHALL 让 `FunctionInvokingChatClient` 负责在消息历史中写入 `Tool` 相关内容。业务层只负责提供工具列表与初始消息，不手写 Tool 往返消息。

#### Scenario: Tool 角色消息自动加入历史
- **WHEN** 模型在一次请求中调用工具
- **THEN** `FunctionInvokingChatClient` 自动追加 `FunctionCallContent` 与 `FunctionResultContent`
- **AND** 业务层无需自行把工具结果拼接回用户 Prompt

