## ADDED Requirements

### Requirement: lookup_file 工具
系统 SHALL 提供 `lookup_file(string filePath, int? startLine = null, int? endLine = null)` 方法，从当前 `WikiVersion` 绑定的 `AstVersion.result_json` 中查找目标文件的 chunks 数据。返回包含带行号的源码片段和该文件的符号摘要清单。

#### Scenario: 完整文件查询
- **WHEN** LLM 调用 `lookup_file("Services/UserService.cs")`
- **THEN** 返回该文件的所有 chunks 内容（带行号）
- **AND** 返回该文件的符号列表（类名、方法名、属性名）

#### Scenario: 指定行范围查询
- **WHEN** LLM 调用 `lookup_file("Services/UserService.cs", 20, 45)`
- **THEN** 返回指定行范围内的 chunks 内容
- **AND** 标注截断信息

#### Scenario: 文件不存在
- **WHEN** LLM 调用 `lookup_file("NonExistent.cs")`
- **THEN** 返回文件不存在的错误提示
- **AND** 建议检查文件路径拼写

### Requirement: find_usages 工具
系统 SHALL 提供 `find_usages(string symbolName, string? symbolKind = null)` 方法，从 `AstVersion.result_json` 的 callEdges 反查指定符号的所有调用者。

#### Scenario: 查询方法调用者
- **WHEN** LLM 调用 `find_usages("GetUserAsync")`
- **THEN** 返回所有文件中调用 `GetUserAsync` 的调用者列表
- **AND** 每条结果包含调用者符号名、文件路径、调用类型和置信度

#### Scenario: 限定符号类型
- **WHEN** LLM 调用 `find_usages("IUserService", "interface")`
- **THEN** 只在接口符号中查找引用
- **AND** 返回实现了该接口的类列表

#### Scenario: 未找到任何引用
- **WHEN** 指定的符号名在调用边中不存在
- **THEN** 返回"未找到对 {symbolName} 的引用"

## MODIFIED Requirements

### Requirement: ReadCodeFile 工具
系统 SHALL 提供 `ReadCodeFile(string filePath, int maxLines=500)` 方法，从当前 `WikiVersion` 绑定的 `AstVersion.result_json` 的 chunks 中读取文件内容，不再访问本地文件系统。返回带行号的代码文本。单次最多返回 maxLines 行，超出截断并标注。

#### Scenario: 成功读取 / 超行截断 / 文件不存在
- **WHEN** LLM 调用 `ReadCodeFile` 且文件在 `AstVersion` 中存在且 < 500 行 → 返回完整内容；超过 maxLines → 截断并标注；文件不在 AST 结果中 → 返回错误提示

### Requirement: SearchSymbols 工具
系统 SHALL 提供 `SearchSymbols(string query, string? symbolKind=null)` 方法，从 `AstVersion.result_json` 的 `symbol_names_json` 或内联 `symbols` 数组中搜索匹配符号，不再依赖 `IHybridSearchService`。返回 top-10 匹配结果，包含符号名称、类型、文件路径和行号。

#### Scenario: 按类名搜索
- **WHEN** LLM 调用 `SearchSymbols("IUserRepository")`
- **THEN** 从 `AstVersion` 数据中匹配符号名
- **AND** 返回包含符号名称、文件路径、行号、符号类型的结果列表

### Requirement: QueryCallGraph 工具
系统 SHALL 提供 `QueryCallGraph(string symbolName, string direction="both")` 方法，从 `AstVersion.result_json` 的 `call_edges` 数组中查询调用边，不再依赖 `DependencyTopologyService`。返回调用者和被调用者列表，按置信度降序排列。

#### Scenario: 查询双向调用关系
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser")`
- **THEN** 直接从持久化的 call_edges 反查
- **AND** 返回调用者和被调用者列表，含置信度评分

### Requirement: RetrieveClassDefinition 工具
系统 SHALL 提供 `RetrieveClassDefinition(string className)` 方法，从 `AstVersion.result_json` 的 `symbols` 数组中查找指定类，不再访问文件系统。返回类的完整签名、基类/接口列表、公开方法签名、属性列表、所属文件和行号。

#### Scenario: 成功检索类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 从持久化的 symbols 数据中匹配类名
- **AND** 返回类签名、基类、接口、方法、属性、文件路径和行号
