## MODIFIED Requirements

### Requirement: AST L3 工具数据源
LLM 可调用的三个代码分析工具 SHALL 数据源切换为 AST：

- `QueryCallGraph(symbolName, direction)` SHALL 返回 TreeSitterAnalyzer.ExtractCallEdges 构建的 AST 调用边数据。调用者/被调用者列表 SHALL 包含完整方法全签名（含参数类型）和置信度（同文件 AST ≥ 0.9，跨文件符号匹配 ≥ 0.7）。
- `RetrieveClassDefinition(className)` SHALL 返回 AstSymbol 的完整 10 字段数据——类签名、基类、接口列表、公开方法签名含参数、属性列表、修饰符、文件路径和行号。
- `SearchSymbols(query, symbolKind)` SHALL 支持按 AST Kind（class/method/interface/function）筛选，返回匹配的 AstSymbol 及其所在文件和行号。

#### Scenario: QueryCallGraph 返回 AST 数据
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser")`
- **THEN** 返回调用者列表（含完整方法签名和文件路径）和被调用者列表，同文件关系置信度 ≥ 0.9

#### Scenario: RetrieveClassDefinition 返回完整 AST 类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 返回 AST 完整数据：类签名 `UserService : BaseService, IUserService, IDisposable`、12 个公开方法签名、5 个属性、文件路径、修饰符

#### Scenario: SearchSymbols 按 AST Kind 筛选
- **WHEN** LLM 调用 `SearchSymbols("IUserService", "interface")`
- **THEN** 返回所有名为 `IUserService` 的 `interface` Kind 符号及其所在文件和行号
