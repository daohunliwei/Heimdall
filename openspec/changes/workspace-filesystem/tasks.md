## 1. Workspace 基础设施

- [ ] 1.1 新增 `WorkspaceConfig` 配置模型，从 `HEIMDALL_WORKSPACE` 环境变量读取，默认 `./workspace`
- [ ] 1.2 新增 `WorkspaceService` 单例服务，封装 `RootPath`、`EnsureDirectories()`、6 个路径解析方法
- [ ] 1.3 在 `Program.cs` 中注册 `WorkspaceService` 为 Singleton，启动时调用 `EnsureDirectories()`
- [ ] 1.4 新增 `HeimdallConfigService` 中的 workspace 配置读取支持

## 2. 实体模型迁移

- [ ] 2.1 `AstVersion`：新增 `ast_dir_path`（VARCHAR 512），保留 `symbol_names_json`、`file_list_json`，标记 `result_json` 为废弃
- [ ] 2.2 `WikiPage`：新增 `content_file_path`（VARCHAR 1024），标记 `ContentMarkdown` 为废弃
- [ ] 2.3 `WikiVersion`：新增 `structure_file_path`（VARCHAR 512），标记 `StructureJson` 为废弃
- [ ] 2.4 `TaskArtifact`：新增 `payload_file_path`（VARCHAR 1024），标记 `PayloadJson` 为废弃
- [ ] 2.5 `TaskLlmCallLog`：新增 `log_file_path`（VARCHAR 512），标记 `RequestPreview`/`ResponsePreview` 为废弃
- [ ] 2.6 运行 CodeFirst 同步，确认新列创建成功，旧列保留

## 3. 写入端改造（双写过渡）

- [ ] 3.1 改造 `AstPersistenceService`：结果写入 `workspace/ast/{id[:8]}/` 目录文件 + 更新 `ast_dir_path`
- [ ] 3.2 改造 `WikiTaskService` 持久化阶段：Wiki 页面写入 `workspace/wiki/` 文件 + 更新 `content_file_path`
- [ ] 3.3 改造 `WikiTaskService` 工件存储：`UpsertTaskArtifactAsync` 写 `artifacts/` 文件 + 更新 `payload_file_path`
- [ ] 3.4 改造 `TaskLlmCallLog` 写入：LLM 调用日志以 JSONL 格式追加到 `logs/{task_id[:8]}/calls.jsonl`
- [ ] 3.5 改造 `RepositoryAccessService`：克隆路径改为 `workspace/repos/{owner}_{repo}/`

## 4. 读取端改造（文件优先，DB 回退）

- [ ] 4.1 `WorkspaceService` 实现 `ReadOrRegenerateAsync<T>(path, regenerate)` 通用方法
- [ ] 4.2 改造 `AstVersion` 读取：优先读 workspace 文件，缺失时触发重新解析
- [ ] 4.3 改造 `WikiPage` 读取：优先读 workspace `.md` 文件，缺失时触发页面重新生成
- [ ] 4.4 改造 `TaskArtifact` 读取：优先读 `artifacts/` 文件，缺失时从任务状态判断是否需要重新生成
- [ ] 4.5 保持所有 LLM Tool 数据源兼容（`SearchSymbols` 仍从 DB `symbol_names_json` 读，无需文件 I/O）

## 5. 一次性数据迁移

- [ ] 5.1 实现 `WorkspaceMigrationService`：遍历现有 DB 记录，将 `result_json`、`ContentMarkdown`、`StructureJson`、`PayloadJson` 写入 workspace 文件
- [ ] 5.2 迁移完成后更新对应 `*_file_path` 列
- [ ] 5.3 验证迁移完整性：抽样检查文件内容与 DB 列一致
- [ ] 5.4 清理旧 `%TEMP%/heimdall_repos/` 目录（提示用户手动清理或提供脚本）

## 6. 验证

- [ ] 6.1 单元测试：`WorkspaceService` 路径解析正确性、目录创建、`ReadOrRegenerate` 模式
- [ ] 6.2 集成测试：完整 AST 解析 → workspace 文件落盘 → 读取验证
- [ ] 6.3 集成测试：完整 Wiki 生成 → 页面文件和结构文件落盘 → 读取验证
- [ ] 6.4 运行全量后端测试，确认不破坏现有功能
- [ ] 6.5 手动验证：删除 workspace 下某目录后，触发自动重新生成
