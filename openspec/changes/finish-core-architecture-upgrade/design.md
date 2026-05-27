## Context

当前架构中 AST 数据的端到端利用率不足 8%。TreeSitterAnalyzer 提取的 10 个 AstSymbol 字段仅 1 个（Name）保留到管道下游；AstCallEdge 的 6 个字段仅 1 个（CalleeFilePath）保留。到达 LLM 提示词的是"23 methods, 156 edges"式聚合数字——无法帮助 LLM 理解代码结构。

核心架构缺陷：三个独立管道各自为政，AST 数据在管道路径中被丢弃。

## Goals / Non-Goals

**Goals:**
- AST 数据端到端保真：TreeSitterAnalyzer 提取 → CodeIndexService 存储 → 管道传输 → 提示词注入，全链路保留结构化数据
- 结构规划提示词从"聚合数字"升级为"结构化 AST 关系"
- 页面生成提示词中代码块附带 AST 上下文
- 删除所有正则和硬编码代码路径

**Non-Goals:**
- 不引入 Roslyn / Microsoft.CodeAnalysis
- 不新增外部 NuGet 包
- 不修改前端
- 不改变 Chat/Ask 路径

## Decisions

### Decision 1: AST 提示词注入架构（核心设计）

**选择**：提示词分三层注入 AST 数据，每层有明确的职责和格式。

```
┌─────────────────────────────────────────────────────────────┐
│                   AST → Prompt 三层架构                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  L1: 结构概览（注入结构规划提示词）                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ 类型层级: UserService → BaseService, IUserService     │    │
│  │ 调用拓扑: AuthController → UserService → IUserRepo    │    │
│  │ 设计模式: IUserService 有 3 实现类 → 策略模式 (0.95)   │    │
│  │ 模块依赖: Api → Core → Repository                     │    │
│  │ 入口文件: Program.cs, Startup.cs                      │    │
│  └──────────────────────────────────────────────────────┘    │
│                                                              │
│  L2: 页面级 AST 上下文（注入页面生成提示词）                     │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ [代码块: UserService.CreateUser]                       │    │
│  │   Class: UserService : BaseService, IUserService      │    │
│  │   Signature: public async Task<User> CreateUser(...)  │    │
│  │   Called by: AuthController.Register, AdminController │    │
│  │   Calls: IUserRepository.AddAsync, IValidator.Validate│    │
│  │   Design role: Strategy Pattern participant           │    │
│  └──────────────────────────────────────────────────────┘    │
│                                                              │
│  L3: 工具级（LLM 按需查询）                                    │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ QueryCallGraph("CreateUser") → 调用链完整拓扑          │    │
│  │ RetrieveClassDefinition("UserService") → 完整类定义    │    │
│  │ SearchSymbols("IUserRepo") → 所有实现类和引用位置      │    │
│  └──────────────────────────────────────────────────────┘    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

**替代方案被拒绝**：
- 保持当前"聚合数字"方案：LLM 只能看到计数，无法理解结构
- 注入原始 AST JSON：数据量过大，超出上下文窗口

### Decision 2: TreeSitterAnalyzer 完整提取

**选择**：扩展所有 13 种语言的 S-expression Query，提取 AstSymbol 的全部 10 个字段。

| 字段 | S-expression Query 模式 |
|------|------------------------|
| Name | `(class_declaration name: (identifier) @name)` |
| Kind | 从父节点类型判定（class_declaration → "class" 等） |
| FullSignature | 拼接返回类型 + 名称 + 参数列表 |
| ParentClass | `(class_declaration bases: (base_list (identifier) @base))` |
| Modifiers | `(method_declaration (access_modifier) @mod)` |
| BaseTypes | `(class_declaration bases: (base_list (identifier) @base))` （含接口） |
| AttributeAnnotations | `(attribute (identifier) @attr)` |
| StartLine/EndLine | tree-sitter 节点原生的 `StartPosition`/`EndPosition` |
| FilePath | 已知参数 |

**替代方案被拒绝**：仅提取 Name（当前方案）：无法区分重载、无法推断类型关系、无法构建完整类型图。

### Decision 3: CodeIndexEntry 保留 AST 结构

**选择**：`CodeIndexEntry` 不再将 AST 数据展平为 `List<string>`，改为保留结构化字段：

```csharp
// 旧（丢弃结构）
public List<string> ExportedSymbols { get; set; }  // 仅符号名字符串
public List<string> DependencyHints { get; set; }  // 仅文件路径字符串

