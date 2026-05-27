## MODIFIED Requirements

### Requirement: 方法级调用图构建
系统 SHALL 通过 `TreeSitterAnalyzer.ExtractCallEdges` 从 tree-sitter 语法树中提取方法级调用关系。对 `invocation_expression`（C#）、`call_expression`（TypeScript/JS）、`call`（Python）等调用节点，通过 `parent` 遍历定位调用者方法，从调用表达式节点提取被调用函数标识符。跨文件调用通过被调用者方法名 + 文件级 import 依赖推定目标文件。置信度：同文件 AST 解析 ≥ 0.9，跨文件符号名匹配 ≥ 0.7。

#### Scenario: AST 提取同文件方法调用
- **WHEN** C# 文件 `UserService.cs` 中 `CreateUser` 方法调用了 `ValidateEmail`
- **THEN** TreeSitterAnalyzer 识别 `invocation_expression` 节点 → parent 遍历定位 `CreateUser` 为调用者 → 提取被调用标识符 `ValidateEmail` → 置信度 0.95

#### Scenario: 跨文件调用关系
- **WHEN** `ControllerA.cs` 调用了 `ServiceB.Process()`，import 依赖指向 `ServiceB.cs`
- **THEN** 调用图记录跨文件调用：调用者 `ControllerA.MethodX` → 被调用者 `ServiceB.Process` → 置信度 0.7

#### Scenario: 非支持语言回退正则
- **WHEN** 文件语言无 tree-sitter 语法支持
- **THEN** 系统回退到正则匹配方法调用模式

## REMOVED Requirements

### Requirement: 正则方法调用提取（旧 CallGraphBuilder）
**Reason**: 旧 `CallGraphBuilder` 使用正则 + `body.Contains(name + "(")` 字符串匹配构建调用图，置信度低（0.6），无法区分注释/字符串中的假匹配。Tree-sitter AST 提供语法级精度替代。
**Migration**: 删除 `CallGraphBuilder` 中所有正则调用提取逻辑。已有调用图数据在下次索引时由 AST 重新构建。

---

### Requirement: 设计模式启发式检测
系统 SHALL 通过 Tree-sitter AST 节点关系识别常见设计模式。Factory 检测方法返回接口类型 + `object_creation_expression` 创建具体类；Strategy 检测接口 `class_declaration` 多实现 + DI 注入 `IEnumerable<T>`；Observer 检测 `event` 关键字 + `+=` 订阅操作符；Singleton 检测 `static` 字段持有自身实例 + `private` 构造函数；Builder 检测方法返回 `this` + 链式调用；Repository 检测实现 `IRepository` 接口；Mediator 检测注入 `IRequestHandler`/`INotificationHandler`。

#### Scenario: Factory 模式 AST 检测
- **WHEN** AST 发现方法返回类型为接口且方法体包含 `new` 表达式创建具体实现类
- **THEN** 系统标注为"工厂模式"，置信度 ≥ 0.9

#### Scenario: Strategy 模式 AST 检测
- **WHEN** AST 发现接口有 3+ 个 `class_declaration` 实现，且构造函数注入 `IEnumerable<T>`
- **THEN** 系统标注为"策略模式"，置信度 ≥ 0.95

#### Scenario: Observer 模式 AST 检测
- **WHEN** AST 检测到 `event` 关键字节点及 `+=` 订阅操作符
- **THEN** 系统标注为"观察者模式"，记录事件发布者和订阅者

## REMOVED Requirements

### Requirement: 类名正则设计模式检测（旧 DesignPatternDetector）
**Reason**: 旧 `DesignPatternDetector` 使用类名正则匹配（如 `class\s+(\w*Factory\w*)`）检测设计模式，无法验证真实结构关系。Tree-sitter AST 节点关系提供结构级精度替代。
**Migration**: 删除 `DesignPatternDetector` 中所有类名正则匹配逻辑。已有设计模式标记在下次分析时由 AST 重新检测。
