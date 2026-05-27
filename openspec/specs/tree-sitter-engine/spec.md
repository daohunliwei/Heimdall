## Purpose

统一的 Tree-sitter AST 引擎能力规范——涵盖多语言 native library 加载、语法树解析、完整符号提取、方法级调用边提取、AST 节点驱动的代码分块，以及面向真实代码的基础验证要求。该能力由 `TreeSitterAnalyzer` 承载，作为 `code-analysis` 等上层能力的底层引擎。

## Requirements

### Requirement: Native Library 正确加载
系统 SHALL 通过 `TreeSitterAnalyzer` 为已配置语言创建并缓存 `Language` 实例。创建过程 SHALL 优先使用 `new Language(id)` 的官方入口，并对标准名、蛇形名、连字符名等注册名进行受控探测；成功结果 SHALL 缓存到 `ConcurrentDictionary<string, Language>`，失败时 SHALL 抛出包含语言名与已尝试注册名的明确异常，不得静默降级为正则解析。

#### Scenario: C# Language 加载成功
- **WHEN** `TreeSitterAnalyzer` 首次解析 C# 文件
- **THEN** 系统成功加载 `tree-sitter-c-sharp` 对应语法
- **AND** 返回可复用的 `Language` 实例

#### Scenario: Language 缓存命中
- **WHEN** 同一语言被再次解析
- **THEN** 系统直接从 `ConcurrentDictionary` 返回已缓存的 `Language` 实例

#### Scenario: Native 加载失败时明确报错
- **WHEN** 所有候选注册名均加载失败
- **THEN** 系统抛出 `InvalidOperationException`
- **AND** 异常消息包含语言名与尝试过的注册名列表

### Requirement: 统一多语言 AST 解析
系统 SHALL 使用 `TreeSitter.DotNet` 对已配置 Query 的语言执行统一 AST 解析，并通过 `new Parser(language).Parse(source)` 生成语法树。对于未配置 Query 的语言，系统 MAY 回退到受限的正则路径；但已配置语言不得因 language id 映射错误而回退。

#### Scenario: 已配置语言执行 AST 解析
- **WHEN** 解析任意已配置 Query 的代码文件
- **THEN** 系统使用对应 `Language` 创建 `Parser`
- **AND** 返回可供后续 Query 提取的语法树

#### Scenario: 文件超过 100KB 时截断
- **WHEN** 输入源代码文本超过 100KB
- **THEN** 系统仅解析前 100KB 内容

### Requirement: 完整符号提取
`ExtractSymbolsFromTree` SHALL 基于 Query capture 的父节点提取 `AstSymbol` 的全部 10 个字段：`Name`、`Kind`、`FullSignature`、`FilePath`、`StartLine`、`EndLine`、`ParentClass`、`Modifiers`、`BaseTypes`、`AttributeAnnotations`。`Kind` SHALL 来源于声明父节点类型，而不是 capture 节点本身；`ParentClass` 与 `BaseTypes` SHALL 来自 `base_list` 等结构节点；`Modifiers` SHALL 来自访问修饰符或修饰符节点；行号与文件路径 SHALL 来源于 AST 节点位置信息。

#### Scenario: C# 类完整符号提取
- **WHEN** 解析 `public class UserService : BaseService, IUserService`
- **THEN** 系统返回 `Name=UserService`
- **AND** `Kind=class`
- **AND** `ParentClass=BaseService`
- **AND** `BaseTypes` 包含 `IUserService`
- **AND** `Modifiers` 包含 `public`

#### Scenario: C# 方法完整符号提取
- **WHEN** 解析 `public async Task<User> CreateUser(string name, string email)`
- **THEN** 系统返回 `Kind=method`
- **AND** `FullSignature` 包含返回类型、方法名与参数列表
- **AND** `ParentClass` 为所在类名
- **AND** `StartLine` 与 `EndLine` 为真实行号

#### Scenario: 符号 Kind 不再返回 identifier
- **WHEN** 提取任意支持语言的符号
- **THEN** `Kind` 为声明节点语法类型对应的标准化结果
- **AND** 不返回 `identifier`

### Requirement: 方法级调用边提取
`ExtractCallEdges` SHALL 基于调用表达式 Query 提取方法级调用关系，包括但不限于 C# 的 `invocation_expression`、TypeScript/JavaScript 的 `call_expression`、Python 的 `call`、Go 的 `call_expression` 与 Java 的 `method_invocation`。对每个调用节点，系统 SHALL 向上遍历最近的方法或函数声明作为调用者，并提取被调用函数标识符；同文件调用置信度 SHALL 不低于 `0.9`，跨文件基于 import 或 using 推断的调用置信度 SHALL 不低于 `0.7`。

#### Scenario: C# 同文件方法调用
- **WHEN** `CreateUser` 方法体内存在 `_validator.Validate(email)` 调用
- **THEN** 系统产出一条 `AstCallEdge`
- **AND** `CallerSymbol=CreateUser`
- **AND** `CalleeSymbol=Validate`
- **AND** `Confidence=0.9`

#### Scenario: 跨文件调用解析
- **WHEN** 控制器方法调用服务类方法且 import 或 using 可解析到目标文件
- **THEN** 系统产出包含 `CalleeFilePath` 的 `AstCallEdge`
- **AND** `Confidence` 不低于 `0.7`

#### Scenario: 无法确定目标文件时保留调用边
- **WHEN** 系统只能识别被调用函数名但无法解析目标文件
- **THEN** 系统仍保留调用边
- **AND** 目标文件路径允许为空

### Requirement: AST 节点驱动的代码分块
`ExtractChunksFromTree` SHALL 以顶级声明节点和关键声明节点作为代码分块边界，返回 `StartLine`、`EndLine`、`Label` 与原始内容。系统 SHALL 跳过长度过短的块与纯 import 或 using 块；当单个节点过长时，系统 MAY 在二级子节点边界做进一步分割。

#### Scenario: 顶级声明精确分块
- **WHEN** 文件包含多个类、接口或函数声明
- **THEN** 系统按 AST 节点边界生成多个代码块
- **AND** 每个块的 `Label` 对应声明节点类型

#### Scenario: 纯导入块不参与分块
- **WHEN** AST 节点仅包含 import 或 using 声明
- **THEN** 系统跳过该节点

### Requirement: 面向真实代码的基础验证
系统 SHALL 通过测试与真实文件扫描验证引擎可用性。对非空代码样例，测试 SHALL 验证 `Symbols.Count > 0`、关键字段填充率达标、`Kind` 不为 `identifier`、含调用代码时 `CallEdges > 0`、`Chunks >= 1` 且 `Label` 非空；真实项目扫描 SHALL 能输出可观测的符号数、调用边数与分块结果。

#### Scenario: C# 完整测试
- **WHEN** 对包含类、方法与方法调用的 C# 代码运行 `Analyze`
- **THEN** `Symbols > 0`
- **AND** `Kind` 包含 `class` 与 `method`
- **AND** `ParentClass`、`FullSignature` 等关键字段有值
- **AND** `CallEdges > 0`

#### Scenario: 空代码返回空结果
- **WHEN** 传入空字符串
- **THEN** `Symbols=0`
- **AND** `CallEdges=0`
- **AND** `Chunks=0`
