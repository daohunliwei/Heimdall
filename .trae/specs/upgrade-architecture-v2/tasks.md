# Tasks: Heimdall 架构升级 V2

> **状态说明**：V2 规划的所有里程碑核心能力已在 V3 实施中落地。以下以 `[x]` 标记已完成的子任务，以 `[~]` 标记被 V3 方案覆盖/替代的子任务。未勾选的条目有注释说明原因。

## 里程碑 M1：主标识统一（repositoryId 路由与 API）

- [x] M1.1 后端：改造 `Repository` 实体与基础接口
  - [x] M1.1.1 `Repository` 实体新增 `provider_type`、`provider_repository_key`、`display_name`、`clone_url`、`is_archived` 字段
  - [x] M1.1.2 创建 Fluent API 配置，增加 `(provider_type, provider_repository_key)` 唯一索引
  - [x] M1.1.3 生成 EF Core 迁移并更新数据库

- [x] M1.2 后端：新增 RepositoriesController 与 `POST /api/repositories/import`
  - [x] M1.2.1 创建 `IRepositoryService` 接口与 `RepositoryService` 实现
  - [x] M1.2.2 实现 `ImportAsync(repoUrl)` 方法：解析 URL → 查库 → 新建或复用 → 返回 `repositoryId`
  - [x] M1.2.3 创建 `RepositoriesController`：`POST /api/repositories/import`、`GET /api/repositories`、`GET /api/repositories/{id}`、`PATCH /api/repositories/{id}`、`DELETE /api/repositories/{id}`
  - [x] M1.2.4 在 `Program.cs` 中注册 DI

- [x] M1.3 后端：改造 ProjectsController 返回结构
  - [x] M1.3.1 `GET /api/projects` 返回字段统一为 `repository_id`、`display_name`、`repo_type`、`default_branch`、`latest_wiki_version_id`、`published_wiki_version_id`
  - [x] M1.3.2 `DELETE /api/projects/{repositoryId}` 改为按 `repositoryId` 删除（移除 owner/repo 依赖）

- [x] M1.4 后端：改造 WikiCacheController 为 repositoryId 风格
  - [x] M1.4.1 `GET /api/repositories/{repositoryId}/wiki` 替代 `GET /api/wiki/cache?owner=&repo=`
  - [x] M1.4.2 `DELETE /api/repositories/{repositoryId}/wiki` 替代旧的删除接口

- [x] M1.5 后端：改造 TasksController 请求模型
  - [x] M1.5.1 `POST /api/tasks/wiki` 请求体改为接受 `repository_id`（替代 `repo_url`）
  - [x] M1.5.2 新增 `branch`、`refresh_strategy`、`force_refresh`、`generation_profile` 参数（兼容默认值）

- [x] M1.6 前端：创建 `/repositories/[repositoryId]` 路由
  - [x] M1.6.1 创建 `frontend/src/app/repositories/[repositoryId]/page.tsx` 仓库主页
  - [x] M1.6.2 创建 `frontend/src/app/repositories/[repositoryId]/slides/page.tsx`
  - [x] M1.6.3 创建 `frontend/src/app/repositories/[repositoryId]/workshop/page.tsx`
  - [x] M1.6.4 仓库主页根据 `repositoryId` 加载仓库信息与 Wiki 内容

- [x] M1.7 前端：改造首页导入流程
  - [x] M1.7.1 首页输入仓库地址后调用 `POST /api/repositories/import`
  - [x] M1.7.2 收到 `repositoryId` 后跳转至 `/repositories/{repositoryId}`

- [x] M1.8 前端：改造 ProcessedProjects 项目列表
  - [x] M1.8.1 卡片跳转链接改为 `/repositories/{repositoryId}`
  - [x] M1.8.2 删除操作改为调用 `DELETE /api/projects/{repositoryId}`

