## MODIFIED Requirements

### Requirement: ReadCodeFile 工具
系统 SHALL 提供 `ReadCodeFile(string filePath, int maxLines=500)` 方法，从 `AstVersion` 关联的 Workspace `ast/{version_id[:8]}/files/` 目录中读取 CST 文件和源码，或不依赖 AST 时从 `workspace/repos/` 仓库路径读取原始文件。返回带行号的代码文本。单次最多返回 maxLines 行，超出截断并标注。文件不存在时返回错误提示。

#### Scenario: 成功读取 / 超行截断 / 文件不存在
- **WHEN** LLM 调用 `ReadCodeFile` 且文件存在 120 行 → 返回完整内容；文件 800 行 → 返回前 maxLines 行并标注截断；文件不存在 → 返回错误提示

### Requirement: SearchSymbols 工具
系统 SHALL 提供 `SearchSymbols(string query, string? symbolKind=null)` 方法，从 `AstVersion.symbol_names_json`（DB 列）或 `ast/{version_id[:8]}/symbols.json`（Workspace 文件）中搜索匹配符号。返回 top-10 匹配结果，包含符号名称、类型、文件路径和行号。

#### Scenario: 按类名搜索
- **WHEN** LLM 调用 `SearchSymbols("IUserRepository")`
- **THEN** 系统优先从 DB `symbol_names_json` 列匹配
- **AND** 返回包含符号名称、文件路径、行号、符号类型的结果列表

### Requirement: QueryCallGraph 工具
系统 SHALL 提供 `QueryCallGraph(string symbolName, string direction="both")` 方法，从 Workspace `ast/{version_id[:8]}/manifest.json` 中定位到对应的 CST 文件，读取调用边数据。返回调用者和被调用者列表，按置信度降序排列。

#### Scenario: 查询双向调用关系
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser")`
- **THEN** 系统从 workspace AST 文件中读取调用边
- **AND** 返回调用者和被调用者列表，含置信度评分

### Requirement: RetrieveClassDefinition 工具
系统 SHALL 提供 `RetrieveClassDefinition(string className)` 方法，从 Workspace `ast/{version_id[:8]}/symbols.json` 中查找指定类。返回类的完整签名、基类/接口列表、公开方法签名、属性列表、所属文件和行号。

#### Scenario: 成功检索类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 系统从 workspace symbols 文件中匹配类名
- **AND** 返回类签名、基类、接口、方法、属性、文件路径和行号
