## Why

当前所有大型数据——AST 解析结果、Wiki 页面 Markdown、任务工件 JSON、LLM 调用日志——全部压缩在 PostgreSQL TEXT 列中。`AstVersion.result_json` 单字段可达 5-15 MB，`WikiPage.ContentMarkdown` 40 页 × 数 KB 持续膨胀。这不仅拖慢数据库备份/恢复，也让调试、手动检查和文件级缓存变得困难。更合理的设计是：数据库存元数据+路径，文件系统存内容——这正是 Workspace 概念要解决的问题。

## What Changes

- 新增 `HEIMDALL_WORKSPACE` 环境变量（默认 `./workspace`），作为所有文件系统数据的统一根目录
- 设计 Workspace 标准目录结构：`repos/`、`ast/`、`wiki/`、`artifacts/`、`logs/`、`cache/`
- 将以下数据从 DB TEXT 列迁移到 Workspace 文件，DB 只存 `*_file_path`：
  - **AST 解析结果**：`AstVersion.result_json` → `ast/{version_id}/` 目录下的 `.cst` + `.json` 文件
  - **Wiki 页面内容**：`WikiPage.ContentMarkdown` → `wiki/{version_id}/pages/{page_id}.md`
  - **任务工件**：`TaskArtifact.PayloadJson` → `artifacts/{task_id}/{artifact_type}.json`
  - **Wiki 结构/关系**：`WikiVersion.StructureJson` → `wiki/{version_id}/structure.json`
  - **LLM 调用日志**：`TaskLlmCallLog` 大文本字段 → `logs/{task_id}/calls.jsonl`
- 仓库克隆路径从 `%TEMP%/heimdall_repos/` 迁移到 `workspace/repos/`
- 实现"文件缺失即触发重新生成"的缓存失效模式
- 保持所有实体元数据字段（ID、时间戳、统计、状态）在数据库中，不做迁移

## Capabilities

### New Capabilities
- `workspace-management`: Workspace 根目录配置、标准目录结构初始化、路径解析服务、文件级缓存失效与重新生成触发

### Modified Capabilities
- `cst-persistence`: CST S-expression 存储从 DB JSON 列改为 Workspace 文件，DB 只记录目录路径
- `wiki-generation-pipeline`: Wiki 页面内容和结构从 DB 列改为 Workspace 文件存储；仓库克隆路径迁移到 workspace
- `llm-tools`: LLM Tool 数据源从 DB JSON 查询改为 Workspace 文件读取

## Impact

- **数据模型**: 6 个实体的 TEXT 列改为 `*_file_path`（VARCHAR）；新增 `WorkspaceConfig` 配置模型
- **基础设施**: 新增 `WorkspaceService`（路径解析、目录初始化、文件读写）；`RepositoryAccessService` 克隆路径变更
- **仓储层**: `AstVersionRepository`、`WikiPageRepository`、`TaskArtifactRepository` 等增加文件读写协调
- **管线**: `WikiTaskService`、`AstPersistenceService` 生成逻辑改为写文件 + 写路径到 DB
- **配置**: 新增 `HEIMDALL_WORKSPACE` 环境变量，`HeimdallConfigService` 增加 workspace 配置读取
- **BREAKING**: 已有 `AstVersion`、`WikiPage`、`TaskArtifact` 记录的 TEXT 内容需要一次性迁移到文件