- [~] M1.9 前端：旧路由兼容跳转
  - [~] M1.9.1 保留 `/[owner]/[repo]` 路由，内部根据 owner/repo 查询 repositoryId
  - [~] M1.9.2 返回 301 重定向至 `/repositories/{repositoryId}`
  - **V3 决策**：旧 `/[owner]/[repo]` 路由已直接移除，不再保留 301 兼容跳转。V3 以 repositoryId 为唯一主标识，无历史包袱。

## 里程碑 M2：版本化底座

- [x] M2.1 数据库：创建 `repository_versions` 实体与迁移
  - [x] M2.1.1 创建 `RepositoryVersion` 实体（id、repository_id、branch_name、commit_sha、tree_fingerprint、commit_time、commit_author、commit_message、source_status、is_latest_on_branch、created_at）
  - [x] M2.1.2 创建 `RepositoryVersionConfiguration` Fluent API 配置
  - [x] M2.1.3 唯一索引：`(repository_id, branch_name, commit_sha)`
  - [x] M2.1.4 生成 EF Core 迁移并更新数据库

- [x] M2.2 数据库：创建 `wiki_spaces` 与 `wiki_versions` 实体与迁移
  - [x] M2.2.1 创建 `WikiSpace` 实体（id、repository_id、language、view_type、title、description、published_wiki_version_id）
  - [x] M2.2.2 创建 `WikiVersion` 实体（id、wiki_space_id、repository_version_id、version_no、generation_mode、generation_profile、prompt_profile_hash、model_profile_hash、status、is_force_refresh、page_count、toc_depth、summary_markdown、created_by_task_id）
  - [x] M2.2.3 创建对应 Fluent API 配置
  - [x] M2.2.4 生成 EF Core 迁移并更新数据库

- [x] M2.3 数据库：创建 `wiki_page_relations` 实体与迁移
  - [x] M2.3.1 创建 `WikiPageRelation` 实体（id、wiki_version_id、source_page_id、target_page_id、relation_type、metadata_json）
  - [x] M2.3.2 创建 Fluent API 配置与索引
  - [x] M2.3.3 更新 `WikiPage` 实体，增加 `page_type`、`nav_title`、`outline_json`、`source_coverage_json`、`token_count`、`status` 字段
  - [x] M2.3.4 生成 EF Core 迁移并更新数据库

- [x] M2.4 后端：版本发现服务与 API
  - [x] M2.4.1 创建 `IVersionDiscoveryService` 接口
  - [x] M2.4.2 实现 `DiscoverRepositoryVersionAsync(repositoryId, branch)`：查询远端 HEAD → 比较本地 → 创建或复用 `repository_version`
  - [x] M2.4.3 实现 `GetLatestVersionAsync(repositoryId, branch)` 查询接口
  - [x] M2.4.4 在 `Program.cs` 中注册 DI
  - [x] M2.4.5 创建 `RepositoryVersionsController` 提供 `GET /api/repositories/{id}/versions`、`GET .../versions/{vid}`、`POST .../versions/discover`、`GET .../versions/latest` 接口

- [x] M2.5 后端：刷新编排服务
  - [x] M2.5.1 创建 `IRefreshOrchestrationService` 接口
  - [x] M2.5.2 实现三种刷新策略：`RefreshCurrentVersion` / `RefreshLatestVersion` / `ForceRefresh`
  - [x] M2.5.3 统一返回结果模型（task_id、repository_version_id、wiki_version_id、result_type、change_status）
  - [x] M2.5.4 在 `Program.cs` 中注册 DI

- [x] M2.6 后端：Wiki 版本 API
  - [x] M2.6.1 创建 `WikiVersionController`：`GET /api/repositories/{repositoryId}/wiki/versions`、`GET .../{wikiVersionId}`、`POST .../refresh`、`POST .../publish`
  - [x] M2.6.2 实现发布态读写逻辑（默认返回 `published_wiki_version_id` 对应内容）

