## Context

当前 Wiki 管线（`WikiTaskService` + `TaskPromptService`）存在三个架构半迁移问题：

1. **AST 半迁移**：Tree-sitter 已集成用于符号提取，但 `CallGraphBuilder`（调用图）和 `DesignPatternDetector`（设计模式）仍使用纯正则实现。Tree-sitter 迁移设计文档明确选择了"放弃 Roslyn 语义分析"，但未提供 AST 替代方案。
2. **提示词双轨制**：`PromptSeedData` → DB → `IPromptMergeService` → `ChatController` 是一条完成的 DB 驱动链路，但 `WikiTaskService`、`SlidesTaskService`、`WorkshopTaskService` 通过 `TaskPromptService`（~500 行硬编码模板）独立运行。两套系统互不交互。
3. **消息扁平化**：`ChatMessageBuilderService` 为 Chat/Ask 路径构建了正确的 `List<ChatMessage>`（System/User 分离），但 Wiki/Slides/Workshop 管线将所有内容拼接为单字符串，角色信息以 Markdown 标题（`## 角色`）嵌入文本。

**约束**：不引入 Roslyn（已被移除，无 `Microsoft.CodeAnalysis` 依赖）；不新增外部 NuGet 包；不修改前端。

## Goals / Non-Goals

**Goals:**
- `CallGraphBuilder` 和 `DesignPatternDetector` 全部替换为基于 Tree-sitter AST 的实现
- 所有管线（Wiki/Slides/Workshop）的提示词统一从 DB 通过 `IPromptMergeService` 加载
- 所有 LLM 调用使用结构化 `List<ChatMessage>` + `ChatOptions`，System/User 角色分离
- 删除所有硬编码提示词和正则调用提取代码
- 6 个受影响的 spec 同步更新

**Non-Goals:**
- 不引入 Roslyn SemanticModel（超出 tree-sitter 能力的方法级调用关系使用 tree-sitter 调用表达式节点 + 符号名匹配）
- 不改变 Chat/Ask 路径（已正确实现）
- 不改变前端
- 不新增数据库表结构（复用 `prompt_templates` 表）

## Decisions

### Decision 1: Tree-sitter AST 调用图方案（替代正则）

**选择**：扩展 `TreeSitterAnalyzer`，新增 `ExtractCallEdges` 方法。对 tree-sitter 语法树中的 `invocation_expression`（C#）、`call_expression`（TypeScript/JS）、`call`（Python）等节点，提取调用者方法名和被调用函数名。

- 利用 tree-sitter 的 `parent` 遍历找到调用点所在的函数/方法声明节点 → 获取调用者
- 从调用表达式节点中提取被调用函数的标识符 → 获取被调用者
- 跨文件调用：被调用者方法名 + 文件级 import 依赖（已有的 `ExtractDependenciesFromTree`）→ 推定目标文件
- 置信度：同文件内 AST 解析 → 0.9+；跨文件符号名匹配 → 0.7

**替代方案被拒绝**：
- Roslyn SemanticModel：已被移除，重新引入会破坏统一架构
- 仅用正则：当前方案，置信度低（~0.6），无法区分注释/字符串中的假匹配

### Decision 2: Tree-sitter AST 设计模式检测（替代正则）

**选择**：重写 `DesignPatternDetector`，7 种模式全部基于 AST 节点关系：

| 模式 | AST 检测逻辑 |
|------|-------------|
| Factory | 方法返回类型为接口/基类 + 方法体包含 `new` 表达式创建具体类 |
| Strategy | 接口节点 + 多个 `class_declaration` 实现该接口 + DI 注入 `IEnumerable<T>` |
| Observer | `event` 关键字节点 + `+=` 订阅操作符 |
| Builder | 类方法返回 `this` 类型 + 链式调用模式 |
| Singleton | `static` 字段持有自身类型实例 + `private` 构造函数 |
| Repository | 类实现命名包含 `IRepository` 的接口 |
| Mediator | 类注入多个 `IRequestHandler` / `INotificationHandler` 接口 |

