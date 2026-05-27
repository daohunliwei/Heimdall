## MODIFIED Requirements

### Requirement: AST L2 数据注入页面生成提示词
页面生成提示词 SHALL 为每个从 BM25 检索到的代码块附带 AST L2 层上下文：(1) 所属类名、继承链和实现的接口；(2) 方法签名和修饰符；(3) 调用该方法的其他方法（Callers）；(4) 该方法调用的其他方法（Callees）；(5) Design Role（参与的设计模式角色）。AST 上下文以紧凑的 Markdown 格式置于代码块之前。

#### Scenario: 代码块附带完整 AST 上下文
- **WHEN** 页面生成获取到 `UserService.CreateUser` 方法代码块
- **THEN** 提示词中代码块前出现 AST 上下文块：
  ```
  > **AST Context** | Class: `UserService` (public, extends `BaseService`, implements `IUserService`)
  > Signature: `public async Task<User> CreateUser(string name, string email)`
  > Called by: `AuthController.Register`, `AdminController.BatchCreate`
  > Calls: `IUserRepository.AddAsync`, `IValidator.Validate`
  > Design Role: Strategy Pattern participant
  ```

#### Scenario: AST 上下文可折叠
- **WHEN** 页面包含 10+ 个代码块，每个带 AST 上下文
- **THEN** 提示词中低重要性方法的 AST 上下文折叠为单行（仅保留类名和方法签名），高重要性方法的上下文完整展开

### Requirement: 结构化消息注入
所有 LLM 调用 SHALL 使用结构化 `List<ChatMessage>` 消息列表：(1) `ChatRole.System` 消息包含从 DB 加载的角色设定、输出格式约束和质量自查清单；(2) 第一个 `ChatRole.User` 消息包含 AST 代码上下文；(3) 第二个 `ChatRole.User` 消息（如有）包含页面主题和写作指令。不再将 System 和 User 内容拼接为单字符串。

#### Scenario: 页面生成结构化消息
- **WHEN** 生成"用户认证"Wiki 页面
- **THEN** System 消息 = 角色+格式约束+Markdown 规范（从 DB 模板加载）
- **AND** User[1] 消息 = BM25 检索代码块 + AST L2 上下文
- **AND** User[2] 消息 = 页面标题、ContentDepthLevel、父页面摘要

### Requirement: 质量审查利用 AST 验证
质量评估阶段 SHALL 利用 AST 数据验证生成内容中引用的符号是否真实存在：(1) 检查生成的类名/方法名是否在 AST 符号列表中存在；(2) 不存在的引用扣分并标记"疑似虚构"；(3) 存在但未提供调用上下文的方法标记"可增强"。

#### Scenario: AST 真实性验证
- **WHEN** 质量审查扫描页面内容，发现引用了 `UserService.DeleteUser` 方法
- **THEN** 系统查询 AST 符号列表 → `DeleteUser` 存在 → 通过验证
- **AND** 若发现引用不存在的 `UserService.ArchiveUser` → 扣分 + 标记"疑似虚构"