- [x] M2.7 后端：任务模型版本关联改造
  - [x] M2.7.1 `TaskRecord` 实体新增 `RepositoryId`、`TargetBranch`、`ResolvedRepositoryVersionId`、`ResultWikiVersionId`、`RefreshStrategy`、`ForceRefresh`、`ConfigHash` 字段
  - [x] M2.7.2 更新 TaskRecordConfiguration
  - [x] M2.7.3 生成 EF Core 迁移并更新数据库
  - [x] M2.7.4 改造 Wiki 生成任务链路：任务创建时绑定版本，完成后回写版本 ID

- [ ] M2.8 数据迁移：历史数据回填
  - [ ] M2.8.1 为每个已有仓库创建默认 `wiki_space`（language=zh, view_type=default）
  - [ ] M2.8.2 为每个仓库回填一个初始 `repository_version`（commit_sha 标记为 unknown 或 inferred）
  - [ ] M2.8.3 将现有 Wiki 数据映射为初始 `wiki_version`
  - **未实施原因**：V3 以全新 schema 部署（InitialV2 迁移创建所有表），无历史数据需要回填。若未来需要从旧版迁移数据，此项作为参考。

## 里程碑 M3：双向量表

- [x] M3.1 数据库：创建 `code_embedding_chunks` 实体与迁移
  - [x] M3.1.1 创建 `CodeEmbeddingChunk` 实体（id、repository_version_id、file_path、symbol_path、chunk_index、chunk_type、language、start_line、end_line、content_raw、content_normalized、content_hash、token_count、embedding_model、embedding_vector）
  - [x] M3.1.2 创建 Fluent API 配置，pgvector 向量索引
  - [x] M3.1.3 生成 EF Core 迁移并更新数据库

- [x] M3.2 数据库：创建 `wiki_embedding_chunks` 实体与迁移
  - [x] M3.2.1 创建 `WikiEmbeddingChunk` 实体（id、wiki_version_id、wiki_page_id、chunk_index、chunk_type、content_raw、content_hash、token_count、embedding_model、embedding_vector）
  - [x] M3.2.2 创建 Fluent API 配置，pgvector 向量索引
  - [x] M3.2.3 生成 EF Core 迁移并更新数据库

- [x] M3.3 后端：代码嵌入服务
  - [x] M3.3.1 创建 `ICodeEmbeddingService` 接口
  - [x] M3.3.2 实现文件分块、嵌入生成、批量写入 `code_embedding_chunks`
  - [x] M3.3.3 在 `Program.cs` 中注册 DI

- [x] M3.4 后端：Wiki 嵌入服务
  - [x] M3.4.1 创建 `IWikiEmbeddingService` 接口
  - [x] M3.4.2 实现页面内容分块、嵌入生成、批量写入 `wiki_embedding_chunks`
  - [x] M3.4.3 在 `Program.cs` 中注册 DI

- [x] M3.5 后端：双向量检索服务
  - [x] M3.5.1 创建 `IDualVectorSearchService` 接口
  - [x] M3.5.2 实现 `SearchCodeAsync`：按 `repository_version_id` 检索代码向量
  - [x] M3.5.3 实现 `SearchWikiAsync`：按 `wiki_version_id` 检索 Wiki 向量
  - [x] M3.5.4 实现 `SearchCombinedAsync`：双向量域召回 + 结果重排
  - [x] M3.5.5 在 `Program.cs` 中注册 DI

- [x] M3.6 改造 Ask 问答链路适配双向量检索
  - [x] M3.6.1 ChatController / Ask 服务切换到 `IDualVectorSearchService`（AskTaskService 与 RagContextService 均已接入）
  - [x] M3.6.2 旧 `embedding_documents` 保留只读回退能力

- [x] M3.7 后端：向量清理接口
  - [x] M3.7.1 新增 `DELETE /api/repositories/{repositoryId}/vectors/code` 接口
  - [x] M3.7.2 新增 `DELETE /api/repositories/{repositoryId}/vectors/wiki` 接口

## 里程碑 M4：复杂 Wiki 编排能力

