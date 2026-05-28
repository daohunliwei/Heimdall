## Purpose

LLM Tool Call 基础设施——涵盖 FunctionInvokingChatClient 中间件集成与配置、四类代码分析工具（ReadCodeFile/SearchSymbols/QueryCallGraph/RetrieveClassDefinition）及 ToolCallConfigurationService 统一配置管理。
## Requirements
### Requirement: FunctionInvokingChatClient 集成
系统 SHALL 在所有 Provider 的 ChatClientBuilder 管道中通过 UseFunctionInvocation() 注册 FunctionInvokingChatClient 中间件，自动处理 Tool Call 往返：解析 FunctionCallContent → 执行 AIFunction → 包装结果 → 追加消息历史 → 自动循环直到 LLM 返回最终文本或达到最大轮数。

#### Scenario: 自动处理单轮 Tool Call
- **WHEN** LLM 返回 FunctionCallContent 要求调用工具
- **THEN** FunctionInvokingChatClient 自动执行工具、包装结果、追加历史、发起下一轮调用

#### Scenario: 自动处理多轮 Tool Call
- **WHEN** LLM 先调用 SearchSymbols 再调用 ReadCodeFile
- **THEN** FunctionInvokingChatClient 自动执行 2-3 轮往返，调用方只需调用一次 GetResponseAsync

#### Scenario: 达到最大轮数限制
- **WHEN** LLM 在 MaximumIterationsPerRequest（默认 8 轮）后仍要求调用工具
- **THEN** FunctionInvokingChatClient 抛出异常或返回最后一条响应

#### Scenario: 工具执行异常
- **WHEN** AIFunction 执行时抛出异常
- **THEN** FunctionInvokingChatClient 将异常信息作为 FunctionResultContent 的 Error 属性返回给 LLM，不中断循环

### Requirement: TailoredFunctionInvokingChatClient 自定义配置
系统 SHALL 配置 MaximumIterationsPerRequest=8、AllowConcurrentInvocation=true、TerminateOnUnknownCalls=true、MaximumConsecutiveErrorsPerRequest=5。

#### Scenario: 自定义最大轮数与并发
- **WHEN** Stage 5 页面生成 LLM 连续调用 6 轮工具
- **THEN** MaximumIterationsPerRequest=8 允许继续执行
- **AND** AllowConcurrentInvocation=true 允许并发工具调用

### Requirement: ToolCallConfigurationService 统一配置
系统 SHALL 以 ToolCallConfigurationService 作为唯一的 Tool Call 配置事实来源。Stage 3/Stage 5 的 Tool Call 开关 SHALL 统一通过该服务判定。

#### Scenario: 配置读取失败时统一降级
- **WHEN** SystemSetting 读取失败或缺失
- **THEN** ToolCallConfigurationService 统一返回"全部关闭"的安全降级结果

### Requirement: ReadCodeFile 工具
系统 SHALL 提供 ReadCodeFile(string filePath, int maxLines=500) 方法，从 `AstVersion` 关联的 Workspace `ast/{version_id[:8]}/files/` 目录中读取 CST 文件和源码，或不依赖 AST 时从 `workspace/repos/` 仓库路径读取原始文件。返回带行号的代码文本。单次最多返回 maxLines 行，超出截断并标注。文件不存在时返回错误提示。

#### Scenario: 成功读取 / 超行截断 / 文件不存在
- **WHEN** LLM 调用 ReadCodeFile 且文件存在 120 行 → 返回完整内容；文件 800 行 → 返回前 maxLines 行并标注截断；文件不存在 → 返回错误提示

### Requirement: SearchSymbols 工具
系统 SHALL 提供 SearchSymbols(string query, string? symbolKind=null) 方法，从 `AstVersion.symbol_names_json`（DB 列）或 `ast/{version_id[:8]}/symbols.json`（Workspace 文件）中搜索匹配符号。返回 top-10 匹配结果，包含符号名称、类型、文件路径和行号。

#### Scenario: 按类名搜索
- **WHEN** LLM 调用 SearchSymbols("IUserRepository")
- **THEN** 系统优先从 DB `symbol_names_json` 列匹配
- **AND** 返回包含符号名称、文件路径、行号、符号类型的结果列表

### Requirement: QueryCallGraph 工具
系统 SHALL 提供 QueryCallGraph(string symbolName, string direction="both") 方法，从 Workspace `ast/{version_id[:8]}/manifest.json` 中定位到对应的 CST 文件，读取调用边数据。返回调用者和被调用者列表，按置信度降序排列。

#### Scenario: 查询双向调用关系
- **WHEN** LLM 调用 QueryCallGraph("UserService.CreateUser")
- **THEN** 系统从 workspace AST 文件中读取调用边
- **AND** 返回调用者和被调用者列表，含置信度评分

### Requirement: RetrieveClassDefinition 工具
系统 SHALL 提供 RetrieveClassDefinition(string className) 方法，从 Workspace `ast/{version_id[:8]}/symbols.json` 中查找指定类。返回类的完整签名、基类/接口列表、公开方法签名、属性列表、所属文件和行号。

#### Scenario: 成功检索类定义
- **WHEN** LLM 调用 RetrieveClassDefinition("UserService")
- **THEN** 系统从 workspace symbols 文件中匹配类名
- **AND** 返回类签名、基类、接口、方法、属性、文件路径和行号

### Requirement: 工具描述元数据
每个工具方法 SHALL 使用 [Description("...")] 特性标注中文描述，通过 AIFunctionFactory.Create 自动转换为 OpenAI Function Calling 的 function.description 字段。
