## ADDED Requirements

### Requirement: ReadCodeFile 工具
系统 SHALL 提供 `ReadCodeFileTool` 静态类，暴露 `ReadCodeFile(string filePath, int maxLines = 500)` 方法，通过 `AIFunctionFactory.Create` 转换为 MEAI `AIFunction`。方法 SHALL 从本地仓库工作目录读取指定文件的代码内容，单次最多返回 `maxLines` 行（默认 500），超出部分截断并在末尾标注截断位置。文件不存在时 SHALL 抛出 `FileNotFoundException` 包裹的错误信息，由 `TaskLlmService` 的异常处理机制返回给 LLM。

#### Scenario: 成功读取文件
- **WHEN** LLM 调用 `ReadCodeFile("/src/Services/UserService.cs")`，文件存在且 120 行
- **THEN** 返回文件完整内容（120 行），包含行号前缀（格式：`   1| using System;`）

#### Scenario: 文件超过最大行数
- **WHEN** LLM 调用 `ReadCodeFile("/src/LargeFile.cs")`，文件 800 行，maxLines=500
- **THEN** 返回前 500 行，末尾追加 `[截断：文件共 800 行，已返回前 500 行。可通过 SearchSymbols 定位后指定偏移读取剩余部分]`

#### Scenario: 文件不存在
- **WHEN** LLM 调用 `ReadCodeFile("/src/NotExist.cs")`，文件不存在
- **THEN** 抛出异常，`TaskLlmService` 将异常信息包装为 `FunctionResultContent` 返回给 LLM
- **AND** LLM 收到 `错误：文件 /src/NotExist.cs 不存在于当前仓库中`

### Requirement: SearchSymbols 工具
系统 SHALL 提供 `SearchSymbolsTool` 静态类，暴露 `SearchSymbols(string query, string? symbolKind = null)` 方法，封装对现有 `IHybridSearchService.SearchAsync` 的调用。方法 SHALL 返回 top-10 匹配结果的符号名称、文件路径、行号、符号类型（类/接口/方法/属性/枚举）。`query` 参数 SHALL 同时进行 BM25 关键词匹配和 pgvector 语义相似度搜索。

#### Scenario: 按类名搜索
- **WHEN** LLM 调用 `SearchSymbols("IUserRepository")`
- **THEN** 返回搜索结果列表：`IUserRepository (接口) → /src/Repositories/IUserRepository.cs:5`、`UserRepository (类) → /src/Repositories/UserRepository.cs:12`
- **AND** 最多返回 10 条结果

#### Scenario: 按方法名搜索
- **WHEN** LLM 调用 `SearchSymbols("GetUserById", symbolKind: "method")`
- **THEN** 仅返回方法类型的匹配结果，过滤掉类型/接口/属性

#### Scenario: 搜索无结果
- **WHEN** LLM 调用 `SearchSymbols("NonExistentClass")` 且无匹配
- **THEN** 返回 `搜索 "NonExistentClass" 无结果。建议尝试不同的搜索词。`

### Requirement: QueryCallGraph 工具
系统 SHALL 提供 `QueryCallGraphTool` 静态类，暴露 `QueryCallGraph(string symbolName, string direction = "both")` 方法，封装对代码索引中调用图数据的查询。方法 SHALL 返回指定符号的调用者列表（谁调用了它）和被调用者列表（它调用了谁），按置信度降序排列。

#### Scenario: 查询调用者
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser", direction: "callers")`
- **THEN** 返回：`被以下符号调用: UserController.Register (置信度 0.98), AdminController.BatchCreate (置信度 0.95)`

#### Scenario: 查询被调用者
- **WHEN** LLM 调用 `QueryCallGraph("UserController.Register", direction: "callees")`
- **THEN** 返回：`调用以下符号: UserService.CreateUser (置信度 0.98), IValidator.Validate (置信度 0.96), IUserRepository.AddAsync (置信度 0.97)`

#### Scenario: 未在调用图中的符号
- **WHEN** LLM 调用 `QueryCallGraph("SomeUtilityMethod")` 且该符号不在调用图中
- **THEN** 返回 `符号 "SomeUtilityMethod" 不在调用图索引中。可能是无调用关系的叶子方法。`

### Requirement: RetrieveClassDefinition 工具
系统 SHALL 提供 `RetrieveClassDefinitionTool` 静态类，暴露 `RetrieveClassDefinition(string className)` 方法，封装对代码索引中类定义摘要的查询。方法 SHALL 返回类的完整签名、基类/接口列表、公开方法签名、属性列表、所属文件和行号。

#### Scenario: 成功检索类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 返回：`类 UserService (完整签名: public class UserService : IUserService) → 文件: /src/Services/UserService.cs:15 → 基类: Object → 接口: IUserService, IDisposable → 方法: CreateUser(UserDto): User, GetById(int): User, Delete(int): void → 属性: DbContext Context`

#### Scenario: 类未找到
- **WHEN** LLM 调用 `RetrieveClassDefinition("UnknownClass")` 且该类不在索引中
- **THEN** 返回 `未在代码索引中找到类 "UnknownClass" 的定义。请检查类名拼写或使用 SearchSymbols 搜索。`

### Requirement: 工具描述元数据
每个工具方法 SHALL 使用 `[Description("...")]` 特性标注中文描述，该描述将通过 MEAI `AIFunctionFactory.Create` 自动转换为 OpenAI Function Calling 的 `function.description` 字段，帮助 LLM 选择合适的工具。

#### Scenario: 工具描述传递给 LLM
- **WHEN** `AIFunctionFactory.Create(ReadCodeFile)` 生成 `AIFunction`
- **THEN** 生成的 Function Definition 包含 `name: "ReadCodeFile"` 和 `description: "根据文件路径读取仓库中的代码内容，返回带行号的代码文本。单次最多返回500行。"`
- **AND** 参数描述 `filePath: "要读取的文件相对路径，如 /src/Services/UserService.cs"` 和 `maxLines: "最大返回行数，默认500"`
