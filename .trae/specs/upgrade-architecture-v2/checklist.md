# Checklist: Heimdall 架构升级 V2

## M1：主标识统一

- [x] `Repository` 实体已新增 `provider_type`、`provider_repository_key`、`display_name` 等字段与唯一索引
- [x] `POST /api/repositories/import` 可根据仓库 URL 返回 `repositoryId`
- [x] `GET /api/repositories` 返回仓库列表，每项含 `repository_id`、`display_name`
- [x] `GET /api/repositories/{repositoryId}` 返回单个仓库详情
- [x] `PATCH /api/repositories/{repositoryId}` 可更新仓库元数据
- [x] `DELETE /api/repositories/{repositoryId}` 可删除仓库
- [x] `GET /api/projects` 返回字段统一使用 `repository_id`
- [x] `DELETE /api/projects/{repositoryId}` 基于 repositoryId 删除
- [x] `GET /api/repositories/{repositoryId}/wiki` 可按 repositoryId 读取 Wiki
- [x] `DELETE /api/repositories/{repositoryId}/wiki` 可按 repositoryId 删除 Wiki
- [x] `POST /api/tasks/wiki` 接受 `repository_id` 参数
- [x] 前端页面 `/repositories/[repositoryId]` 可正常加载仓库主页
- [x] 前端页面 `/repositories/[repositoryId]/slides` 可正常加载 Slides
- [x] 前端页面 `/repositories/[repositoryId]/workshop` 可正常加载 Workshop
- [x] 首页输入仓库地址后先调用 import 接口，获得 repositoryId 后跳转
- [x] 项目列表卡片跳转链接使用 repositoryId
- [x] 项目列表删除操作使用 repositoryId
- [x] 旧路由 `/[owner]/[repo]` 已删除（V2 不需要此路由）

## M2：版本化底座

- [x] `repository_versions` 表存在，唯一索引 `(repository_id, branch_name, commit_sha)` 生效
- [x] `GET /api/repositories/{repositoryId}/versions` 等版本查询接口已实现
- [x] `wiki_spaces` 表存在，关联 `repository_id`
- [x] `wiki_versions` 表存在，关联 `wiki_space_id` 与 `repository_version_id`
- [x] `wiki_page_relations` 表存在，支持 `parent`、`depends_on`、`related_to` 等关系类型
- [x] `wiki_pages` 表新增 `page_type`、`nav_title`、`outline_json`、`source_coverage_json` 字段
- [x] 版本发现服务可查询远端 HEAD 并创建或复用 `repository_version`
- [x] 刷新最新版本时，若无新提交则返回 `change_status: "unchanged"`
- [x] 强制刷新不会重复创建 `repository_version`
- [x] 同一 `repository_version` 可对应多个 `wiki_version`
- [x] 发布态读写：默认返回 `published_wiki_version_id` 对应内容
- [x] `TaskRecord` 实体含 `RepositoryId`、`ResolvedRepositoryVersionId`、`ResultWikiVersionId` 等版本字段
- [x] 任务完成后 `ResultWikiVersionId` 正确回写
- [x] 历史数据无需回填（全新系统，WikiSpace/RepositoryVersion 在首次任务运行时惰性创建）

## M3：双向量表

- [x] `code_embedding_chunks` 表存在 ~~，含 pgvector 向量索引~~ *(向量存储为 bytea，相似度在内存中计算)*
- [x] `wiki_embedding_chunks` 表存在 ~~，含 pgvector 向量索引~~ *(同上)*
- [x] 代码嵌入服务可对仓库文件分块、嵌入并写入 `code_embedding_chunks`
- [x] Wiki 嵌入服务可对页面内容分块、嵌入并写入 `wiki_embedding_chunks`
- [x] 双向量检索服务 `SearchCodeAsync` 可检索代码向量
- [x] 双向量检索服务 `SearchWikiAsync` 可检索 Wiki 向量
- [x] `SearchCombinedAsync` 可实现双向量域召回与结果重排
- [x] Ask 问答链路已切换到双向量检索服务
- [x] 旧 `embedding_documents` 表已删除（V2 不需要回退）
- [x] `DELETE /api/repositories/{repositoryId}/vectors/code` 和 `wiki` 向量清理接口已实现

## M4：复杂 Wiki 编排

- [ ] 四阶段生成编排已实现（仓库理解 → 结构规划 → 页面生成 → 全局收敛）
  - **仓库理解 + 结构规划**: 合并为一个 LLM 调用（`BuildWikiStructurePrompt`），无独立服务
  - **页面生成**: 已实现，含源文件内容注入
  - **全局收敛（Phase D）**: ❌ 未实现 — 无重复检测、页面大小分析、风格统一
- [x] 阶段 B 结构规划结果已存储为 `structure_json`（`WikiVersion.StructureJson`）
- [ ] 阶段 D 全局收敛可检测重复页面、过大/过小页面、风格不一致等问题 — ❌ 未实现
- [x] 页面关系分析服务可自动补全 `wiki_page_relations`（`SaveWikiPageRelationsAsync`）
- [x] 前端版本切换器组件（`VersionSwitcher.tsx`）可展示并切换历史版本，已接入页面
- [x] 前端刷新面板组件（`RefreshPanel.tsx`）支持分支选择、刷新策略、强制刷新、档位、Provider/Model 配置，已接入页面
- [x] 前端 Wiki 页面底部展示"相关页面"导航（`related_to` 关系）
- [ ] 前端 Wiki 页面底部展示"前置阅读"导航（`depends_on` 关系）— ❌ 未实现
- [ ] 生成失败后可从中间工件恢复（resume）— ❌ 未实现

## M5：版本对比

- [x] `POST /api/repositories/{repositoryId}/wiki/compare` 可比较两个 Wiki 版本 — 已实现真实对比逻辑
- [x] 对比结果包含新增页面、删除页面、标题变化、内容变化较大页面
- [ ] 可比较两个仓库快照版本的文件变更摘要 — ❌ 未实现（无端点）
- [ ] 前端版本对比页面可展示结构化差异列表 — ❌ 未实现（无页面）

## 通用验证

- [x] `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj` 编译通过（0 错误）
- [ ] `npm run build`（frontend）编译通过 — 待验证
- [ ] `npm run lint`（frontend）无新增告警 — 待验证
- [x] 所有新增实体、服务、控制器注释完备且使用中文
- [x] 所有新增接口在非认证模式下可正常调用
- [x] 数据库迁移已应用（`InitialV2` 干净初始迁移，数据库 `ai_heimdall_base` @ `10.189.10.252` 已重置）
- [x] 所有 V1 兼容逻辑（旧路由、repo_url 回退、embedding_documents 回退）已清理

---

### 未完成项汇总（6 项）

| 编号 | 描述 | 优先级 |
|------|------|--------|
| M4-1 | 全局收敛阶段（Phase D）：重复检测、页面大小分析、风格统一 | 中 |
| M4-3 | Phase D 收敛逻辑实现 | 中 |
| M4-7 | 前置阅读导航（`depends_on` 关系前端展示） | 低 |
| M4-8 | 生成失败后中间工件恢复（resume） | 低 |
| M5-11 | 仓库快照版本文件变更对比 | 低 |
| M5-12 | 前端版本对比页面 | 低 |
