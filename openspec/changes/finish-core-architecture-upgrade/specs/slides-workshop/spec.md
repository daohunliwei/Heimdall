## MODIFIED Requirements

### Requirement: Slides 和 Workshop 接入 AST 上下文 + DB 提示词 + 结构化消息
SlidesTaskService 和 WorkshopTaskService SHALL：(1) 提示词从 DB 通过 `IPromptMergeService` 加载；(2) LLM 调用使用结构化 `List<ChatMessage>`（System/User 分离）；(3) 上下文构建注入 AST L2 层数据——每个相关代码块附带 AST 上下文（类关系、方法签名、调用拓扑）。

#### Scenario: Slides 生成的 AST 增强上下文
- **WHEN** SlidesTaskService 生成关于 `UserService` 的演示页
- **THEN** System 消息 = DB 加载的 Slides 角色模板；User 消息包含 AST L2 上下文描述（"UserService 是核心服务类，被 3 个 Controller 调用"）+ 代码检索片段

#### Scenario: Workshop 使用结构化消息
- **WHEN** WorkshopTaskService 生成训练营材料
- **THEN** 每个模块的 LLM 调用使用 `[System, User(context), User(topic)]` 三元组消息结构
