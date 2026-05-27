## ADDED Requirements

### Requirement: CST S-expression 原始存储
系统 SHALL 在 AST 持久化时，对每个已解析的源码文件保存其 Tree-sitter 原始 CST 的 S-expression 字符串。S-expression SHALL 通过 `Node.ToSexp()` 方法获取，包含完整语法节点类型、字段名和标识符文本，不丢失任何语法结构信息。

#### Scenario: 单文件 CST 存储
- **WHEN** 系统完成某个 C# 文件的 Tree-sitter 解析
- **THEN** `result_json` 中该文件对应的条目包含 `cst_sexp` 字段
- **AND** `cst_sexp` 内容以 `(compilation_unit ...)` 为根节点
- **AND** 包含 `class_declaration`、`method_declaration`、`property_declaration` 等所有语法节点

#### Scenario: 不支持 Tree-sitter 的语言
- **WHEN** 文件语言无 Tree-sitter Query 配置
- **THEN** 系统不生成该文件的 `cst_sexp`
- **AND** `cst_sexp` 字段为 null 或不存在
- **AND** 降级路径的正则提取结果仍保存在 `symbols` 数组中

#### Scenario: 解析失败时的降级
- **WHEN** Tree-sitter 解析过程中抛出异常
- **THEN** 该文件的 `cst_sexp` 为 null
- **AND** 错误信息记录在文件的 `error` 字段中
- **AND** 不阻断其他文件的 CST 持久化

### Requirement: CST 派生轻量索引
系统 SHALL 在持久化时从 CST 同步派生 `symbol_names_json` 和 `file_list_json` 轻量索引字段，不在读取时重复解析 CST。

#### Scenario: 从 CST 同步派生符号索引
- **WHEN** 系统持久化 AST 版本
- **THEN** `symbol_names_json` 从 CST 的声明节点提取符号名、类型和文件路径
- **AND** `file_list_json` 从 CST 文件列表提取路径、语言和符号数
- **AND** 派生过程与现有 `AstFileResult` 提取逻辑一致

### Requirement: CST 版本格式标识
系统 SHALL 通过 `projection_format_version` 区分 CST 存储格式版本。当格式从提取模式（1.0）升级到 CST 模式（2.0）后，读取方 SHALL 能根据版本号选择正确的反序列化路径。

#### Scenario: 版本正确标识
- **WHEN** 系统以 CST 模式持久化 AST 版本
- **THEN** `projection_format_version` 为 "2.0"
- **AND** `config_fingerprint` 基于 "2.0" 计算

#### Scenario: 旧版本兼容读取
- **WHEN** 读取方请求 `projection_format_version` 为 "1.0" 的历史 AST 版本
- **THEN** 系统按 `AstFileResult[]` 格式反序列化 `result_json`
- **AND** `cst_sexp` 相关字段返回 null
