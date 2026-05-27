## Why

当前 `persist-versioned-ast-results` 已建立 AST 版本化持久化底座，但存储的是二次提取后的结构化数据（`AstFileResult`），Tree-sitter 的原始 CST（Concrete Syntax Tree）内容被丢弃，导致无法溯源码级精度的语法信息。同时现有 LLM 代码工具（`ReadCodeFile`、`QueryCallGraph` 等）仍依赖实时文件读取和重复解析，没有利用已持久化的 AST 数据。本次变更完成两个关键升级：原始 CST 存储保证数据不可丢失，以及基于持久化数据的 LLM 按需代码查询 Tool 体系。

## What Changes

- 将 CST S-expression 写入 `workspace/ast/{version_id}/files/{hash}.cst` 文件，DB 只记录 `ast_dir_path`（由 `workspace-filesystem` 变更提供）
- 保留轻量索引字段（`symbol_names_json`、`file_list_json`）在 DB，同步从 CST 派生
- 修复 `TreeSitterAnalyzer` 中 `attributeAnnotations` 噪声和 `fullSignature` 截断两个 bug，并增加 `ToCstString()` 方法输出原始 S-expression
- 改造现有 4 个 LLM 代码 Tool（`ReadCodeFile`、`SearchSymbols`、`QueryCallGraph`、`RetrieveClassDefinition`），使其从 Workspace 文件和 DB 轻量索引读取，而非实时解析文件系统
- 实现混合注入策略：Wiki 页面生成时预注入 BM25 Top-3 关键代码 + LLM 按需自主调用 Tool 扩展上下文
- 新增 `lookup_file` 和 `find_usages` 两个 Tool，暴露 CST 级别的文件内容查询和符号引用反查能力

## Capabilities

### New Capabilities
- `cst-persistence`: Tree-sitter 原始 CST（S-expression）的持久化存储、从 CST 派生轻量索引、以及 CST 级别的源码溯源能力

### Modified Capabilities
- `code-analysis`: AST 分析不再只产出提取后的结构化数据，还必须产出原始 CST；代码分析结果以 CST 为 canonical source
- `llm-tools`: 现有 4 个工具改为从 AstVersion 持久化数据读取；新增 `lookup_file` 和 `find_usages` 两个 Tool
- `wiki-generation-pipeline`: 页面生成采用"轻量预注入 + Tool 按需查询"混合策略，减少每页注入 Token 量

## Impact

- **数据模型**: 无表结构变更（`ast_dir_path` 已由 `workspace-filesystem` 提供）；轻量索引字段保留在 DB
- **TreeSitterAnalyzer**: 新增 `ToCstString()` 方法输出原始 S-expression；修复 `attributeAnnotations` 和 `fullSignature` bug
- **LLM Tools**: 工具实现从文件系统实时解析改为从 Workspace 文件 + DB 轻量索引读取
- **WikiTaskService**: 预注入策略从"注入全部 BM25 结果"改为"Top-3 + Tool 兜底"；`BuildSearchIndexAsync` 从 workspace ast 目录读 chunks
- **依赖**: **必须先实施 `workspace-filesystem` 变更**，确保 `WorkspaceService` 和 `ast_dir_path` 字段已就绪
