## ADDED Requirements

### Requirement: CST S-expression 文件存储（canonical source，不可丢弃）
系统 SHALL 在 AST 持久化时，对每个已解析的源码文件，将 Tree-sitter **原始** CST 的完整 S-expression 写入 `workspace/ast/{ast_version_id[:8]}/files/{source_sha256[:16]}.cst` 文件。S-expression SHALL 为 `root.ToString()` 的原始输出，**不经过任何 JSON 序列化或结构化提取**。此文件是解析结果的 canonical source——原始语法树信息必须 100% 保留，不可丢弃。

**同时**，解析后的结构化数据（`AstFileResult` —— 符号、调用边、分块）SHALL 保留在 `symbols.json` 和 `manifest.json` 索引文件中，并同步存入 DB 的 `symbol_names_json`、`file_list_json`、`result_json` 列。原始 CST 与解析结果 **两者都必须完整保留**。

#### Scenario: 单文件 CST 写入
- **WHEN** 系统完成某个 C# 文件的 Tree-sitter 解析
- **THEN** 对应 `.cst` 文件写入 `workspace/ast/{version_id[:8]}/files/` 目录
- **AND** 文件内容以 `(compilation_unit ...)` 为根节点
- **AND** 文件名 = 源码 SHA256 前 16 位十六进制

#### Scenario: 同源码跨版本去重
- **WHEN** 同一源码文件被两个不同 AST 版本解析
- **THEN** 两个版本使用相同的 SHA256 文件名
- **AND** 第二次解析时直接覆盖（幂等写入）

#### Scenario: 不支持 Tree-sitter 的语言
- **WHEN** 文件语言无 Tree-sitter Query 配置
- **THEN** 系统不生成该文件的 `.cst` 文件
- **AND** 降级路径的提取结果仍写入 `manifest.json`

### Requirement: Workspace 目录下 manifest 与索引文件
AST 持久化 SHALL 在 `workspace/ast/{ast_version_id[:8]}/` 目录下生成 `manifest.json`（文件清单 + 统计数据）和 `symbols.json`（轻量符号索引）。

#### Scenario: manifest.json 内容
- **WHEN** AST 持久化完成
- **THEN** `manifest.json` 包含 `total_files`、`total_symbols`、`total_call_edges`、`total_chunks`
- **AND** 包含 `files` 数组，每个元素为 `{path, language, sha256, symbol_count}`

#### Scenario: symbols.json 内容
- **WHEN** AST 持久化完成
- **THEN** `symbols.json` 包含所有文件的符号摘要列表
- **AND** 每条记录为 `{name, kind, file, startLine, endLine}`

### Requirement: CST 版本格式标识
系统 SHALL 通过 `projection_format_version` 区分 CST 文件存储格式版本。当格式从提取模式（1.0）升级到 CST 文件模式（2.0）后，DB 中 `ast_dir_path` 非空，workspace 目录中包含 `.cst` 文件。

#### Scenario: 版本正确标识
- **WHEN** 系统以 CST 文件模式持久化 AST 版本
- **THEN** `projection_format_version` 为 "2.0"
- **AND** `ast_dir_path` 指向 `workspace/ast/{version_id[:8]}/`

#### Scenario: 旧版本无 workspace 目录
- **WHEN** 读取 `projection_format_version` 为 "1.0" 的历史 AST 版本
- **THEN** `ast_dir_path` 可能为空
- **AND** 系统按旧格式（DB `result_json`）降级读取
