## 1. TreeSitterAnalyzer 修复与 CST 能力

- [ ] 1.1 修复 `ExtractAttributeAnnotations`：只提取直接 `attribute` 节点文本，跳过子节点，消除参数片段噪声
- [ ] 1.2 修复 `BuildFullSignature`：改用 AST `body` 字段节点定位方法体，回退时才用 `{` 文本匹配，消除插值大括号截断
- [ ] 1.3 新增 `ToCstString(Node root)` 方法，封装 `root.ToSexp()` 返回完整 S-expression 字符串

## 2. 持久化格式升级

- [ ] 2.1 新增 `CstFileEntry` 模型类，包含 `filePath`、`language`、`cst_sexp`、`source_hash` 及内联 `symbols`/`call_edges`/`chunks`/`design_pattern_hints`
- [ ] 2.2 修改 `CodeIndexService.BuildPersistenceProjection`：输出 `CstFileEntry[]` 替代 `AstFileResult[]`，同步调用 `ToCstString()` 和现有提取逻辑
- [ ] 2.3 更新 `AstPersistenceService`：`projection_format_version` 改为 "2.0"，`config_fingerprint` 基于 "2.0" 重新计算
- [ ] 2.4 新增 `result_json` 版本化读取逻辑——按 `projection_format_version` 选择反序列化目标类型

## 3. Bug 修复验证

- [ ] 3.1 更新 `AstPersistenceTests`：新增 CST S-expression 完整性断言、attributeAnnotations 去噪断言、fullSignature 不截断断言
- [ ] 3.2 对 Heimdall 自身仓库重新跑 `AstRealRepoTest`，验证 CST S-expression 输出和 bug 修复生效

## 4. LLM 代码 Tool 改造

- [ ] 4.1 新建 `AstBackedCodeToolService`，注入 `IAstVersionRepository`，从 `AstVersion.result_json` 提供数据查询方法
- [ ] 4.2 改造 `ReadCodeFile`：从 `result_json` 的 chunks 数组读取文件内容，不再访问文件系统
- [ ] 4.3 改造 `SearchSymbols`：从 `result_json` 的 symbols 数组搜索匹配符号，不再依赖 `IHybridSearchService`
- [ ] 4.4 改造 `QueryCallGraph`：从 `result_json` 的 call_edges 数组查询调用边，不再依赖 `DependencyTopologyService`
- [ ] 4.5 改造 `RetrieveClassDefinition`：从 `result_json` 的 symbols 数组查找类定义，不再访问文件系统
- [ ] 4.6 新增 `lookup_file` Tool：支持按文件路径 + 可选行范围查询 chunks 和符号摘要
- [ ] 4.7 新增 `find_usages` Tool：支持按符号名反查所有调用者
- [ ] 4.8 更新 DI 注册，确保所有 Tool 注入 `IAstVersionRepository`

## 5. Wiki 管线混合注入

- [ ] 5.1 修改 `BuildSearchIndexAsync`：从 `AstVersion.result_json` 的 chunks 读取数据构建 BM25 索引，替代 `ChunkFile()` 实时解析
- [ ] 5.2 实现混合注入策略：`FormatForPrompt` 只注入 Top-3 分块（~2000 tokens），超出部分不注入
- [ ] 5.3 确保 Tool 定义正确注册到页面生成 LLM 的 `ChatOptions.Tools` 中
- [ ] 5.4 Tool 未启用时的降级路径：预注入放大到 Top-5，关闭 Tool 注册

## 6. 端到端验证

- [ ] 6.1 对 Heimdall 自身仓库跑一次完整 AST 持久化，验证 `projection_format_version`="2.0" 且 `cst_sexp` 非空
- [ ] 6.2 验证 Wiki 生成管线可正常完成（CST 模式 AST + BM25 重用 + 混合注入）
- [ ] 6.3 运行全量后端测试，确认不破坏现有功能