- [x] M4.1 后端：四阶段生成编排
  - [x] M4.1.1 实现阶段 A：仓库理解与主题提取服务（WikiTaskService.structure_planning 阶段）
  - [x] M4.1.2 实现阶段 B：Wiki 结构规划服务（含 `structure_json` 工件存储）
  - [x] M4.1.3 改造阶段 C：页面生成时补充摘要、相关页面、来源文件信息
  - [x] M4.1.4 实现阶段 D：全局收敛与编排修正服务（WikiGlobalConvergenceService：重复检测、拆分合并、风格统一）

- [~] M4.2 后端：页面关系自动补全
  - [~] M4.2.1 实现页面关系分析服务：扫描页面内容 → 提取交叉引用 → 写入 `wiki_page_relations`
  - [~] M4.2.2 实现前置阅读链计算：基于依赖关系推荐阅读顺序
  - **V3 实施**：页面关系分析未作为独立服务存在，但 `WikiGlobalConvergenceService` 在全局收敛阶段完成了双向关系链接、父子关联、空内容检测与回退检测，覆盖了核心关系补全需求。

- [x] M4.3 前端：版本切换器组件
  - [x] M4.3.1 创建 `VersionSwitcher` 组件，展示当前发布版本、历史版本列表
  - [x] M4.3.2 支持切换到指定 `wikiVersionId` 或 `repositoryVersionId`
  - [x] M4.3.3 展示版本元信息（提交哈希、分支、生成时间、生成配置）

- [x] M4.4 前端：刷新面板组件
  - [x] M4.4.1 创建 `RefreshPanel` 组件（分支选择、刷新策略、强制刷新开关、生成档位、Provider/Model）
  - [x] M4.4.2 调用刷新 API 并根据返回结果展示提示（已排队 / 复用已有版本 / 无变化）

- [x] M4.5 前端：页面关系导航
  - [x] M4.5.1 Wiki 页面底部展示"相关页面""前置阅读"等板块
  - [x] M4.5.2 数据来源：`wiki_page_relations` 表

## 里程碑 M5：版本对比与差异

- [x] M5.1 后端：版本对比 API
  - [x] M5.1.1 创建 `POST /api/repositories/{repositoryId}/wiki/compare` 接口（WikiCompareController）
  - [x] M5.1.2 实现比较两个 `wiki_version`：新增页面、删除页面、标题变化、内容变化较大的页面
  - [x] M5.1.3 实现比较两个 `repository_version`：文件变更摘要

- [ ] M5.2 前端：版本对比页面
  - [ ] M5.2.1 创建版本对比入口页面
  - [ ] M5.2.2 展示对比结果摘要（结构化差异列表）
  - **未实施原因**：V2 原始规划中此为非刚性需求（"V2 先不要求做完整可视化 diff"），V3 聚焦核心链路闭环，未纳入前端对比页面。后端 API 已就绪，前端可按需追加。

# 总结

- **M1、M3**：完全实现，无遗留。
- **M2**：核心能力完全实现，仅 M2.8（历史数据回填）因 V3 全新部署无需执行。
- **M4**：核心能力完全实现，M4.2 页面关系分析由 WikiGlobalConvergenceService 覆盖而非独立服务。
- **M5**：后端对比 API 已实现，前端对比页面为后续可选增强。
- **M1.9**（旧路由兼容跳转）：V3 直接移除了旧路由，无需兼容层。

# Task Dependencies（原始依赖关系，保留以供参考）

- M2（版本化底座）依赖 M1（主标识统一）— 版本 API 需基于 repositoryId 路由
- M3（双向量表）可部分与 M2 并行：M3.1/M3.2 实体与迁移可与 M2 实体并行创建，但 M3.3-M3.6 检索服务依赖 M2 的 repository_version / wiki_version
- M4（复杂编排）依赖 M2 的 wiki_version + wiki_page_relations，以及 M3 的双向量检索
- M5（版本对比）依赖 M2 的多版本数据
- M1.6 前端新路由与 M1.1-M1.5 后端 API 可并行开发
- M1.9 旧路由兼容跳转依赖 M1.2 的 import 接口完成
