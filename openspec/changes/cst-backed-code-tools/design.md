## Context

`persist-versioned-ast-results` 已建立 `AstVersion` 单表持久化模型，当前 `result_json` 存储的是 `TreeSitterAnalyzer.Analyze()` 二次提取后的 `AstFileResult[]`（符号、调用边、分块、模式提示）。这种方式存在两个问题：

1. **丢失原始语法信息**：Tree-sitter 解析出的完整 CST 节点树在提取后就被丢弃，只剩下我们"认为有用"的语义投影。无法溯源到原始语法节点。
2. **提取逻辑带 bug**：`attributeAnnotations` 噪声（100+ 条冗余）、`fullSignature` 截断（被 `$"{...}"` 的 `{` 提前截断）。

正确的策略是：**以 CST 为 canonical source**，提取结果为派生数据。Tree-sitter 本身提供 `Node.ToSexp()` 方法输出完整的 S-expression 字符串，包含所有语法节点类型和字段名，结合源码即可完全恢复整个语法树。

同时，现有 LLM Tool 体系（`llm-tools` spec 中定义的 `ReadCodeFile`、`SearchSymbols`、`QueryCallGraph`、`RetrieveClassDefinition`）仍依赖文件系统实时读取和重复解析，没有消费已持久化的 AST 数据。

## Goals / Non-Goals

**Goals:**
- `AstVersion.result_json` 改为存储每个文件的 Tree-sitter 原始 CST S-expression 字符串，做到零丢失可溯源
- 轻量索引字段（`symbol_names_json`、`file_list_json`）从 CST 派生计算，保持当前的搜索能力
- 修复 `TreeSitterAnalyzer` 的 `attributeAnnotations` 噪声和 `fullSignature` 截断两个 bug
- 现有 4 个 LLM Tool 改为从 `AstVersion` 持久化数据读取，不再访问文件系统
- 新增 `lookup_file` 和 `find_usages` 两个 Tool
- Wiki 页面生成改为"轻量预注入（Top-3）+ Tool 按需查询"混合策略

**Non-Goals:**
- 不改变 Tree-sitter 的解析能力边界（语言支持、文件大小限制不变）
- 不实现前端语法树可视化页面（远期目标）
- 不改变 BM25 检索引擎的工作方式
- 不改变 `AstVersion` 的表结构和索引设计

## Decisions

### 决策 1：以 CST S-expression 为 canonical source，提取结果为派生数据

**选择**：`result_json` 存储格式从 `AstFileResult[]` 改为 `CstFileEntry[]`，每个条目包含：

```json
{
  "filePath": "...",
  "language": "csharp",
  "cst_sexp": "(compilation_unit (class_declaration ...))",
  "source_hash": "sha256...",
  "symbols": [{ ... }],
  "call_edges": [{ ... }],
  "chunks": [{ ... }],
  "design_pattern_hints": ["..."]
}
```

核心变化：新增 `cst_sexp` 和 `source_hash` 字段；symbols / call_edges / chunks / pattern_hints 保留在 JSON 中（在持久化时直接从 CST 派生，写入同一行）。

**理由**：
- S-expression 是 Tree-sitter 的官方标准格式，可用 `Node.ToSexp()` 零成本获取
- 结合源码（仓库中已有）可完全恢复语法树，无需存两次码
- symbols/edges/chunks 作为内联派生数据保留，避免读取时重复计算

**替代方案**：自定义 CST→JSON 递归序列化器（每个 AST 节点展开为嵌套 JSON，嵌入文本）。工程量大，JSON 体积膨胀严重（中型仓库可达 50MB+），且 S-expression 已是业界通用格式。

### 决策 2：S-expression 与源码分离存储

**选择**：`cst_sexp` 只存类型名和标识符文本，不复制全部源代码文本。完整代码由 `lookup_file` Tool 按需从仓库文件系统（或 git blob）读取。

**理由**：
- S-expression 中 `(identifier)` 节点的文本已在源码中，重复存储浪费空间
- 源码可能在多处被引用（不同 Tool、不同页面），存一份即可
- 存储开销可控：一个中型仓库的 CST S-expression 约 1-3 MB（远小于嵌入源码的 15-40 MB）