// 新（保留结构）
public List<AstSymbol> Symbols { get; set; }        // 完整 10 字段
public List<AstCallEdge> CallEdges { get; set; }    // 完整 6 字段
public string? ParentClass { get; set; }            // 所属父类名
public List<string>? ImplementedInterfaces { get; set; }
public List<string>? Modifiers { get; set; }
```

BM25 索引新增 `AstMetadata` 搜索字段——代码块检索时 AST 上下文随文本一起返回。

**替代方案被拒绝**：
- 新建独立的 AST 实体表：增加查询开销，AST 数据应与代码索引紧密绑定
- 在 CodeIndexEntry 上添加 JSON 列：可查询性差，不利于 BM25 索引

### Decision 4: 合并 CodeUnderstandingService 到 AST 管道

**选择**：消除"正则管道 vs AST 管道"的双轨制。`CodeUnderstandingService` 不再独立加载原始文件运行正则分析器，而是接收 `CodeIndexResult`（含 AST 数据）作为输入：

```
旧架构:
  CodeIndexService(AST) → CodeIndexResult
  CodeUnderstandingService(正则) → CodeUnderstandingResult  ← 独立管道，无 AST

新架构:
  CodeIndexService(AST) → CodeIndexResult (含 AST 结构化数据)
       └→ CodeUnderstandingService ← 接收 AST 数据
            └→ 设计模式检测 (基于 AST 符号关系)
            └→ 调用拓扑聚合 (基于 AST 调用边)
            └→ LLM 架构理解 (注入 AST 类型层级和调用拓扑)
            └→ CodeUnderstandingResult
```

`CallGraphBuilder` 和 `DesignPatternDetector` 的正则实现完全删除。

**替代方案被拒绝**：
- 保留 CodeUnderstandingService 独立加载文件但改用 AST：重复 IO，管道数据一致性无法保证

### Decision 5: 提示词注入格式

**选择**：AST 数据以结构化 Markdown 注入提示词，而非 JSON：

```markdown
## 类型层级（AST 分析）
- `UserService` (class, public) 继承 `BaseService`, 实现 `IUserService`, `IDisposable`
  - 12 个方法: CreateUser(public), GetById(public), ValidateEmail(private), ...
  - 被 3 个类调用: AuthController, AdminController, UserBackgroundService
  - 调用了: IUserRepository.AddAsync, IValidator.Validate, IEmailService.Send
  - 设计角色: 策略模式参与者（IUserService 接口实现）

## 调用拓扑（AST 分析）
AuthController.Register → UserService.CreateUser → IUserRepository.AddAsync
                     → UserService.CreateUser → IValidator.Validate
AdminController.BatchCreate → UserService.CreateUser
```

**替代方案被拒绝**：
- JSON 注入：增加 token 消耗，LLM 对自然语言解析优于 JSON 结构
- 仅注入符号名列表：与当前无差异，无增益

### Decision 6: 提示词 DB 化 + 结构化消息（继承原设计）

与之前的设计一致：`TaskPromptService` 重写为 DB 驱动协调层，所有 LLM 调用改为 `List<ChatMessage>` 结构化消息。不再赘述。

## Risks / Trade-offs

1. **[Risk] AST 数据膨胀提示词 token 消耗**：注入类型层级和调用拓扑增加 prompt 长度 → **Mitigation**：L1/L2 数据受 `ContextPackingService` 预算约束，低优先级关系裁剪
2. **[Risk] 13 种语言 S-expression Query 扩展工作量大** → **Mitigation**：从 C#/TypeScript/Python/Go/Java 5 种核心语言开始，其余渐进覆盖
3. **[Trade-off] 符号名匹配 vs 语义解析**：tree-sitter 无法像 Roslyn 做类型解析，跨文件调用匹配仍基于符号名 → 接受：标记置信度，LLM 提示词中标注 `(推定)`
4. **[Risk] 设计模式 AST 检测复杂度**：7 种模式需要不同的 AST 节点遍历逻辑 → **Mitigation**：优先实现 Factory/Strategy/Singleton 三种高频模式，其余后续

## Data Flow: Before vs After

```
BEFORE (AST 利用率 <8%):
TreeSitterAnalyzer → [展平为字符串] → CodeIndexEntry(List<string>)
                                   → BM25 索引（仅文本）
CodeUnderstandingService(正则)     → 聚合数字 → Prompt("23 methods...")

AFTER (AST 利用率 >80%):
TreeSitterAnalyzer → CodeIndexEntry(structured AST)
       ├→ BM25 索引（文本 + AST 元数据）
       ├→ CodeUnderstandingService(AST 输入)
       │     ├→ 设计模式 (AST 节点关系)
       │     └→ 调用拓扑 (AST 调用边)
       └→ Prompt:
             L1: "UserService 继承 BaseService，有 5 public 方法，被 3 Controller 调用"
             L2: "[CreateUser] 是 UserService 的 public async 方法，调用 IUserRepo.AddAsync"
             L3: QueryCallGraph → AST 调用边数据
```
