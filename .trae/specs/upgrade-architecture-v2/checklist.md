# Checklist: Heimdall 架构升级 V2

## M1：主标识统一

- [ ] `Repository` 实体已新增 `provider_type`、`provider_repository_key`、`display_name` 等字段与唯一索引
- [ ] `POST /api/repositories/import` 可根据仓库 URL 返回 `repositoryId`
- [ ] `GET /api/repositories` 返回仓库列表，每项含 `repository_id`、`display_name`
- [ ] `GET /api/repositories/{repositoryId}` 返回单个仓库详情
- [ ] `PATCH /api/repositories/{repositoryId}` 可更新仓库元数据
- [ ] `DELETE /api/repositories/{repositoryId}` 可删除仓库
- [ ] `GET /api/projects` 返回字段统一使用 `repository_id`
- [ ] `DELETE /api/projects/{repositoryId}` 基于 repositoryId 删除
- [ ] `GET /api/repositories/{repositoryId}/wiki` 可按 repositoryId 读取 Wiki
- [ ] `DELETE /api/repositories/{repositoryId}/wiki` 可按 repositoryId 删除 Wiki
- [ ] `POST /api/tasks/wiki` 接受 `repository_id` 参数
- [ ] 前端页面 `/repositories/[repositoryId]` 可正常加载仓库主页
- [ ] 前端页面 `/repositories/[repositoryId]/slides` 可正常加载 Slides
- [ ] 前端页面 `/repositories/[repositoryId]/workshop` 可正常加载 Workshop
- [ ] 首页输入仓库地址后先调用 import 接口，获得 repositoryId 后跳转
- [ ] 项目列表卡片跳转链接使用 repositoryId
- [ ] 项目列表删除操作使用 repositoryId
- [ ] 旧路由 `/[owner]/[repo]` 访问后自动 301 重定向到新路由

## M2：版本化底座

- [ ] `repository_versions` 表存在，唯一索引 `(repository_id, branch_name, commit_sha)` 生效
- [ ] `GET /api/repositories/{repositoryId}/versions` 等版本查询接口已实现
- [ ] `wiki_spaces` 表存在，关联 `repository_id`
- [ ] `wiki_versions` 表存在，关联 `wiki_space_id` 与 `repository_version_id`
- [ ] `wiki_page_relations` 表存在，支持 `parent`、`depends_on`、`related_to` 等关系类型
- [ ] `wiki_pages` 表新增 `page_type`、`nav_title`、`outline_json`、`source_coverage_json` 字段
- [ ] 版本发现服务可查询远端 HEAD 并创建或复用 `repository_version`
- [ ] 刷新最新版本时，若无新提交则返回 `change_status: "unchanged"`
- [ ] 强制刷新不会重复创建 `repository_version`
- [ ] 同一 `repository_version` 可对应多个 `wiki_version`
- [ ] 发布态读写：默认返回 `published_wiki_version_id` 对应内容
- [ ] `TaskRecord` 实体含 `RepositoryId`、`ResolvedRepositoryVersionId`、`ResultWikiVersionId` 等版本字段
- [ ] 任务完成后 `ResultWikiVersionId` 正确回写
- [ ] 历史数据成功回填：每个仓库有默认 `wiki_space` 和初始版本

## M3：双向量表

- [ ] `code_embedding_chunks` 表存在，含 pgvector 向量索引
- [ ] `wiki_embedding_chunks` 表存在，含 pgvector 向量索引
- [ ] 代码嵌入服务可对仓库文件分块、嵌入并写入 `code_embedding_chunks`
- [ ] Wiki 嵌入服务可对页面内容分块、嵌入并写入 `wiki_embedding_chunks`
- [ ] 双向量检索服务 `SearchCodeAsync` 可检索代码向量
- [ ] 双向量检索服务 `SearchWikiAsync` 可检索 Wiki 向量
- [ ] `SearchCombinedAsync` 可实现双向量域召回与结果重排
- [ ] Ask 问答链路已切换到双向量检索服务
- [ ] 旧 `embedding_documents` 表保留只读回退
- [ ] `DELETE /api/repositories/{repositoryId}/vectors/code` 和 `wiki` 向量清理接口已实现

## M4：复杂 Wiki 编排

- [ ] 四阶段生成编排已实现（仓库理解 → 结构规划 → 页面生成 → 全局收敛）
- [ ] 阶段 B 结构规划结果可存储为 `structure_json` 工件
- [ ] 阶段 D 全局收敛可检测重复页面、过大/过小页面、风格不一致等问题
- [ ] 页面关系分析服务可自动补全 `wiki_page_relations`
- [ ] 前端版本切换器组件可展示并切换历史版本
- [ ] 前端刷新面板组件支持分支选择、刷新策略、强制刷新、档位、Provider/Model 配置
- [ ] 前端 Wiki 页面底部展示"相关页面""前置阅读"导航
- [ ] 生成失败后可从中间工件恢复（resume）

## M5：版本对比

- [ ] `POST /api/repositories/{repositoryId}/wiki/compare` 可比较两个 Wiki 版本
- [ ] 对比结果包含新增页面、删除页面、标题变化、内容变化较大页面
- [ ] 可比较两个仓库快照版本的文件变更摘要
- [ ] 前端版本对比页面可展示结构化差异列表

## 通用验证

- [ ] `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj` 编译通过
- [ ] `npm run build`（frontend）编译通过
- [ ] `npm run lint`（frontend）无新增告警
- [ ] 所有新增实体、服务、控制器注释完备且使用中文
- [ ] 所有新增接口在非认证模式下可正常调用
- [ ] 数据库迁移可成功应用到 PostgreSQL + pgvector
