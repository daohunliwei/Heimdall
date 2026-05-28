## ADDED Requirements

### Requirement: Tree-sitter CST S-expression 输出
`TreeSitterAnalyzer` SHALL 提供 `ToCstString(Node root)` 方法，调用 `root.ToSexp()` 返回完整的 CST S-expression 字符串。该字符串 SHALL 作为 AST 持久化的 canonical source。

#### Scenario: 输出 C# 文件的 S-expression
- **WHEN** 对任一 C# 文件调用 `ToCstString(tree.RootNode)`
- **THEN** 返回以 `(compilation_unit ...)` 开头的 S-expression 字符串
- **AND** 字符串包含该文件的所有语法节点类型和标识符文本

### Requirement: 修复 attributeAnnotations 噪声
`ExtractAttributeAnnotations` SHALL 只提取直接 `attribute` 节点的完整文本，不再遍历所有后代导致参数片段被当作独立注解。

#### Scenario: 特性注解精确提取
- **WHEN** 解析带有 `[SugarTable("ast_versions")]` 和 `[SugarIndex("name", ...)]` 的 C# 类
- **THEN** `attributeAnnotations` 包含 `[SugarTable("ast_versions")]` 和 `[SugarIndex("name", ...)]` 完整文本
- **AND** 不包含 `SugarTable("ast_versions")`、`("ast_versions")`、`IsUnique = true` 等参数片段

### Requirement: 修复 fullSignature 截断
`BuildFullSignature` SHALL 使用 AST 的 `body` 字段节点起始位置定位方法体开头，而不是用纯文本 `IndexOf("{")` 匹配。当声明节点无 `body` 字段时，回退到使用第一个 `{` 文本标记。

#### Scenario: 特性注解中的插值大括号不截断签名
- **WHEN** 解析包含 `[SugarIndex("name", $"{nameof(X)},{nameof(Y)}")]` 特性的 C# 类
- **THEN** `fullSignature` 包含完整的特性列表和类声明行
- **AND** 不被 `$"{...}"` 中的 `{` 提前截断

## MODIFIED Requirements

### Requirement: AST 分析输出必须可持久化
代码分析阶段 SHALL 产出写入 Workspace 文件系统的 CST S-expression 和派生数据。该数据 SHALL 以 `workspace/ast/{version_id[:8]}/files/{hash}.cst` 文件为 canonical source，同时生成 `manifest.json` 和 `symbols.json` 索引文件。DB 中仅通过 `ast_dir_path` 记录目录路径。

#### Scenario: 代码分析产出文件系统投影
- **WHEN** 系统完成目标仓库快照下所有源文件的 Tree-sitter 分析
- **THEN** 每个文件的 CST S-expression 写入独立的 `.cst` 文件
- **AND** `manifest.json` 记录文件清单和统计数据
- **AND** `symbols.json` 记录符号、调用边、分块的轻量索引

#### Scenario: 代码索引与 CST 投影并存
- **WHEN** `CodeIndexService` 完成仓库代码索引
- **THEN** 系统既保留当前代码索引所需摘要
- **AND** 同时产出写入 workspace 的 CST 持久化投影数据
