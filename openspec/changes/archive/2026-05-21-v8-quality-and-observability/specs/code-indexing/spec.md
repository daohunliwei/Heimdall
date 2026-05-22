## MODIFIED Requirements

### Requirement: 本地代码结构索引
系统 SHALL 对仓库中所有源代码文件进行结构化索引，**使用 AST 解析器替代正则表达式**。C# 文件 SHALL 使用 Roslyn（Microsoft.CodeAnalysis）解析；TypeScript/Go 文件 SHALL 使用 Tree-sitter 解析。索引结果 SHALL 包含：文件路径、编程语言、模块归属、AST 提取的类/函数/接口符号（含完整签名）、继承链、接口实现关系、属性注解。

#### Scenario: C# 仓库 Roslyn 解析
- **WHEN** 索引 C# 仓库中的 .cs 文件
- **THEN** 系统使用 `CSharpSyntaxTree.ParseText()` 解析每个文件，遍历 `ClassDeclarationSyntax` 和 `MethodDeclarationSyntax` 节点提取符号，同时解析 `BaseList` 获取继承链
- **AND** 不再使用正则匹配 `\.MethodName(` 模式

#### Scenario: 不支持 AST 的语言回退
- **WHEN** 仓库文件语言无对应 AST 解析器
- **THEN** 系统使用简化的正则 fallback 做符号提取，标注 Confidence=0.3，不构建调用图

#### Scenario: 跳过非代码文件
- **WHEN** 仓库包含 node_modules、.git、bin、obj 等目录
- **THEN** 索引过程跳过这些目录，不生成索引条目

### Requirement: 向量代码嵌入
系统 SHALL 对索引后的代码文件进行分块向量嵌入。分块策略 SHALL 基于 AST 节点边界精确定位：每个函数/方法/类定义对应一个独立分块，块边界由 `SyntaxNode.SpanStart` 和 `SyntaxNode.SpanEnd` 确定。

#### Scenario: AST 节点精确定位分块
- **WHEN** 代码文件包含 3 个方法定义
- **THEN** 系统按 AST 节点边界精确切割为 3 个分块，每块对应完整的方法语法树

#### Scenario: 超长函数分块
- **WHEN** AST 节点对应的方法体超过 120 行
- **THEN** 系统在 AST 子节点边界（BlockSyntax、IfStatementSyntax 等）处二次分割

#### Scenario: 非 AST 文件回退
- **WHEN** 文件无可用 AST 解析器
- **THEN** 系统回退到按 80 行固定分块

### Requirement: BM25 文本索引
系统 SHALL 为所有源代码文件构建 BM25 倒排索引。Tokenization SHALL 优先从 AST 节点标识符提取 Token，用于中文注释的分词支持、camelCase/snake_case 拆分的符号变体索引。

#### Scenario: AST 标识符优先 Token
- **WHEN** 索引 C# 文件
- **THEN** BM25 Token 从 Roslyn Syntax Tree 的 IdentifierName 节点提取，精度高于正则符号提取

## REMOVED Requirements

### Requirement: 正则符号提取
**Reason**: 正则表达式无法准确区分方法调用、字符串字面量和注释中的相似文本，导致虚假匹配和置信度下降。AST 解析器提供语法级精度，正则方案不再保留。
**Migration**: 删除 `RegexPatterns` 类和 CodeIndexService 中所有正则提取逻辑。已有数据的 code_index_entries 表在下次刷新时由 AST 分析重新填充。
