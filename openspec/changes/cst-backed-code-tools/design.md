## Context

`persist-versioned-ast-results` 已建立 `AstVersion` 单表持久化模型和管线集成点。`workspace-filesystem` 变更提供了 Workspace 文件系统底座（`HEIMDALL_WORKSPACE`），已将大型数据从 DB TEXT 列迁移到 `workspace/ast/`、`workspace/wiki/` 等目录。

在此基础上，本次变更完成两件事：将 CST S-expression 写入 workspace 文件（而非 DB），以及基于 Workspace 文件数据的 LLM 代码 Tool 体系。

当前 AST 持久化存在两个问题：
1. **丢失原始语法信息**：Tree-sitter 解析出的完整 CST 节点树在提取后就被丢弃
2. **提取逻辑带 bug**：`attributeAnnotations` 噪声、`fullSignature` 截断

## Goals / Non-Goals

**Goals:**
- CST S-expression 写入 `workspace/ast/{version_id[:8]}/files/{hash}.cst` 文件，做到零丢失可溯源
- 从 CST 派生轻量索引写入 `workspace/ast/{version_id[:8]}/symbols.json` 和 `manifest.json`
- 修复 `TreeSitterAnalyzer` 的 `attributeAnnotations` 噪声和 `fullSignature` 截断两个 bug
- 现有 4 个 LLM Tool 改为从 Workspace 文件读取数据
- 新增 `lookup_file` 和 `find_usages` 两个 Tool
- Wiki 页面生成改为"轻量预注入（Top-3）+ Tool 按需查询"混合策略

**Non-Goals:**
- 不改变 Tree-sitter 的解析能力边界
- 不实现前端语法树可视化页面（远期目标）
- 不改变 BM25 检索引擎的工作方式
- 不改变 `AstVersion` 的表结构（`ast_dir_path` 已在 workspace-filesystem 中定义）

## Decisions

### 决策 1：CST S-expression 作为独立文件存储在 Workspace

**选择**：每个文件的 CST S-expression 写入 `workspace/ast/{ast_version_id[:8]}/files/{source_sha256[:16]}.cst`。`manifest.json` 记录文件清单与统计，`symbols.json` 记录轻量索引。

```
workspace/ast/{ast_version_id[:8]}/
├── manifest.json          ← { total_files, total_symbols, ... }
├── files/
│   ├── {sha256[:16]}.cst  ← 单个文件的 CST S-expression
│   ├── {sha256[:16]}.cst
│   └── ...
└── symbols.json           ← [{ name, kind, file, ... }]
```

**理由**：
- 文件名 = 源码 SHA256 前 16 位，同一源码跨版本自动去重
- 单个文件独立存储，读取时只加载需要的文件而非全量
- 纯文本格式，可直接 `cat` / 文本编辑器查看调试

**替代方案**：所有 CST 打包到一个 JSON 文件。读取时必须全量加载，不支持按需访问。不采用。

### 决策 2：CST 读取优先 Workspace 文件，缺失则重新生成

**选择**：读取 CST 数据时先检查 workspace 文件是否存在。文件缺失则触发 `AstPersistenceService` 重新解析。此模式由 `WorkspaceService.ReadOrRegenerateAsync` 统一封装。

**理由**：
- 与 `workspace-filesystem` 变更中定义的文件缺失→重生成模式一致
- workspace 目录可被手动清理而不破坏系统一致性
- 文件系统是缓存层，DB `ast_dir_path` 是"应该存在"的记录

### 决策 3：LLM Tools 数据源切换为 Workspace 文件

**选择**：`ReadCodeFile`、`SearchSymbols`、`QueryCallGraph`、`RetrieveClassDefinition` 从 Workspace 文件读取：

| Tool | 数据源 |
|------|--------|
| `ReadCodeFile` | `workspace/repos/` 仓库源文件 + `ast/{id}/files/{hash}.cst` |
| `SearchSymbols` | DB `symbol_names_json`（小 JSON，无文件 I/O） |
| `QueryCallGraph` | `ast/{id}/manifest.json` → 定位 → 读 `.cst` 文件中的 edges |
| `RetrieveClassDefinition` | `ast/{id}/symbols.json` |
| `lookup_file` (新) | `workspace/repos/` + `ast/{id}/files/` |
| `find_usages` (新) | 遍历 `ast/{id}/files/*.cst` 中的 edges |

**理由**：
- 数据已经在 workspace 文件中，不再需要实时 Tree-sitter 解析
- Tool 调用延迟降到毫秒级（读本地文件），不需要文件 I/O 到 DB 再反序列化

### 决策 4：混合注入策略 — Top-3 预注入 + Tool 兜底

**选择**：Wiki 页面生成时，System Prompt 中预注入 BM25 Top-3 代码分块（约 2000 tokens），LLM 在需要更多上下文时自主调用 Tool。不再注入全部 BM25 Top-20 结果。

**理由**：
- 大部分页面的 Top-3 已经覆盖核心代码上下文
- 减少不必要的 Token 消耗（每页节省 ~6000 tokens 预注入）
- 少数复杂页面多 1-3 次 Tool 调用，延迟可控

**替代方案**：纯 Tool 模式（零预注入）。LLM 没有初始上下文锚点，第一轮输出质量不稳定。不采用。

## Risks / Trade-offs

- **[Risk] S-expression 可读性差**：CST S-expression 对非编译器开发者不够直观 -> **Mitigation**：LLM 读取时由 Tool 格式化输出源码 + 符号清单，不直接暴露 S-expression 给 LLM
- **[Risk] 文件数量膨胀**：中型仓库 ~500 源文件 → `ast/{id}/files/` 下有 500 个 `.cst` 文件 -> **Mitigation**：文件系统对此规模完全无压力；SHA256 命名避免目录扫描热点
- **[Risk] 双重解析**：当前持久化一次解析（CST）+ Wiki 管线又一次解析（BM25 分块）-> **Mitigation**：Wiki 管线的 `BuildSearchIndexAsync` 直接从 `workspace/ast/` 中的 chunk 数据构建 BM25 索引，不再调 `ChunkFile()` 实时解析
- **[Risk] workspace-filesystem 未就绪**：如果 workspace 变更未先实施，CST 文件无处存放 -> **Mitigation**：两个变更有明确的依赖顺序——先 `workspace-filesystem`，后 `cst-backed-code-tools`

## Migration Plan

1. 确保 `workspace-filesystem` 变更已实施（`WorkspaceService` 可用）
2. 新增 `TreeSitterAnalyzer.ToCstString(Node root)` 方法
3. 修复 `attributeAnnotations` 和 `fullSignature` 两个 bug
4. 修改 `CodeIndexService.BuildPersistenceProjection`：CST 写入 `workspace/ast/{id}/files/{hash}.cst`
5. 更新 `AstPersistenceService`：`projection_format_version` 改为 "2.0"
6. 改造 4 个已有 LLM Tool 数据源 + 新增 2 个 Tool
7. 调整 `WikiTaskService.BuildSearchIndexAsync`：从 workspace ast 目录读 chunks
8. 实现混合注入策略（Top-3 预注入，其余 Tool 兜底）
9. 测试：跑一次完整 Wiki 生成验证 CST 完整性 + Tool 可用性

## Open Questions

- `find_usages` 是否需要跨 CST 版本查询（查询一个符号在历史版本中的调用变化）？
- 后续问答、Slides、Workshop 是否直接使用同一套 Tool 体系？
- `.cst` 文件是否需要 gzip 压缩以节省磁盘空间？（纯文本 S-expression 压缩率通常 5-10x）
