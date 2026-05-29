## ADDED Requirements

### Requirement: AST 解析结果存储到 Workspace 文件
AST 解析的持久化投影 SHALL 写入 Workspace `ast/{ast_version_id[:8]}/` 目录下的文件系统，而不是存入 DB `result_json` TEXT 列。目录 SHALL 包含 `manifest.json`（文件清单与统计）、`files/{file_hash}.cst`（单文件 CST S-expression）、`symbols.json`（轻量符号索引）。

#### Scenario: AST 解析后写文件
- **WHEN** `AstPersistenceService` 完成仓库全量 AST 解析
- **THEN** 结果写入 `{workspace}/ast/{ast_version_id[:8]}/` 目录
- **AND** `manifest.json` 包含 `total_files`、`total_symbols`、`total_call_edges`、`total_chunks`
- **AND** 每个文件的 CST S-expression 写入 `files/{sha256[:16]}.cst`
- **AND** `symbols.json` 包含符号名、类型和文件路径的轻量索引

#### Scenario: 读取 AST 数据
- **WHEN** 下游服务需要加载 AST 结果
- **THEN** 系统根据 `ast_dir_path` 定位 workspace 目录
- **AND** 从 `manifest.json` 读取统计信息
- **AND** 按需读取单个文件的 `.cst` 文件

#### Scenario: 文件缺失触发重新生成
- **WHEN** `ast_dir_path` 指向的目录不存在或关键文件缺失
- **THEN** 系统触发 `AstPersistenceService` 重新解析
- **AND** 重新写入 workspace 文件并更新 DB

### Requirement: DB 中保留 AST 元数据和轻量索引
`AstVersion` 实体 SHALL 保留 `symbol_names_json` 和 `file_list_json` 轻量索引字段在 DB 中，支持无需文件 I/O 的快速符号搜索。`result_json` 列 SHALL 改为 `ast_dir_path`（VARCHAR），指向 workspace 中的 AST 数据目录。

#### Scenario: 符号搜索不触发文件 I/O
- **WHEN** LLM Tool `SearchSymbols` 执行符号搜索
- **THEN** 系统直接从 DB 的 `symbol_names_json` 列匹配
- **AND** 不需要读取 workspace 文件