**替代方案**：在 CST JSON 中嵌入每个节点的完整文本。数据自包含但体积爆炸，不采用。

### 决策 3：LLM Tools 数据源切换为 AstVersion

**选择**：`ReadCodeFile`、`SearchSymbols`、`QueryCallGraph`、`RetrieveClassDefinition` 注入 `IAstVersionRepository`，从 `AstVersion.result_json` 读取数据，不再访问文件系统或实时解析。

新增 `lookup_file` 和 `find_usages`：

```
lookup_file(path, startLine?, endLine?)
  → 从 result_json 中找到目标文件的 chunks/源码
  → 返回带行号的源码片段 + 该文件的符号列表

find_usages(symbolName)
  → 扫描所有文件的 callEdges，反查被调用方
  → 返回 "谁调用了这个符号" 列表
```

**理由**：
- 数据已经在 `AstVersion.result_json` 中了（chunks 有内容、edges 有关联），不需要再解析
- Tool 调用延迟降到毫秒级（纯 JSON 内存/DB 查询），不需要文件 I/O
- 与 `llm-tools` spec 中的已有工具签名兼容

### 决策 4：混合注入策略 — Top-3 预注入 + Tool 兜底

**选择**：Wiki 页面生成时，System Prompt 中预注入 BM25 Top-3 代码分块（约 2000 tokens），LLM 在需要更多上下文时自主调用 Tool。不再注入全部 BM25 Top-20 结果。

**理由**：
- 大部分页面的 Top-3 已经覆盖核心代码上下文
- 减少不必要的 Token 消耗（每页节省 ~6000 tokens 预注入）
- Tool 定义常驻在 System Prompt 的工具列表中，不调用不消耗
- 少数复杂页面多 1-3 次 Tool 调用，延迟可控

**替代方案**：纯 Tool 模式（零预注入）。LLM 没有初始上下文锚点，第一轮输出质量不稳定。不采用。

## Risks / Trade-offs

- **[Risk] S-expression 可读性差**：CST S-expression 对非编译器开发者不够直观 -> **Mitigation**：LLM 读取时由 Tool 格式化输出源码 + 符号清单，不直接暴露 S-expression 给 LLM
- **[Risk] 历史数据不兼容**：已有 `AstVersion` 记录使用旧 `AstFileResult[]` 格式 -> **Mitigation**：`projection_format_version` 从 "1.0" → "2.0"，读取时按版本分支处理；历史数据标记为可重新解析
- **[Risk] 双重解析**：当前持久化一次解析（CST）+ Wiki 管线又一次解析（BM25 分块）-> **Mitigation**：本变更后 Wiki 管线的 `BuildSearchIndexAsync` 直接复用 `AstVersion.result_json` 中的 chunks 内容，不再调 `ChunkFile()` 重新解析
- **[Trade-off] JSON 体积**：CST S-expression 会使 `result_json` 从 ~5 MB 增加到 ~8-15 MB -> **Mitigation**：单行数据，TEXT 列可承载；查询时按需加载单个文件条目而非全量

## Migration Plan

1. 新增 `TreeSitterAnalyzer.ToCstString(Node root)` 方法，调用 `root.ToSexp()` 返回 S-expression
2. 修复 `attributeAnnotations` 和 `fullSignature` 两个 bug
3. 修改 `CodeIndexService.BuildPersistenceProjection`：改为输出 CST S-expression + 内联派生数据
4. 更新 `AstVersion.projection_format_version` 为 "2.0"
5. 改造 4 个已有 LLM Tool 数据源 + 新增 2 个 Tool
6. 调整 `WikiTaskService.BuildSearchIndexAsync`：从 `AstVersion.result_json` 读 chunks，不再调 `ChunkFile()`
7. 实现混合注入策略（Top-3 预注入，其余 Tool 兜底）
8. 测试：跑一次完整 Wiki 生成验证 CST 完整性 + Tool 可用性

## Open Questions

- S-expression 是否需要压缩存储（gzip + base64）以减小 TEXT 列体积？
- `find_usages` 是否需要跨 CST 版本查询（查询一个符号在历史版本中的调用变化）？
- 后续问答、Slides、Workshop 是否直接使用同一套 Tool 体系？
