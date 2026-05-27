## Purpose

代码分析管线——涵盖 Tree-sitter AST 驱动的代码索引与分块、BM25 检索、方法级调用图构建、设计模式检测、LLM 辅助架构理解及混合检索结果注入提示词。
## Requirements
### Requirement: Tree-sitter 统一多语言 AST 解析
系统 SHALL 使用 TreeSitter.DotNet 对 13 种已配置 S-expression Query 的语言执行 AST 解析。其余 9 种语言（无 Query 定义）回退到正则方案。通过 S-expression Query 提取符号（类、函数、方法、接口）和依赖（import/using/include）。

#### Scenario: TreeSitter 统一解析
- **WHEN** 索引任意支持语言的文件
- **THEN** 系统使用 `new Parser(new Language("<lang>")).Parse(source)` 获取语法树
- **AND** 通过 S-expression Query 提取符号和依赖

#### Scenario: 不支持 AST 的语言回退
- **WHEN** 文件语言不在 tree-sitter 22 种语言映射中
- **THEN** 系统使用简化的正则 fallback 做符号提取

#### Scenario: 文件超过 100KB 时截断
- **WHEN** TreeSitterAnalyzer 解析源代码文本超过 100KB
- **THEN** 仅解析前 100KB 内容

#### Scenario: 跳过非代码目录
- **WHEN** 仓库包含 node_modules、.git、bin、obj 等目录
- **THEN** 索引过程跳过这些目录

### Requirement: AST 节点驱动的代码分块
系统 SHALL 使用 tree-sitter 语法树的一级命名子节点作为代码分块边界。每个顶级函数、类、接口声明作为一个分块单元，包含起始行号、结束行号和节点类型标签。

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
系统 SHALL 通过正则表达式匹配方法定义和方法调用模式构建方法级调用关系。调用图包含：调用者/被调用者符号及文件路径、调用类型（直接/接口/虚方法/事件）、置信度评分。

#### Scenario: C# 方法调用提取
- **WHEN** 索引 C# 仓库文件
- **THEN** 系统使用正则匹配方法定义签名和调用点，构建调用关系

#### Scenario: 跨文件调用关系
- **WHEN** ControllerA.cs 调用了 ServiceB 的已知方法
- **THEN** 通过在当前文件中搜索 `方法名(` 模式检测跨文件调用

> **注**：当前实现使用正则匹配（`CallGraphBuilder`），Roslyn SemanticModel 精确解析为后续迭代计划。

### Requirement: 设计模式启发式检测
系统 SHALL 通过类名和代码结构模式识别常见设计模式。检测基于正则匹配（如 `class\s+(\w*Factory\w*)` 识别工厂、以 `I` 开头的接口名含 `Strategy`/`Policy`/`Handler`/`Processor` 识别策略模式），配合代码结构特征验证。

#### Scenario: 工厂模式检测
- **WHEN** 类名匹配 `*Factory*` 模式
- **THEN** 系统标注为"工厂模式"

#### Scenario: 策略模式检测
- **WHEN** 接口名包含 Strategy/Policy/Handler/Processor 且有多个实现类
- **THEN** 系统标注为"策略模式"

> **注**：当前实现基于类名正则匹配（`DesignPatternDetector`），AST 节点关系方案为后续迭代方向。

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
