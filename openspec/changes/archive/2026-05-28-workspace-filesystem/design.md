## Context

当前系统所有大型数据（AST 结果、Wiki 页面、任务工件、LLM 日志）全部以 TEXT 列存放在 PostgreSQL 中。一个中型仓库的 `AstVersion.result_json` 可达 5-15 MB，40 页 Wiki 的 `ContentMarkdown` 可达数百 KB。这些问题在之前讨论 CST 文件存储时被暴露出来——用户明确要求"数据库只记录文件路径，文件缺失触发重新生成"。

同时，`persist-versioned-ast-results` 和 `cst-backed-code-tools` 两个变更已建立了 AST 持久化模型和管线集成点，但存储方式仍依赖 DB TEXT 列。本次变更将存储层从 DB 迁移到文件系统，为后续所有变更提供统一的工作空间底座。

## Goals / Non-Goals

**Goals:**
- 定义 `HEIMDALL_WORKSPACE` 环境变量，作为所有文件系统数据的统一根目录
- 设计标准目录结构，按数据类型分区（repos / ast / wiki / artifacts / logs / cache）
- 将大型数据从 DB TEXT 列迁移到 Workspace 文件，DB 只存 `*_file_path`
- 实现文件缺失检测 → 自动触发重新生成的缓存失效模式
- 仓库克隆从系统临时目录迁移到 workspace/repos/
- 保持实体元数据（ID、状态、时间戳、统计）在数据库中

**Non-Goals:**
- 不迁移小型配置/设置数据（SystemSetting、PromptTemplate 等 TEXT 列 < 10KB 保留在 DB）
- 不改变数据库表结构（表名、关系、索引不变，仅列值从内容变为路径）
- 不引入对象存储（S3/MinIO）——本次限定为本地文件系统
- 不改变现有 API 契约

## Decisions

### 决策 1：Workspace 目录结构

**选择**：

```
${HEIMDALL_WORKSPACE}/                  ← 默认 ./workspace（相对于进程工作目录）
├── repos/                              ← 克隆的代码仓库
│   └── {owner}_{repo}/                 ← 例: SilverHawk_wikispider
│       └── (git working tree)
│
├── ast/                                ← AST/CST 解析结果
│   └── {ast_version_id}/               ← 例: 0193e4a8-... (Guid 前 8 位)
│       ├── manifest.json               ← 文件清单 + 统计数据
│       ├── files/                      ← 单文件 CST S-expression
│       │   └── {file_sha256[:16]}.cst  ← 文件名 = 源码 SHA256 前 16 位
│       └── symbols.json                ← 轻量符号索引
│
├── wiki/                               ← Wiki 生成结果
│   └── {wiki_version_id[:8]}/          ← Wiki 版本 Guid 前 8 位
│       ├── structure.json              ← 结构规划 JSON
│       ├── pages/                      ← 页面 Markdown 内容
│       │   └── {page_order:D4}_{slug}.md
│       └── relations.json              ← 页面关系边
│
├── artifacts/                          ← 任务工件
│   └── {task_id[:8]}/                  ← 任务 Guid 前 8 位
│       ├── planning.json
│       ├── code_index.json
│       ├── code_understanding.json
│       ├── quality_report.json
│       ├── batch_{index:D4}.json       ← 页面批次工件
│       └── render_postprocess.json
│
├── logs/                               ← LLM 调用日志
│   └── {task_id[:8]}/
│       └── calls.jsonl                 ← 每行一条 LLM 调用记录
│
└── cache/                              ← 可随时清除的缓存
    └── bm25/                           ← BM25 索引快照（可选，加速重建）
        └── {index_key}.bm25
```

**理由**：
- 按数据类型顶层分区，新加入的开发者一眼能看懂每个目录的用途
- 子目录用 Guid 前 8 位——兼顾唯一性和人类可读（比完整 36 字符 Guid 短得多）
- AST 文件用源码 SHA256 前 16 位命名——同一源码文件跨版本共享同一 CST 文件，天然去重
- 页面文件用 `{order:D4}_{slug}.md`——排序稳定，slug 可读

**替代方案**：扁平结构（所有文件在同一目录下用 UUID 命名）。不可调试，不采用。

### 决策 2：DB 列改造规则——"大文本迁出，元数据保留"

**选择**：

| 实体 | 当前 DB 列 | 改为 | 新 Workspace 路径 |
|------|-----------|------|-------------------|
| `AstVersion` | `result_json` (TEXT) | `ast_dir_path` (VARCHAR 512) | `ast/{id[:8]}/` |
| `WikiPage` | `content_markdown` (TEXT) | `content_file_path` (VARCHAR 1024) | `wiki/{version_id[:8]}/pages/{order:D4}_{slug}.md` |
| `TaskArtifact` | `payload_json` (TEXT) | `payload_file_path` (VARCHAR 1024) | `artifacts/{task_id[:8]}/{type}.json` |
| `WikiVersion` | `structure_json` (TEXT) | `structure_file_path` (VARCHAR 512) | `wiki/{version_id[:8]}/structure.json` |
| `TaskLlmCallLog` | `request_preview`, `response_preview` (TEXT) | `log_file_path` (VARCHAR 512) | `logs/{task_id[:8]}/calls.jsonl` |

