## MODIFIED Requirements

### Requirement: 本地代码结构索引
系统 SHALL 对仓库中所有源代码文件进行结构化索引，**统一使用 TreeSitter.DotNet 解析所有语言**。不再使用 Roslyn 或每种语言独立的解析器。索引结果 SHALL 包含：文件路径、编程语言、模块归属、AST 提取的类/函数/接口符号（含完整签名）、继承链、接口实现关系。

#### Scenario: TreeSitter 统一解析
- **WHEN** 索引任意支持语言的文件（C#/TypeScript/JavaScript/Python/Go/Rust/Java 等 28+ 种语言）
- **THEN** 系统使用 `new Parser(new Language("<lang>")).Parse(source)` 获取语法树
- **AND** 通过 S-expression Query 提取类、方法、函数、接口等符号

#### Scenario: 不支持 AST 的语言回退
- **WHEN** 仓库文件语言不在 tree-sitter 28 种内置语法中
- **THEN** 系统使用简化的正则 fallback 做符号提取

#### Scenario: 跳过非代码文件
- **WHEN** 仓库包含 node_modules、.git、bin、obj 等目录
- **THEN** 索引过程跳过这些目录，不生成索引条目

### Requirement: 代码分块
系统 SHALL 对索引后的代码文件按 AST 节点边界进行分块。分块策略 SHALL 基于 tree-sitter 语法树的一级命名子节点作为边界：每个函数/方法/类/接口定义对应一个独立分块，块边界由节点 `StartPosition` 和 `EndPosition` 确定。

#### Scenario: AST 节点精确定位分块
- **WHEN** 代码文件包含 3 个函数定义
- **THEN** 系统按 tree-sitter 节点边界精确切割为 3 个分块，每块对应完整的函数语法树

#### Scenario: 超长函数分块
- **WHEN** tree-sitter 节点对应的函数体超过 120 行
- **THEN** 系统在该节点的二级子节点边界处二次分割

#### Scenario: 非 AST 文件回退
- **WHEN** 文件无可用 tree-sitter 解析器
- **THEN** 系统回退到按 80 行固定分块

### Requirement: BM25 文本索引
系统 SHALL 为所有源代码文件构建 BM25 倒排索引。Tokenization SHALL 优先从 tree-sitter 语法树的标识符节点提取 Token，用于中文注释的分词支持、camelCase/snake_case 拆分的符号变体索引。

#### Scenario: AST 标识符优先 Token
- **WHEN** 索引 C# 文件
- **THEN** BM25 Token 从 tree-sitter 语法树的 `identifier` 类型节点提取
