## MODIFIED Requirements

### Requirement: AST L3 工具数据源
LLM 可调用的三个代码分析工具 SHALL 返回 AST 结构化数据：

- `QueryCallGraph(symbolName, direction)` SHALL 返回 AST 调用边数据（调用者/被调用者，含完整方法签名和置信度）。数据源由下游变更（`cst-backed-code-tools`）从持久化的 `AstVersion` workspace 文件中读取，不在 Tool 调用时实时解析。
- `RetrieveClassDefinition(className)` SHALL 返回 AstSymbol 的完整 10 字段数据——类签名、基类、接口列表、公开方法签名含参数、属性列表、修饰符、文件路径和行号。
- `SearchSymbols(query, symbolKind)` SHALL 支持按 AST Kind（class/method/interface/function）筛选，优先从 DB 轻量索引列（`symbol_names_json`）匹配，返回符号名、文件路径和行号。

#### Scenario: QueryCallGraph 返回 AST 数据
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser")`
- **THEN** 返回调用者列表（含完整方法签名和文件路径）和被调用者列表，同文件关系置信度 ≥ 0.9
- **AND** 数据来自持久化 AST 数据，不在调用时重新解析

#### Scenario: RetrieveClassDefinition 返回完整 AST 类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 返回 AST 完整数据：类签名 `UserService : BaseService, IUserService, IDisposable`、12 个公开方法签名、5 个属性、文件路径、修饰符

#### Scenario: SearchSymbols 按 AST Kind 筛选
- **WHEN** LLM 调用 `SearchSymbols("IUserService", "interface")`
- **THEN** 返回所有名为 `IUserService` 的 `interface` Kind 符号及其所在文件和行号
- **AND** 优先从 DB `symbol_names_json` 匹配，无需文件 I/O
