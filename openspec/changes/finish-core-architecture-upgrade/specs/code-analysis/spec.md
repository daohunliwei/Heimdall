## MODIFIED Requirements

### Requirement: Tree-sitter 统一多语言 AST 完整提取
系统 SHALL 使用 TreeSitter.DotNet 对 13 种已配置语言执行完整 AST 解析。对每个源代码文件，SHALL 提取 `AstSymbol` 的全部 10 个字段——Name、Kind（class/method/interface/function）、FullSignature、FilePath、StartLine、EndLine、ParentClass、Modifiers（public/private/protected/static/async）、BaseTypes（基类+实现的接口列表）、AttributeAnnotations。不再仅提取 Name 字段。通过 S-expression Query 扩展覆盖父类声明、接口实现、访问修饰符、属性注解节点。

#### Scenario: C# 类完整符号提取
- **WHEN** 解析 C# 文件 `UserService.cs` 包含 `public class UserService : BaseService, IUserService`
- **THEN** AstSymbol 包含：Name="UserService"、Kind="class"、ParentClass="BaseService"、BaseTypes=["IUserService"]、Modifiers=["public"]、StartLine=15、EndLine=120
- **AND** 类中每个方法的 AstSymbol 包含：Kind="method"、Modifiers=["public","async"]、FullSignature="Task<User> CreateUser(string name, string email)"、ParentClass="UserService"

#### Scenario: AST 调用边完整提取
- **WHEN** 解析包含方法调用 `_repository.AddAsync(user)` 的 C# 文件
- **THEN** TreeSitterAnalyzer.ExtractCallEdges 生成 AstCallEdge：CallerSymbol="UserService.CreateUser"、CalleeSymbol="AddAsync"、CallerFilePath、CalleeFilePath（通过 import 解析）、CallType="direct"、Confidence=0.9
- **AND** 同文件 AST 调用置信度 ≥ 0.9，跨文件符号名匹配 ≥ 0.7

### Requirement: AST 结构化数据由 AstVersion 承载
`CodeIndexEntry` SHALL 保持当前摘要字段（`ExportedSymbols`、`DependencyHints` 等），完整 AST 结构化数据（`AstSymbol`、`AstCallEdge` 等）SHALL 由 `AstVersion` 实体（`persist-versioned-ast-results` 变更已实施）和 `AstPersistenceProjection` 承载和传输。BM25 索引构建时从 `AstVersion` workspace 数据读取 chunks。

#### Scenario: CodeIndexEntry 保留摘要不变
- **WHEN** CodeIndexService 索引文件
- **THEN** CodeIndexEntry 保留文件级摘要（符号名列表、依赖提示）
- **AND** 完整结构化数据通过 AstPersistenceProjection → AstVersion 路径持久化

### Requirement: AST 数据注入结构规划提示词（L1 层）
结构规划提示词 SHALL 注入三个层次的 AST 数据：(1) 类型层级图——每个关键类的继承链、接口实现列表、公开方法签名；(2) 调用拓扑——关键方法的调用者和被调用者关系，以"X → Y → Z"格式呈现；(3) 设计模式证据——AST 检测到的模式名称、参与类列表和置信度。SHALL 以结构化 Markdown 格式注入，替代仅包含数字聚合的旧格式。

#### Scenario: 类型层级注入结构规划
- **WHEN** AST 分析显示 `UserService : BaseService, IUserService`、`IUserService` 有 `UserService`/`MockUserService`/`CachedUserService` 三个实现
- **THEN** 提示词包含："`UserService` (class, public) 继承 `BaseService`, 实现 `IUserService`。`IUserService` 有 3 个实现类——构成策略模式。12 个方法，5 个 public。被 AuthController、AdminController 调用。"

#### Scenario: 调用拓扑注入结构规划
- **WHEN** AST 调用边显示 AuthController.Register → UserService.CreateUser → IUserRepository.AddAsync
- **THEN** 提示词包含完整调用链，帮助 LLM 理解数据流方向并据此设计 Wiki 章节结构

#### Scenario: 设计模式证据注入结构规划
- **WHEN** AST 检测到策略模式（IUserService + 3 实现 + DI 注入 IEnumerable<IUserService>）
- **THEN** 提示词包含："检测到策略模式：`IUserService` 有 `UserService`/`MockUserService`/`CachedUserService` 三个实现，通过 DI 注入 → 建议为策略模式创建独立 article 页面"

### Requirement: AST 上下文注入页面生成提示词（L2 层）
页面生成提示词中每个代码块 SHALL 附带 AST 上下文元数据：(1) 所属类名和继承关系；(2) 方法签名和修饰符；(3) 调用该方法的其他方法列表；(4) 该方法调用的其他方法列表；(5) 该方法参与的设计模式角色（如有）。AST 上下文与 BM25 检索的代码文本合并注入。

#### Scenario: 代码块附带 AST 上下文
- **WHEN** 页面生成提示词包含 `UserService.CreateUser` 方法代码
- **THEN** 代码块前注入 AST 上下文块："`CreateUser` — `UserService` (class, public) 的 public async 方法。签名: `Task<User> CreateUser(string name, string email)`。被 `AuthController.Register` 调用。调用了 `IUserRepository.AddAsync`、`IValidator.Validate`。策略模式参与者。"

#### Scenario: AST 上下文受预算约束
- **WHEN** 页面涉及大量代码块且 AST 上下文总长度超过 ContextPackingService 分配的预算
- **THEN** 低优先级 AST 元数据（如内部调用方法列表）被截断，保留类关系和被调用者信息

### Requirement: AST 驱动的设计模式检测
设计模式检测 SHALL 基于 Tree-sitter AST 节点关系，不再使用类名正则匹配。Factory 检测方法返回接口类型 + `object_creation_expression`；Strategy 检测接口 3+ 实现 + DI `IEnumerable<T>` 注入；Observer 检测 `event` 关键字 + `+=` 订阅；Singleton 检测 `static` 自身类型字段 + `private` 构造函数；Repository 检测实现 `IRepository` 接口；Builder 检测方法返回 `this` + 链式调用；Mediator 检测注入 `IRequestHandler`/`INotificationHandler`。

#### Scenario: Strategy 模式 AST 检测
- **WHEN** AST 发现接口 `IUserService` 有 3 个 `class_declaration` 实现，且构造函数注入 `IEnumerable<IUserService>`
- **THEN** 系统标注为策略模式，置信度 ≥ 0.95，参与类列表注入提示词

## REMOVED Requirements

### Requirement: 正则调用图构建（CallGraphBuilder）
**Reason**: CallGraphBuilder 使用正则 + body.Contains(name+"(") 字符串匹配构建调用图，置信度低（0.6），无法区分注释/字符串中的假匹配。Tree-sitter AST 提供语法级精度替代。
**Migration**: 删除 `CallGraphBuilder` 全部代码。已有调用图数据由 TreeSitterAnalyzer.ExtractCallEdges 重新构建。

### Requirement: 类名正则设计模式检测（DesignPatternDetector）
**Reason**: DesignPatternDetector 使用类名正则匹配（如 class\s+(\w*Factory\w*)），无法验证真实结构关系。Tree-sitter AST 节点关系提供结构级精度替代。
**Migration**: 删除 `DesignPatternDetector` 全部代码。已有设计模式标记由新的 AST 检测器重新检测。
