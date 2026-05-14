# Heimdall 架构升级 V2 Spec

## Why

当前系统存在标识体系割裂（前端 `owner/repo` 路由 vs 后端 `repositoryId`）、版本模型缺失（单版本覆盖式缓存）、向量模型粗糙（单表混合代码向量与内容向量）三大核心问题。本轮升级旨在将 Heimdall 从"当前仓库状态的生成缓存系统"重构为"以仓库主身份、代码快照版本、Wiki 生成版本、双向量知识域"为核心的仓库知识平台，为生成 50 页以上复杂编排 Wiki 奠定架构基础。

## What Changes

- 前端路由从 `/[owner]/[repo]` 迁移为 `/repositories/[repositoryId]` — **BREAKING**
- 后端 API 统一以 `repositoryId` 作为主标识，新增 `POST /api/repositories/import` 作为导入入口 — **BREAKING**
- `repositories` 表扩充：新增 `provider_type`、`provider_repository_key`、`display_name` 等字段，并建立唯一约束
- 新增 `repository_versions` 表：以 `(repository_id, branch_name, commit_sha)` 唯一标识仓库快照
- 新增 `wiki_spaces` 与 `wiki_versions` 表：分离"逻辑 Wiki 空间"与"生成版本"，支持同代码快照多轮生成
- 新增 `wiki_page_relations` 表：显式建模页面间关系（parent、depends_on、related_to 等）
- 新增 `code_embedding_chunks` 与 `wiki_embedding_chunks` 双向量表，替代单一 `embedding_documents` 表
- 任务表增加版本关联字段（repository_version_id、wiki_version_id、refresh_strategy 等）
- 增加对仓库版本、双向量表的清理接口 (`DELETE /api/repositories/{id}/vectors/code` 等)
- 定义三种刷新语义：刷新当前版本 / 刷新最新版本 / 强制刷新
- 引入发布态与历史态分离，支持版本切换、回滚与差异对比入口
- 生成链路升级为四阶段编排：仓库理解 → 结构规划 → 页面生成 → 全局收敛

## Impact

- Affected specs: 无（本次为新增独立变更）
- Affected code:
  - `frontend/src/app/[owner]/[repo]/**` — 路由重构
  - `frontend/src/components/ProcessedProjects.tsx` — 项目列表主键改造
  - `frontend/src/app/page.tsx` — 首页导入流程
  - `backend/Heimdall.Api/Controllers/` — Projects、WikiCache、Tasks、新增 Repositories、RepositoryVersions
  - `backend/Heimdall.Core/Entities/` — 更新 Repository；新增 RepositoryVersion、WikiSpace、WikiVersion、WikiPageRelation、CodeEmbeddingChunk、WikiEmbeddingChunk
  - `backend/Heimdall.Core/Interfaces/` — 新增对应服务接口
  - `backend/Heimdall.Core/Services/` — 新增版本发现、刷新编排、双向量检索服务
  - `backend/Heimdall.Repository/` — 新增实体配置、仓储、迁移
  - `backend/Heimdall.Infrastructure/Providers/` — 检索服务适配双向量表

## ADDED Requirements

### Requirement: 统一 repositoryId 主标识与元数据扩充

系统 SHALL 在所有前后端公开接口、页面 URL、任务请求、缓存读写中以 `repositoryId` 作为主标识。`owner/repo` 降级为展示属性与导入解析元数据，不再承担系统主键职责。同时 `repositories` 表需增加源平台标识字段，确保同一平台的同一仓库不会被重复导入。

#### Scenario: 仓库元数据更新与唯一约束

- **WHEN** 导入新仓库或更新现有仓库
- **THEN** 系统验证 `(provider_type, provider_repository_key)` 的唯一性
- **AND** 系统正确保存 `display_name`、`clone_url`、`is_archived` 等新增字段

#### Scenario: 用户通过仓库地址导入仓库

- **WHEN** 用户在首页输入仓库地址并提交
- **THEN** 系统调用 `POST /api/repositories/import`，解析地址并创建或复用仓库记录，返回 `repositoryId`
- **AND** 前端收到 `repositoryId` 后跳转至 `/repositories/{repositoryId}`

#### Scenario: 项目列表展示与操作

- **WHEN** 前端请求项目列表
- **THEN** 每项返回 `repository_id`、`display_name`、仓库类型、默认分支等信息
- **AND** 所有卡片跳转、删除操作均使用 `repositoryId`

#### Scenario: 前端路由访问仓库页

- **WHEN** 用户访问 `/repositories/{repositoryId}`
- **THEN** 页面根据 `repositoryId` 加载仓库信息、Wiki 内容、Slides、Workshop 等内容

### Requirement: 仓库快照版本

系统 SHALL 提供 `repository_versions` 表，以 `(repository_id, branch_name, commit_sha)` 唯一标识一个不可变仓库快照。每次刷新最新版本时，系统应查询远端 HEAD，若对应快照不存在则创建新的 `repository_version`。并提供针对版本查询的独立 API。

#### Scenario: 刷新最新版本时发现新提交

- **WHEN** 用户对某仓库触发"刷新最新版本"
- **THEN** 系统查询远端 HEAD 获取最新 `commit_sha`
- **AND** 若 `(repository_id, branch_name, commit_sha)` 在本地不存在，则创建新的 `repository_version`
- **AND** 基于该新快照创建 Wiki 生成任务

#### Scenario: 刷新最新版本时无新提交

- **WHEN** 远端 HEAD 对应的 `commit_sha` 与本地最新 `repository_version` 一致
- **THEN** 系统返回 `change_status: "unchanged"`，不创建新的 `repository_version`
- **AND** 若已有同配置的 `wiki_version`，则返回 `result_type: "reused"`

