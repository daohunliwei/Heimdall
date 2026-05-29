## ADDED Requirements

### Requirement: lookup_file 工具
系统 SHALL 提供 `lookup_file(string filePath, int? startLine = null, int? endLine = null)` 方法，从当前 `WikiVersion` 绑定的 `AstVersion` 的 Workspace AST 目录读取目标文件的 CST 数据和源码。返回包含带行号的源码片段和该文件的符号摘要清单。

#### Scenario: 完整文件查询
- **WHEN** LLM 调用 `lookup_file("Services/UserService.cs")`
- **THEN** 从 `workspace/repos/` 读取源文件内容（带行号）
- **AND** 从 `workspace/ast/{id}/symbols.json` 匹配该文件的符号列表

#### Scenario: 指定行范围查询
- **WHEN** LLM 调用 `lookup_file("Services/UserService.cs", 20, 45)`
- **THEN** 返回指定行范围内的内容
- **AND** 标注截断信息

### Requirement: find_usages 工具
系统 SHALL 提供 `find_usages(string symbolName, string? symbolKind = null)` 方法，从 Workspace AST 目录的 `.cst` 文件中反查指定符号的所有调用者。

#### Scenario: 查询方法调用者
- **WHEN** LLM 调用 `find_usages("GetUserAsync")`
- **THEN** 从 `workspace/ast/{id}/files/*.cst` 的 edges 中反查
- **AND** 返回所有文件中调用 `GetUserAsync` 的调用者列表

#### Scenario: 未找到任何引用
- **WHEN** 指定的符号名在调用边中不存在
- **THEN** 返回"未找到对 {symbolName} 的引用"

## MODIFIED Requirements

### Requirement: ReadCodeFile 工具
系统 SHALL 提供 `ReadCodeFile(string filePath, int maxLines=500)` 方法，从 Workspace `repos/` 目录读取仓库源文件，或从 `workspace/ast/{id}/` 获取 CST 信息辅助格式化。返回带行号的代码文本。单次最多返回 maxLines 行，超出截断并标注。

#### Scenario: 成功读取 / 超行截断 / 文件不存在
- **WHEN** LLM 调用 `ReadCodeFile` 且文件在 workspace repos 中存在且 < 500 行 → 返回完整内容；超过 maxLines → 截断并标注；文件不存在 → 返回错误提示

### Requirement: SearchSymbols 工具
系统 SHALL 提供 `SearchSymbols(string query, string? symbolKind=null)` 方法，从 DB `AstVersion.symbol_names_json` 列直接匹配（无需文件 I/O），或从 `workspace/ast/{id}/symbols.json` 补充。返回 top-10 匹配结果。

#### Scenario: 按类名搜索
- **WHEN** LLM 调用 `SearchSymbols("IUserRepository")`
- **THEN** 系统优先从 DB `symbol_names_json` 匹配
- **AND** 返回包含符号名称、文件路径、行号、符号类型的结果列表

### Requirement: QueryCallGraph 工具
系统 SHALL 提供 `QueryCallGraph(string symbolName, string direction="both")` 方法，从 Workspace AST 文件的 edges 数据中查询调用关系。返回调用者和被调用者列表，按置信度降序排列。

#### Scenario: 查询双向调用关系
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser")`
- **THEN** 系统从 workspace ast 文件中读取 edges
- **AND** 返回调用者和被调用者列表，含置信度评分

### Requirement: RetrieveClassDefinition 工具
系统 SHALL 提供 `RetrieveClassDefinition(string className)` 方法，从 `workspace/ast/{id}/symbols.json` 中查找指定类的完整定义信息。返回类的完整签名、基类/接口列表、公开方法签名、属性列表、所属文件和行号。

#### Scenario: 成功检索类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 系统从 workspace symbols 文件中匹配类名
- **AND** 返回类签名、基类、接口、方法、属性、文件路径和行号