**保留在 DB 中**（< 10KB 或需频繁查询）：
- 所有实体 ID、Status、时间戳——元数据，必须 DB
- `AstVersion.symbol_names_json`、`file_list_json`——小 JSON，搜索用
- `WikiPage.outline_json`、`source_coverage_json`——小 JSON
- `CodeIndexEntry` 相关 JSON——已独立存在，暂不迁移
- 系统配置、提示词模板——小文本

**理由**：
- 阈值 ≈ 10KB：小于此值放 DB 更方便（无需额外 I/O），大于此值放文件更高效
- 频繁 JOIN 查询的字段留在 DB，偶尔整体读取的迁到文件

### 决策 3：文件缺失 = 缓存失效，触发重新生成

**选择**：读取路径优先检查文件是否存在。若文件缺失但 DB 路径字段非空，标记记录为 `stale`，触发对应服务的重新生成逻辑，生成完成后更新路径。

```
读取流程:
  DB.path_field 非空?
    → 是: File.Exists(path)?
        → 是: 读文件返回 ✓
        → 否: 标记 stale → 触发重新生成 → 写文件 → 更新 path → 返回 ✓
    → 否: 触发重新生成 → 写文件 → 更新 path → 返回 ✓
```

**理由**：
- 用户可手动删除 workspace 下任意目录实现"强制重新生成"
- 进程重启/迁移后 workspace 清空不导致数据不一致——自动重建
- DB 是唯一真实状态来源（记录"应该有什么"），文件系统是缓存层

### 决策 4：`WorkspaceService` 作为统一入口

**选择**：新增单例 `WorkspaceService`，封装：

```csharp
public class WorkspaceService
{
    string RootPath { get; }                    // HEIMDALL_WORKSPACE
    string GetRepoPath(owner, repo);            // → repos/{owner}_{repo}/
    string GetAstDir(astVersionId);             // → ast/{id[:8]}/
    string GetWikiDir(wikiVersionId);           // → wiki/{id[:8]}/
    string GetArtifactDir(taskId);              // → artifacts/{id[:8]}/
    string GetLogDir(taskId);                   // → logs/{id[:8]}/
    void EnsureDirectories();                   // 启动时创建顶层目录
    Task<string> ReadOrRegenerate(path, Func<Task<string>> regenerate);
}
```

**理由**：
- 全系统统一路径解析，避免各处硬编码路径拼接
- `ReadOrRegenerate` 封装文件缺失→重新生成的标准模式
- 未来换存储后端（如 S3）只需改这个服务

## Risks / Trade-offs

- **[Risk] 文件系统与 DB 事务不一致**：DB 写入成功但文件写入失败（或相反）→ **Mitigation**：先写文件，再写 DB；DB 写入失败时清理已写文件。文件系统操作天然幂等（覆盖写入）
- **[Risk] Workspace 目录被误删**：所有生成数据丢失 → **Mitigation**：文件缺失自动触发重新生成（决策 3）；DB 中路径字段保留，作为"应该存在"的记录
- **[Risk] 备份策略变化**：之前只备份 DB 即可，现在需要同时备份 workspace → **Mitigation**：workspace 中所有数据都可以从 DB 元数据 + 源码仓库重建，workspace 本质是缓存，不是唯一数据源
- **[Trade-off] 跨平台路径兼容**：Windows `\` vs Unix `/` → **Mitigation**：`WorkspaceService` 统一使用 `/` 存储路径到 DB，读取时用 `Path.Combine()` / `Path.DirectorySeparatorChar`

## Migration Plan

1. 新增 `HEIMDALL_WORKSPACE` 环境变量支持和 `WorkspaceService`
2. 新增实体字段（`*_file_path`），与旧 TEXT 列并存（可空迁移）
3. 修改写入逻辑：新数据同时写文件和 DB TEXT 列（双写过渡期）
4. 修改读取逻辑：优先读文件，回退读 DB TEXT 列
5. 运行一次性迁移脚本：遍历现有记录，将 TEXT 内容写入 workspace 文件，填充 `*_file_path`
6. 验证迁移完整性后，移除旧 TEXT 列和双写逻辑
7. 清理 `%TEMP%/heimdall_repos/` 旧克隆目录

## Open Questions

- 是否需要 `workspace prune` 命令清理孤立的 workspace 文件（DB 中已无对应记录的文件）？
- BM25 索引是否需要持久化到 `cache/bm25/` 以加速 Wiki 生成重启后的索引重建？
- workspace 是否需要最大容量限制和自动清理策略（如保留最近 N 个版本）？
