## MODIFIED Requirements

### Requirement: Tree-sitter 统一多语言 AST 完整提取
系统 SHALL 使用修复后的 `TreeSitterAnalyzer` 对 13 种已配置语言执行完整 AST 解析。对每个源代码文件，SHALL 提取 `AstSymbol` 的全部 10 个字段——所有字段均从 tree-sitter 语法树节点中获取，不再设 null/空值。Kind 为父节点类型（class/method/interface）。提取沿用的 S-expression Query 不变（13 种语言），但提取逻辑升级为 parent 遍历 + 完整字段填充。

#### Scenario: C# 文件完整符号提取
- **WHEN** 解析包含类/方法/接口/属性的 C# 文件
- **THEN** 每个符号的 10 个字段均有值（Kind ≠ "identifier"、ParentClass 有值、Modifiers 有值 等）

### Requirement: AST 驱动的调用边提取
调用边数据 SHALL 来源于 `TreeSitterAnalyzer.ExtractCallEdges` 的 AST 调用表达式节点提取，替代旧 `CallGraphBuilder` 正则实现。同文件调用置信度 ≥0.9，跨文件符号名+import 匹配 ≥0.7。

#### Scenario: AST 调用边完整提取
- **WHEN** 解析包含方法调用的文件
- **THEN** `ExtractCallEdges` 返回 AstCallEdge 列表，CallerSymbol/CalleeSymbol 均非空，Confidence > 0

### Requirement: AST 设计模式检测
设计模式检测 SHALL 基于 `TreeSitterAnalyzer` 的 AST 节点关系，7 种模式（Factory/Strategy/Observer/Singleton/Builder/Repository/Mediator）的检测逻辑 SHALL 在 `TreeSitterAnalyzer` 内部完成，输出到 `DesignPatternHints` 列表中。不再依赖外部 `DesignPatternDetector` 类。

#### Scenario: AST 设计模式检测有输出
- **WHEN** 解析包含已知设计模式的代码文件
- **THEN** `DesignPatternHints` 列表非空，包含模式名称和置信度

## REMOVED Requirements

### Requirement: 正则调用图构建（CallGraphBuilder）
**Reason**: `CallGraphBuilder` 使用正则 + `body.Contains(name+"(")`。TreeSitterAnalyzer 的 `ExtractCallEdges` 提供 AST 级替代。
**Migration**: 删除 `CallGraphBuilder` 全部代码。已有调用图数据由 `TreeSitterAnalyzer` 重新构建。

### Requirement: 类名正则设计模式检测（DesignPatternDetector）
**Reason**: `DesignPatternDetector` 使用类名正则匹配。TreeSitterAnalyzer 内部 AST 检测提供结构级替代。
**Migration**: 删除 `DesignPatternDetector` 全部代码。已有模式标记由 `TreeSitterAnalyzer.DetectDesignPatterns` 重新生成。
