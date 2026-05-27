## Purpose

代码分析管线——涵盖 Tree-sitter AST 驱动的代码索引与分块、BM25 检索、方法级调用图构建、设计模式检测、LLM 辅助架构理解及混合检索结果注入提示词
## Requirements
### Requirement: Tree-sitter 统一多语言 AST 解析
系统 SHALL 使用 TreeSitter.DotNet 对已配置 Query 的语言执行 AST 解析，并通过 `TreeSitterAnalyzer` 提供统一入口。Language 实例 SHALL 通过 `new Language(id)` 创建，并缓存到 `ConcurrentDictionary<string, Language>` 中。对无 Query 的语言允许回退到正则方案，但已配置语言不得再因错误的 language id 映射而静默回退。

#### Scenario: TreeSitter 统一解析
- **WHEN** 索引任意支持语言的文件
- **THEN** 系统使用 `new Parser(language).Parse(source)` 获取语法树
- **AND** 通过 S-expression Query 提取符号、依赖与调用边

#### Scenario: 不支持 AST 的语言回退
- **WHEN** 文件语言无对应 Tree-sitter Query 配置
- **THEN** 系统使用简化的正则 fallback 做符号提取

#### Scenario: 文件超过 100KB 时截断
- **WHEN** TreeSitterAnalyzer 解析源代码文本超过 100KB
- **THEN** 仅解析前 100KB 内容

#### Scenario: 跳过非代码目录
- **WHEN** 仓库包含 node_modules、.git、bin、obj 等目录
- **THEN** 索引过程跳过这些目录

### Requirement: 完整符号提取
系统 SHALL 基于 Query capture 的父节点提取完整符号数据，而不是仅提取 `identifier`。`AstSymbol` SHALL 填充 Name、Kind、FullSignature、FilePath、StartLine、EndLine、ParentClass、Modifiers、BaseTypes、AttributeAnnotations 10 个字段。Kind SHALL 来源于声明节点类型，不能再返回 `"identifier"`。

#### Scenario: C# 类声明完整提取
- **WHEN** 解析 `public class UserService : BaseService, IUserService`
- **THEN** 系统返回 Name=`UserService`、Kind=`class`、ParentClass=`BaseService`
- **AND** BaseTypes 包含 `IUserService`
- **AND** Modifiers 包含 `public`

#### Scenario: C# 方法声明完整提取
- **WHEN** 解析 `public async Task<User> CreateUser(string name, string email)`
- **THEN** 系统返回 Kind=`method`
- **AND** FullSignature 包含返回类型、方法名和参数列表
- **AND** ParentClass 为所在类名
- **AND** StartLine 与 EndLine 为真实行号

### Requirement: AST 节点驱动的代码分块
系统 SHALL 使用声明级 AST 节点作为代码分块边界。分块 SHALL 返回起始行号、结束行号、节点标签与原始内容，并跳过过短块与纯 import/using 块。

#### Scenario: AST 节点精确定位分块
- **WHEN** 代码文件包含 3 个函数定义
- **THEN** 系统按 tree-sitter 节点边界精确切割为 3 个分块

#### Scenario: 超长函数分块
- **WHEN** tree-sitter 节点对应的函数体超过 120 行
- **THEN** 系统在二级子节点边界处二次分割

#### Scenario: 非 AST 文件回退
- **WHEN** 文件无可用 tree-sitter 解析器
- **THEN** 系统回退到按 80 行固定分块

### Requirement: BM25 文本索引
系统 SHALL 为所有源代码文件构建 BM25 倒排索引，Tokenization 优先从 tree-sitter 语法树的标识符节点提取 Token，支持 camelCase/snake_case 拆分。

#### Scenario: AST 标识符优先 Token
- **WHEN** 索引 C# 文件
- **THEN** BM25 Token 从 tree-sitter 语法树的 identifier 类型节点提取

### Requirement: 混合检索与结果注入
系统 SHALL 以 BM25 作为代码检索主链路，检索结果格式化后注入 Wiki 页面生成提示词。注入量由 ContextPackingService 根据模型上下文窗口动态决定。搜索结果 SHALL 在同一次 Wiki 生成任务中缓存。

#### Scenario: 基于 BM25 的代码检索
- **WHEN** 页面生成或问答需要搜索代码片段
- **THEN** 系统执行 BM25 检索并返回按相关度排序的结果

#### Scenario: 动态检索量调整
- **WHEN** 使用 128K 上下文模型
- **THEN** 代码片段注入量可达 70K+ tokens；使用 8K 模型时自动缩减至 ~3K tokens

#### Scenario: 搜索缓存命中
- **WHEN** 同一任务内两个页面使用相同的搜索 query
- **THEN** 第二次搜索直接返回缓存结果

### Requirement: 方法级调用图构建
系统 SHALL 通过 `TreeSitterAnalyzer.ExtractCallEdges` 提取方法级调用关系。调用边 SHALL 基于调用表达式 Query 匹配，向上遍历最近的方法或函数声明作为调用者，并提取被调用函数标识符。调用图包含调用者、被调用者、文件路径、调用类型与置信度。

#### Scenario: C# 方法调用提取
- **WHEN** 索引 C# 仓库文件
- **THEN** 系统使用 `invocation_expression` Query 提取方法调用
- **AND** 同文件调用的置信度不低于 `0.9`

#### Scenario: 跨文件调用关系
- **WHEN** ControllerA.cs 调用了 ServiceB 的已知方法
- **THEN** 系统结合 import 或 using 依赖推定目标文件路径
- **AND** 无法解析目标文件时仍保留调用边但文件路径可为空

### Requirement: 设计模式启发式检测
系统 SHALL 由 `TreeSitterAnalyzer` 基于 AST 节点关系输出设计模式提示，不再依赖独立的正则类。系统 SHALL 支持至少 Factory、Strategy、Observer、Singleton、Builder、Repository、Mediator 7 种模式，并将结果写入 `DesignPatternHints`。

#### Scenario: 工厂模式检测
- **WHEN** 类名包含 `Factory` 且声明体内存在 `object_creation_expression`
- **THEN** 系统标注为"工厂模式"

#### Scenario: 策略模式检测
- **WHEN** 接口名包含 Strategy/Policy/Handler/Processor 且有多个实现类
- **THEN** 系统标注为"策略模式"

### Requirement: LLM 辅助架构理解（Stage 3）
系统 SHALL 在 AST 分析完成后执行 1-2 次 LLM 调用进行高级架构理解。当 ToolCall.Stage3.Enabled 为 true 时，通过 ChatOptions.Tools 绑定 QueryCallGraph 和 RetrieveClassDefinition，由 FunctionInvokingChatClient 自动处理工具交互。

#### Scenario: 识别分层架构
- **WHEN** AST 分析显示 Controller/Service/Repository 分层特征
- **THEN** LLM 基于 AST 数据确认为分层架构并输出各层职责

#### Scenario: Tool Call 辅助解析复杂模式
- **WHEN** 方法实现通过多层继承间接获得接口方法签名
- **THEN** LLM 通过 FunctionInvokingChatClient 调用 RetrieveClassDefinition 获取各层定义

#### Scenario: Tool Call 未启用时降级
- **WHEN** ToolCall.Stage3.Enabled 为 false
- **THEN** ChatOptions.Tools 为 null，行为与不使用 UseFunctionInvocation 时一致