#### Scenario: 强制刷新不重复创建快照

- **WHEN** 用户触发强制刷新（`force_refresh: true`）
- **AND** 远端 HEAD 与本地最新 `repository_version` 一致
- **THEN** 系统不创建新的 `repository_version`
- **AND** 系统创建新的 `wiki_version` 生成任务

### Requirement: Wiki 版本管理

系统 SHALL 提供 `wiki_spaces` 与 `wiki_versions` 两张表。`wiki_spaces` 表示某仓库在某语言、某视角下的逻辑 Wiki 空间；`wiki_versions` 表示该空间在某个 `repository_version` 上的一次生成结果。同一 `repository_version` 可对应多个 `wiki_version`（因 Prompt、模型、档位不同）。

#### Scenario: 同代码版本更换模型重新生成

- **WHEN** 用户在同一 `repository_version` 上切换模型并触发刷新当前版本
- **THEN** 系统创建新的 `wiki_version`，`generation_mode` 为 `current`
- **AND** 新版本记录不同的 `model_profile_hash`

#### Scenario: 设置发布态

- **WHEN** 管理员将某个 `wiki_version` 设为发布态
- **THEN** 对应 `wiki_space.published_wiki_version_id` 更新为该版本 ID
- **AND** 默认浏览时展示发布态版本内容

#### Scenario: 前端切换历史 Wiki 版本

- **WHEN** 用户在版本切换器中选择一个历史 `wiki_version`
- **THEN** 页面加载该版本对应的 Wiki 页面树与内容

### Requirement: 双向量表与清理机制

系统 SHALL 提供 `code_embedding_chunks` 与 `wiki_embedding_chunks` 两张独立的向量表。代码向量表服务于代码理解、Ask 问答底座；Wiki 向量表服务于内容检索、语义导航。两者在生命周期、元数据、召回策略上完全独立。且必须提供对应的向量删除清理 API。

#### Scenario: 代码向量创建

- **WHEN** 新的 `repository_version` 创建后触发代码嵌入任务
- **THEN** 系统对仓库文件进行分块、嵌入，将结果写入 `code_embedding_chunks`
- **AND** 每条记录关联 `repository_version_id`

#### Scenario: Wiki 向量创建

- **WHEN** 新的 `wiki_version` 生成完成后触发内容嵌入任务
- **THEN** 系统对 Wiki 页面内容进行分块、嵌入，将结果写入 `wiki_embedding_chunks`
- **AND** 每条记录关联 `wiki_version_id` 与 `wiki_page_id`

#### Scenario: Ask 问答双向量召回

- **WHEN** 用户在仓库页发起 Ask 提问
- **THEN** 系统优先检索 `code_embedding_chunks`，辅助检索 `wiki_embedding_chunks`
- **AND** 结合页面树和页面关系对结果进行重排后返回

### Requirement: 页面关系模型

系统 SHALL 提供 `wiki_page_relations` 表，显式建模页面间关系，支持 `parent`、`depends_on`、`related_to`、`see_also`、`generated_from`、`diff_against` 等关系类型。

#### Scenario: Wiki 生成后补充页面关系

- **WHEN** 页面生成阶段完成后进入全局收敛阶段
- **THEN** 系统分析页面间引用与依赖，写入 `wiki_page_relations`
- **AND** 前端可基于关系数据展示"相关页面""前置阅读"等导航

### Requirement: 旧路由兼容跳转

系统 SHALL 在迁移期内对旧路由 `/[owner]/[repo]` 提供兼容跳转，自动重定向至 `/repositories/{repositoryId}`，避免旧收藏链接失效。

#### Scenario: 访问旧路由自动跳转

- **WHEN** 用户访问 `/[owner]/[repo]`
- **THEN** 系统根据 `owner/repo` 查询对应 `repositoryId`
- **AND** 返回 301 重定向至 `/repositories/{repositoryId}`

## MODIFIED Requirements

### Requirement: Wiki 缓存读写

**原行为**：Wiki 缓存基于 `owner/repo/repo_type/language` 进行读取、存储、删除，本质是单版本覆盖式缓存。

**新行为**：Wiki 内容通过 `wiki_space` → `wiki_version` → `wiki_pages` 三层模型管理。读取默认返回发布态版本，可指定 `wikiVersionId` 查看历史版本。删除按 `repositoryId` 或指定版本操作。

#### Scenario: 读取发布态 Wiki

- **WHEN** 前端请求 `/api/repositories/{repositoryId}/wiki` 不指定版本
- **THEN** 系统返回该仓库发布态 `wiki_version` 的页面树与内容

#### Scenario: 读取指定版本 Wiki

- **WHEN** 前端请求 `/api/repositories/{repositoryId}/wiki/versions/{wikiVersionId}`
- **THEN** 系统返回指定 `wiki_version` 的页面树与内容

### Requirement: 任务创建与追踪

**原行为**：任务创建依赖 `repo_url`，任务结果缺乏版本关联。

**新行为**：任务创建基于 `repository_id`，可选 `branch`、`refresh_strategy`、`force_refresh`。任务完成后绑定 `resolved_repository_version_id` 与 `result_wiki_version_id`，形成完整的版本追踪链路。

#### Scenario: 创建 Wiki 生成任务

- **WHEN** 前端调用 `POST /api/tasks/wiki` 传入 `repository_id`、`branch`、`refresh_strategy` 等参数
- **THEN** 系统校验参数并创建任务记录
- **AND** 任务执行过程中解析远端 HEAD，确定 `repository_version`，生成 `wiki_version`

## REMOVED Requirements

无。本轮不删除已有功能，采用双轨兼容策略：旧接口保留只读，旧路由做跳转，旧向量表保留一个版本周期后下线。
