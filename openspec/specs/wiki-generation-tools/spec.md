## ADDED Requirements

### Requirement: ReadCodeFile 工具
系统 SHALL 提供 `ReadCodeFileTool` 静态类，暴露 `ReadCodeFile(string filePath, int maxLines = 500)` 方法，通过 `AIFunctionFactory.Create` 转换为 MEAI `AIFunction`。方法 SHALL 从本地仓库工作目录读取指定文件的代码内容，返回带行号的代码文本。单次最多返回 `maxLines` 行，超出部分截断并标注截断位置。文件不存在时 SHALL 抛出 `FileNotFoundException`。

#### Scenario: 成功读取文件
- **WHEN** LLM 调用 `ReadCodeFile("/src/Services/UserService.cs")`，文件存在且 120 行
- **THEN** 返回文件完整内容，每行前缀格式为 `   1| using System;`

#### Scenario: 文件超过最大行数截断
- **WHEN** LLM 调用 `ReadCodeFile("/src/LargeFile.cs")`，文件 800 行，maxLines=500
- **THEN** 返回前 500 行，末尾追加截断提示：`[截断：文件共 800 行，已返回前 500 行]`

#### Scenario: 文件不存在
- **WHEN** LLM 调用 `ReadCodeFile("/src/NotExist.cs")`，文件不存在
- **THEN** 抛出 `FileNotFoundException`，`FunctionInvokingChatClient` 将异常信息包装为 Error 内容返回给 LLM

### Requirement: SearchSymbols 工具
系统 SHALL 提供 `SearchSymbolsTool` 静态类，暴露 `SearchSymbols(string query, string? symbolKind = null)` 方法，封装对现有 `IHybridSearchService.SearchAsync` 的调用。返回 top-10 匹配结果（符号名称、文件路径、行号、符号类型）。

#### Scenario: 按类名搜索
- **WHEN** LLM 调用 `SearchSymbols("IUserRepository")`
- **THEN** 返回搜索结果，每行格式：`IUserRepository (接口) → /src/Repositories/IUserRepository.cs:5`

#### Scenario: 搜索无结果
- **WHEN** LLM 调用 `SearchSymbols("NonExistentClass")` 且无匹配
- **THEN** 返回提示：`搜索 "NonExistentClass" 无结果。建议尝试不同的搜索词。`

### Requirement: QueryCallGraph 工具
系统 SHALL 提供 `QueryCallGraphTool` 静态类，暴露 `QueryCallGraph(string symbolName, string direction = "both")` 方法，查询调用图中指定符号的调用者列表和被调用者列表，按置信度降序排列。

#### Scenario: 查询双向调用关系
- **WHEN** LLM 调用 `QueryCallGraph("UserService.CreateUser")`
- **THEN** 返回调用者：`UserController.Register (置信度 0.98), AdminController.BatchCreate (0.95)`
- **AND** 被调用者：`IUserRepository.AddAsync (置信度 0.97), IValidator.Validate (0.96)`

### Requirement: RetrieveClassDefinition 工具
系统 SHALL 提供 `RetrieveClassDefinitionTool` 静态类，暴露 `RetrieveClassDefinition(string className)` 方法，返回类的完整签名、基类/接口列表、公开方法签名、属性列表、所属文件和行号。

#### Scenario: 成功检索类定义
- **WHEN** LLM 调用 `RetrieveClassDefinition("UserService")`
- **THEN** 返回：`类 UserService → /src/Services/UserService.cs:15 → 基类: Object → 接口: IUserService, IDisposable → 方法: CreateUser, GetById, Delete → 属性: DbContext`

### Requirement: 工具描述元数据
每个工具方法 SHALL 使用 `[Description("...")]` 特性标注中文描述，通过 `AIFunctionFactory.Create` 自动转换为 OpenAI Function Calling 的 `function.description` 字段。

#### Scenario: 工具描述传递给 LLM
- **WHEN** `AIFunctionFactory.Create(ReadCodeFile)` 生成 `AIFunction`
- **THEN** Function Definition 包含 `name: "ReadCodeFile"` 和中文描述：`根据文件路径读取仓库中的代码内容，返回带行号的代码文本。单次最多返回500行。`