**替代方案被拒绝**：
- 仅用类名正则：当前方案，无法验证真实的结构关系（如假 Factory 类名不含 Create 方法）

### Decision 3: TaskPromptService 重构为管线协调层

**选择**：`TaskPromptService` 不再包含任何提示词文本。改为：
1. 接收 `IPromptMergeService` 依赖
2. 对外暴露 `BuildWikiStructurePromptAsync`、`BuildWikiPagePromptAsync` 等方法
3. 内部通过 `_promptMergeService.BuildPrompt(category, provider, format, variables)` 获取 DB 中的模板
4. 只负责变量替换和管线特定逻辑（如深度指导字符串 `GetDepthGuidance`——这些保留，因为是逻辑而非模板）

**替代方案被拒绝**：
- 直接在 `WikiTaskService` 中调用 `IPromptMergeService`：会让 WikiTaskService 更臃肿（1809 行），TaskPromptService 作为协调层保持关注点分离

### Decision 4: 结构化消息迁移路径

**选择**：所有管线 LLM 调用点从字符串重载改为结构化消息重载：

```
Before: _taskLlm.GenerateWithMetricsAsync(provider, model, null, singleStringPrompt, ct)
After:  _taskLlm.GenerateWithMetricsAsync(provider, model, null, chatMessages, ct)
```

`chatMessages` 构建逻辑：
1. `new ChatMessage(ChatRole.System, systemPrompt)` — 从 DB 模板中提取的 System 角色部分
2. `new ChatMessage(ChatRole.User, userPrompt)` — 上下文 + 任务指令（分离的 User 消息）
3. 对于多文件/多阶段场景，文件上下文作为独立 `ChatRole.User` 消息追加

`ChatMessageBuilderService` 扩展 `BuildWikiMessages` 方法，复用已有的角色映射基础设施。

**替代方案被拒绝**：
- 保持字符串 + system prompt 参数：无法充分利用 ChatMessage 模型的角色分离能力
- 为每个上下文片段创建独立消息：过度碎片化，影响 LLM 理解质量

## Risks / Trade-offs

1. **[Risk] Tree-sitter 语法级调用图无法区分重载方法**：同名方法可能有多个重载 → **Mitigation**：AST 可以提取参数数量和类型信息，优先匹配参数签名；无法区分时标记低置信度（0.7）
2. **[Risk] DB 提示词首次加载性能**：所有模板从 DB 读取可能比硬编码慢 → **Mitigation**：`IPromptMergeService` 使用 `IMemoryCache` 缓存（有效期 10 分钟），首次命中后速度等同于硬编码
3. **[Risk] 提示词迁移导致生成质量波动**：DB 模板内容不同于当前硬编码模板 → **Mitigation**：DB 种子数据从当前硬编码模板内容直接迁移，保持核心提示词一致；格式升级（五层结构）逐步验证
4. **[Trade-off] 放弃 Roslyn 语义精度**：Tree-sitter 无法解析 `GetSymbolInfo` 级别的精确类型信息 → 接受：大规模索引优先速度和覆盖面，语义增强留给 LSP 方案

## Migration Plan

1. **阶段 1 — AST 实现**：扩展 `TreeSitterAnalyzer`，重写 `CallGraphBuilder` 和 `DesignPatternDetector`。无数据库影响，可回滚。
2. **阶段 2 — 提示词 DB 化**：扩展 `PromptSeedData`，重写 `TaskPromptService`。需要执行种子数据更新。回滚：恢复 `TaskPromptService` 旧代码 + 重新播种。
3. **阶段 3 — 结构化消息**：修改 `WikiTaskService`、`SlidesTaskService`、`WorkshopTaskService`。无数据库影响，可回滚。
4. **阶段 4 — 清理**：删除死代码 `PromptTemplateService`，更新 6 个 spec。可回滚。
5. **验证**：`dotnet build` 零错误 + 关键路径手动测试（Wiki 生成、Slides 生成、Workshop 生成）

每个阶段独立可验证，不依赖后续阶段。
