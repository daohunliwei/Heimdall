## ADDED Requirements

### Requirement: 基于 TreeSitter.DotNet 的统一多语言 AST 解析
系统 SHALL 使用 `TreeSitter.DotNet` NuGet 包替代自研 `IAstAnalyzer` 接口和 `RoslynCSharpAnalyzer` 实现，对所有代码文件执行真实语法树解析。

#### Scenario: 加载 TreeSitter 语言语法
- **WHEN** 系统需要解析一个 `.ts` 文件
- **THEN** TreeSitterAnalyzer 通过 `new Language("TypeScript")` 加载对应语法
- **AND** 如果 tree-sitter 内置 28 种语法不包含该语言，回退到正则解析

#### Scenario: 解析源代码为语法树
- **WHEN** TreeSitterAnalyzer 解析源代码文本
- **THEN** 调用 `new Parser(language).Parse(source)` 返回语法树
- **AND** 文件超过 100KB 时，仅解析前 100KB 内容

### Requirement: S-expression Query 驱动的符号提取
系统 SHALL 使用 tree-sitter S-expression Query 从语法树中提取符号（函数、类、方法、接口、类型定义），每种语言配置独立的 Query 字符串。

#### Scenario: 提取 C# 符号
- **WHEN** 解析 C# 文件
- **THEN** 使用 Query `(class_declaration name: (identifier) @name) (method_declaration name: (identifier) @name)` 提取类和方法名
- **AND** 结果以 `List<string>` 返回，上限 100 个符号

#### Scenario: 提取 TypeScript 符号
- **WHEN** 解析 TypeScript 文件
- **THEN** 使用 TypeScript 对应的 Query 提取 `function_declaration`、`class_declaration`、`method_definition`、`interface_declaration`、`export_statement`

### Requirement: S-expression Query 驱动的依赖提取
系统 SHALL 使用 tree-sitter S-expression Query 从语法树中提取依赖（import/using/include 声明）。

#### Scenario: 提取 import 依赖
- **WHEN** 解析 TypeScript 或 Python 文件
- **THEN** 使用语言对应的 import Query 提取导入模块名
- **AND** 结果以 `List<string>` 返回，上限 30 条

### Requirement: 语法节点驱动的代码分块
系统 SHALL 使用 tree-sitter 语法树的一级命名子节点作为代码分块边界，替代当前基于正则关键词（class/function/def）的行号检测。

#### Scenario: 按顶级声明分块
- **WHEN** 解析任意支持语言的文件
- **THEN** 每个顶级函数、类、接口声明作为一个分块单元
- **AND** 分块包含起始行号、结束行号和节点类型标签

### Requirement: 语言识别映射
系统 SHALL 维护 `DetectLanguage` 返回值到 tree-sitter 语法名的映射表，确保所有 28+ 内置语言正确关联。

#### Scenario: 语言映射查找
- **WHEN** `DetectLanguage` 返回 "typescript"
- **THEN** TreeSitterAnalyzer 使用 `new Language("TypeScript")` 加载语法
- **AND** 映射表包含 CSharp/TypeScript/JavaScript/Python/Go/Rust/Java/Haskell/PHP/Ruby/Swift/Scala/C/C++ 等

### Requirement: 正则回退路径
对于 tree-sitter 不支持的编程语言，系统 SHALL 保留现有的正则表达式符号/依赖提取逻辑作为回退。

#### Scenario: 不支持语言的回退
- **WHEN** 文件语言为 SQL 或 Markdown（不在 tree-sitter 28 种语法中）
- **THEN** 系统使用现有正则逻辑提取符号和依赖
- **AND** 不影响索引流程的其他阶段
