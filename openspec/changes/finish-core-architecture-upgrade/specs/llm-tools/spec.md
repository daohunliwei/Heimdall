## MODIFIED Requirements

### Requirement: QueryCallGraph 工具（AST 数据源）
系统 SHALL 通过 `QueryCallGraphTool` 查询调用图中指定符号的调用者列表和被调用者列表。调用图数据 SHALL 来源于 Tree-sitter AST 提取的方法级调用关系（`TreeSitterAnalyzer.ExtractCallEdges`），替代旧正则 `CallGraphBuilder` 数据。

#### Scenario: 查询双向调用关系
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser")`
- **THEN** 返回 AST 提取的调用者/被调用者列表，按置信度降序排列（同文件 AST ≥ 0.9，跨文件符号匹配 ≥ 0.7）

### Requirement: RetrieveClassDefinition 工具（AST 数据源）
系统 SHALL 通过 `RetrieveClassDefinitionTool` 返回类的完整信息。类定义数据 SHALL 来源于 Tree-sitter AST 提取的结构信息（类签名、方法列表、属性列表、基类/接口），替代旧 `CodeIndexEntry` 的字符串列表。

#### Scenario: 检索类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 返回 AST 提取的：类签名、基类、实现接口、公开方法签名含参数、属性列表、文件路径和行号

### Requirement: SearchSymbols 工具
系统 SHALL 通过 `SearchSymbolsTool` 封装对 `IHybridSearchService.SearchAsync` 的调用。符号索引 SHALL 包含 Tree-sitter AST 提取的符号名和符号类型（class/method/interface/function），支持按符号类型筛选。
