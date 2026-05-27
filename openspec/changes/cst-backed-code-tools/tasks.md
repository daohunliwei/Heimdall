## 0. 前置依赖

- [ ] 0.1 确认 `workspace-filesystem` 变更已实施——`WorkspaceService` 可用，`ast_dir_path` 字段已存在于 DB

## 1. TreeSitterAnalyzer 修复与 CST 能力

- [ ] 1.1 修复 `ExtractAttributeAnnotations`：只提取直接 `attribute` 节点文本，消除参数片段噪声
- [ ] 1.2 修复 `BuildFullSignature`：改用 AST `body` 字段节点定位方法体，消除插值大括号截断
- [ ] 1.3 新增 `ToCstString(Node root)` 方法，封装 `root.ToSexp()` 返回完整 S-expression 字符串

## 2. CST 文件存储

- [ ] 2.1 修改 `CodeIndexService.BuildPersistenceProjection`：为每个文件生成 `.cst` 文件写入 `workspace/ast/{id[:8]}/files/{hash}.cst`
- [ ] 2.2 生成 `manifest.json`（文件清单 + 统计）和 `symbols.json`（轻量符号索引）写入 AST 目录
- [ ] 2.3 更新 `AstPersistenceService`：`projection_format_version` 改为 "2.0"，`ast_dir_path` 写入 DB

## 3. Bug 修复验证

- [ ] 3.1 更新 `AstPersistenceTests`：新增 CST S-expression 完整性断言、attributeAnnotations 去噪断言、fullSignature 不截断断言
- [ ] 3.2 对 Heimdall 自身仓库重新跑分析，验证 workspace 文件输出和 bug 修复生效

## 4. LLM 代码 Tool 改造

- [ ] 4.1 新建 `AstBackedCodeToolService`，注入 `WorkspaceService` 和 `IAstVersionRepository`
- [ ] 4.2 改造 `ReadCodeFile`：从 `workspace/repos/` 读取源文件
- [ ] 4.3 改造 `SearchSymbols`：从 DB `symbol_names_json` 列匹配，必要时从 `workspace/ast/` 补充
- [ ] 4.4 改造 `QueryCallGraph`：从 `workspace/ast/{id}/files/*.cst` 读取调用边
- [ ] 4.5 改造 `RetrieveClassDefinition`：从 `workspace/ast/{id}/symbols.json` 查找类定义
- [ ] 4.6 新增 `lookup_file` Tool：workspace repos 源文件 + symbols.json 符号摘要
- [ ] 4.7 新增 `find_usages` Tool：遍历 `.cst` 文件反查调用者

## 5. Wiki 管线混合注入

- [ ] 5.1 修改 `BuildSearchIndexAsync`：从 `workspace/ast/{id}/manifest.json` 读取 chunks 构建 BM25 索引，替代 `ChunkFile()` 实时解析
- [ ] 5.2 实现混合注入策略：`FormatForPrompt` 只注入 Top-3 分块（~2000 tokens）
- [ ] 5.3 确保 Tool 定义正确注册到页面生成 LLM 的 `ChatOptions.Tools` 中

## 6. 端到端验证

- [ ] 6.1 对 Heimdall 自身仓库跑完整 AST 持久化，验证 `workspace/ast/` 目录结构完整
- [ ] 6.2 验证 Wiki 生成管线可正常完成（workspace CST + BM25 重用 + 混合注入）
- [ ] 6.3 运行全量后端测试，确认不破坏现有功能
