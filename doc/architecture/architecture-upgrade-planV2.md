# Heimdall 架构升级改造方案 V2

## 1. 文档目的

本文档用于指导 Heimdall 下一轮架构升级改造，重点解决以下三类问题：

1. 统一以 `repositoryId` 作为前后端主标识，替代当前大量依赖 `owner/repo` 的路由与接口设计
2. 引入仓库快照多版本与 Wiki 多版本能力，为“历史回顾、差异对比、增量刷新、重复生成控制”提供稳定基础
3. 重构向量化与内容模型，使系统逐步具备生成 50 页以上、多层嵌套、复杂编排仓库 Wiki 的能力

本文档在 `architecture-upgrade-plan.md` 的基础上继续演进。V1 主要解决了数据库、分层、任务系统、管理后台等基础能力；V2 重点解决“统一主键、版本化、内容复杂化、检索升级、长期演进空间”问题。

## 2. 当前现状与核心不足

结合当前代码与已落地结构，现状可以概括为：

- 后端实体内部已经以 `Repository.Id` 作为数据库主键，但对外 API 仍大量通过 `repo_url` 或 `owner + repo + repo_type` 标识仓库
- 前端主页面、Slides、Workshop 仍然使用 `frontend/src/app/[owner]/[repo]/**` 路由
- 项目列表虽然已经返回数据库主键 `id`，但跳转、删除、缓存查询仍然基于 `owner/repo`
- Wiki 当前仍然是“同一仓库 + 分支 + 语言唯一一份”，本质是单版本覆盖式缓存
- 向量化当前只有一张 `embedding_documents` 表，且没有版本维度，也没有把“代码向量”和“生成内容向量”分开
- 生成链路更接近“仓库当前快照缓存系统”，还不是“面向长期沉淀的仓库知识系统”

这会直接带来以下问题：

### 2.1 标识体系割裂

- 数据层主键是 `repositoryId`
- 前端路由主键是 `owner/repo`
- 部分任务创建依赖 `repo_url`
- 缓存读取与清理依赖 `owner/repo/repo_type/language`

这种多套标识并存会导致：

- 前后端契约复杂
- 仓库重命名、迁移地址、镜像仓场景难以稳定处理
- 同一仓库的“逻辑身份”与“展示名称”耦合
- 后续版本切换、权限控制、分享链接设计都会变得别扭

### 2.2 版本模型缺失

当前系统虽然有 `SourceBranch` 字段，但整体仍是覆盖式写入，缺乏以下能力：

- 基于仓库快照的历史版本回看
- 基于版本的差异比较
- 基于版本的去重刷新
- 基于版本的多轮生成尝试追踪
- 同一代码版本下不同 Prompt/Profile 的结果并存

### 2.3 向量模型过于粗糙

单表向量化方案无法清晰支撑两类完全不同的检索需求：

- 代码理解检索：更关注文件、符号、结构、上下文连续性
- Wiki 内容检索：更关注页面段落、知识点、结论、编排关系

如果不分开建模，后续会出现：

- 嵌入粒度混乱
- 召回结果不稳定
- 重建成本高
- 无法支撑版本化回溯与差异分析

### 2.4 长期内容目标与数据模型不匹配

你的长期目标不是“生成一份简要总结”，而是生成“50 页以上、多层复杂嵌套、具备复杂编排的仓库解读 Wiki”。这意味着系统最终必须具备：

- 复杂目录树
- 页面间交叉引用
- 章节级摘要与导航
- 页面来源追踪
- 多视图输出能力
- 大规模生成的可恢复、可增量、可复用

当前只存一份 `content_markdown` 的模型能够跑通 MVP，但很难支撑复杂编排和长期演化。

## 3. V2 总体目标

V2 不追求一次性把所有长期能力全部做完，而是遵循“先统一主轴，再挂版本，再强化内容与检索”的原则。

### 3.1 阶段性目标

本轮改造必须优先达成以下结果：

1. 前后端统一以 `repositoryId` 作为主标识
2. 引入稳定的 `RepositoryVersion` 仓库快照模型
3. 引入独立的 `WikiVersion` 生成结果模型
4. 把向量化拆成“代码向量域”和“生成内容向量域”
5. 建立“刷新当前版本 / 刷新最新版本 / 强制刷新”三种明确语义
6. 让前端具备版本切换与后续差异展示的基础能力

### 3.2 长期目标

在不推翻本轮设计的前提下，持续演进到以下能力：

- 支持 50 页以上大规模 Wiki 自动生成
- 支持 3 层以上目录嵌套与复杂页面编排
- 支持按主题、按模块、按视角的多入口浏览
- 支持代码版本差异与 Wiki 版本差异
- 支持跨页面图谱导航、知识聚合、问答溯源
- 支持从 Wiki 继续派生 Slides、Workshop、FAQ、训练营材料

## 4. V2 核心设计原则

### 4.1 `repositoryId` 是唯一主标识

从 V2 开始，所有前后端公开接口、页面 URL、任务请求、缓存读取、版本切换，都以 `repositoryId` 为主标识。

`owner/repo` 的角色调整为：

- 展示名称
- 兼容旧链接时的辅助信息
- 仓库地址解析与导入时的元数据

不再承担系统主键职责。

### 4.2 “仓库快照版本”与“Wiki 生成版本”必须分离

这两个概念不能混在一起：

- `RepositoryVersion`：表示代码仓库在某个分支、某个提交哈希下的不可变快照
- `WikiVersion`：表示针对某个仓库快照，按某种生成配置得到的一次 Wiki 结果

这样做的好处是：

- 同一个代码版本可以多次生成
- 可以区分“代码变了”和“Prompt/模型变了”
- 后续支持重跑、A/B 生成、质量回归、差异对比

### 4.3 代码知识与文档知识双向量域分治

至少拆成两张主向量表：

- `code_embedding_chunks`
- `wiki_embedding_chunks`

它们分别服务于不同场景：

- 代码向量表用于代码理解、符号追踪、上下文补全、Ask 问答底座
- Wiki 向量表用于页面检索、语义导航、内容推荐、历史版本差异分析

### 4.4 数据库为唯一信源，文件系统仅作临时执行空间

继续坚持 V1 的方向，但在 V2 中进一步强化：

- 仓库克隆目录只用于任务执行期间
- 仓库快照元数据、Wiki 版本、页面树、向量块、任务记录全部存数据库
- 所有“当前缓存”概念都转成“某个版本是否被标记为当前发布版本”

### 4.5 优先设计可增量体系，而不是全量覆盖体系

随着仓库规模变大，后续生成 50+ 页复杂 Wiki 时，不应每次都整仓重做。V2 的设计必须允许：

- 仅为新增仓库快照创建新版本
- 仅重建发生变化的代码向量块
- 仅重生成受影响页面
- 仅重嵌入受影响的 Wiki 内容块

第一阶段不一定实现完整增量，但表结构与任务链路必须为此预留空间。

## 5. 目标架构总览

```text
┌────────────────────────────────────────────────────────────────────┐
│ 前端层                                                            │
│ repositories/[repositoryId]                                       │
│ ├─ Wiki 浏览                                                      │
│ ├─ 版本切换 / 版本对比                                            │
│ ├─ 当前版本刷新 / 最新版本刷新 / 强制刷新                         │
│ ├─ Ask / Slides / Workshop                                        │
│ └─ 发布态与历史态浏览                                             │
├────────────────────────────────────────────────────────────────────┤
│ API 层                                                            │
│ ├─ Repository API                                                 │
│ ├─ Repository Version API                                         │
│ ├─ Wiki Version API                                               │
│ ├─ Refresh / Publish API                                          │
│ ├─ Task API                                                       │
│ └─ Compare API                                                    │
├────────────────────────────────────────────────────────────────────┤
│ 核心业务层                                                        │
│ ├─ 仓库导入与主数据服务                                            │
│ ├─ 版本发现与快照登记服务                                          │
│ ├─ Wiki 规划与页面编排服务                                          │
│ ├─ 增量生成编排服务                                                 │
│ ├─ 双向量检索服务                                                   │
│ └─ 发布与回滚服务                                                   │
├────────────────────────────────────────────────────────────────────┤
│ 数据层                                                            │
│ ├─ repositories                                                   │
│ ├─ repository_versions                                            │
│ ├─ wiki_versions / wiki_pages / wiki_page_relations               │
│ ├─ code_embedding_chunks                                          │
│ ├─ wiki_embedding_chunks                                          │
│ ├─ tasks / task_steps / task_artifacts                            │
│ └─ compare / lineage / metrics                                    │
└────────────────────────────────────────────────────────────────────┘
```

## 6. 数据模型升级方案

## 6.1 仓库主数据：`repositories`

`repositories` 继续作为“逻辑仓库主表”，但职责要更明确。

建议字段：

| 字段 | 说明 |
|------|------|
| `id` | 仓库主键，也就是公开使用的 `repositoryId` |
| `provider_type` | `github` / `gitlab` / `bitbucket` / `local` |
| `provider_repository_key` | 上游平台可稳定识别的仓库键，优先使用平台原生 ID，没有时再降级 |
| `owner` | 展示用 |
| `repo_name` | 展示用 |
| `display_name` | 推荐新增，形如 `owner/repo` |
| `repo_url` | 仓库访问地址 |
| `clone_url` | 克隆地址 |
| `default_branch` | 默认分支 |
| `is_archived` | 是否归档 |
| `created_at` / `updated_at` | 审计字段 |

关键约束：

- `id` 作为公开主键
- `(provider_type, provider_repository_key)` 唯一
- 兼容没有平台原生 ID 的场景时，可保留 `(owner, repo_name, provider_type)` 唯一约束作为兜底

### 6.1.1 为什么不直接删除 `owner/repo`

不建议删除，而是降级为展示属性。原因如下：

- 前端展示仍需要自然可读名称
- 导入流程仍要从 URL 解析
- 历史数据迁移也需要它们作为映射依据

## 6.2 仓库快照版本：`repository_versions`

这是 V2 最关键的新表。

### 6.2.1 版本定义

一个仓库快照版本由以下维度唯一确定：

- `repository_id`
- `branch_name`
- `commit_sha`

这与您的设想一致，但我建议再额外记录：

- `tree_sha` 或内容指纹
- `commit_time`
- `author_name`
- `commit_message`
- `discovered_at`

建议字段：

| 字段 | 说明 |
|------|------|
| `id` | 仓库快照版本主键 |
| `repository_id` | 所属仓库 |
| `branch_name` | 分支名 |
| `commit_sha` | 当前提交哈希 |
| `tree_fingerprint` | 文件树指纹，用于快速比较 |
| `commit_time` | 提交时间 |
| `commit_author` | 提交作者 |
| `commit_message` | 提交说明摘要 |
| `source_status` | `active` / `superseded` / `deleted` |
| `is_latest_on_branch` | 是否为该分支最新发现版本 |
| `created_at` | 首次登记时间 |

关键约束：

- 唯一索引：`(repository_id, branch_name, commit_sha)`

### 6.2.2 设计意义

`repository_versions` 是所有版本化能力的基础锚点：

- Wiki 版本挂在它上面
- 代码向量挂在它上面
- 差异分析基于它做
- “刷新最新版本”本质是先发现新的 `repository_version`

## 6.3 Wiki 逻辑空间与生成版本

为了兼顾当前落地效率和未来扩展，我建议拆成三层，而不是只用一张 `wikis` 表硬扛全部语义。

### 6.3.1 `wiki_spaces`

表示某个仓库在某种语言、某种内容视角下的逻辑 Wiki 空间。

建议字段：

| 字段 | 说明 |
|------|------|
| `id` | Wiki 空间主键 |
| `repository_id` | 所属仓库 |
| `language` | 语言，当前仍以中文为主 |
| `view_type` | `default` / `architecture` / `onboarding` / `security` 等 |
| `title` | 逻辑 Wiki 标题 |
| `description` | 逻辑 Wiki 描述 |
| `published_wiki_version_id` | 当前发布中的版本 |
| `created_at` / `updated_at` | 审计字段 |

说明：

- 如果当前阶段只做中文默认 Wiki，可以先固定 `language=zh`、`view_type=default`
- 之所以建议保留 `wiki_spaces`，是为了未来扩展不同视角输出时不需要重新拆表

### 6.3.2 `wiki_versions`

表示某个 `wiki_space` 在某个 `repository_version` 上的一次生成结果。

建议字段：

| 字段 | 说明 |
|------|------|
| `id` | Wiki 版本主键 |
| `wiki_space_id` | 所属逻辑 Wiki |
| `repository_version_id` | 基于哪个仓库快照生成 |
| `version_no` | Wiki 版本号，仓库内递增 |
| `generation_mode` | `current` / `latest` / `rebuild` |
| `generation_profile` | 生成档位，例如 `concise` / `comprehensive` |
| `prompt_profile_hash` | Prompt 模板版本摘要 |
| `model_profile_hash` | Provider + Model 配置摘要 |
| `status` | `draft` / `generating` / `ready` / `published` / `failed` / `superseded` |
| `is_force_refresh` | 是否强制刷新生成 |
| `page_count` | 页面数量 |
| `toc_depth` | 目录深度 |
| `summary_markdown` | 版本摘要 |
| `created_by_task_id` | 来源任务 |
| `created_at` / `completed_at` | 审计字段 |

### 6.3.3 为什么 `WikiVersion` 不能直接等同于 `RepositoryVersion`

因为同一个代码版本可能出现多种合法结果：

- Prompt 模板升级后重新生成
- 更换模型后重新生成
- 同一版本先快速生成，再高质量补生成
- 同一版本需要人工回滚到上一个发布态

因此必须允许“多个 `wiki_versions` 对应同一个 `repository_version`”。

## 6.4 页面模型：面向复杂编排而非纯 Markdown 缓存

为了支撑 50+ 页复杂 Wiki，不建议继续只存“页面标题 + 正文”。

建议页面层至少包含以下表：

### 6.4.1 `wiki_pages`

| 字段 | 说明 |
|------|------|
| `id` | 页面主键 |
| `wiki_version_id` | 所属 Wiki 版本 |
| `parent_page_id` | 父页面 |
| `slug` | 页面稳定标识 |
| `title` | 页面标题 |
| `nav_title` | 导航标题 |
| `page_type` | `section` / `article` / `overview` / `appendix` |
| `sort_order` | 排序 |
| `depth` | 层级深度 |
| `content_markdown` | 页面正文 |
| `outline_json` | 页面结构化目录 |
| `summary` | 页面摘要 |
| `source_coverage_json` | 来源文件、符号、版本覆盖信息 |
| `token_count` | 页面大小估算 |
| `status` | `ready` / `stale` / `generating` |
| `created_at` / `updated_at` | 审计字段 |

### 6.4.2 `wiki_page_relations`

用于表达页面间关系，而不是把所有关系都塞进正文里。

关系类型建议包括：

- `parent`
- `depends_on`
- `related_to`
- `see_also`
- `generated_from`
- `diff_against`

这张表在长期内非常重要，因为复杂 Wiki 的浏览体验不应只依赖树形目录。

### 6.4.3 可选预留：`wiki_page_blocks`

如果未来要支持更细粒度编排，可以新增页面块级表，按块存储：

- 引言块
- 表格块
- 代码示例块
- FAQ 块
- 时序图说明块
- 总结块

V2 第一阶段可以先不落地，但建议在文档中明确为后续预留。

## 6.5 双向量表设计

### 6.5.1 代码向量表：`code_embedding_chunks`

该表服务于代码理解，是 Ask、结构规划、页面生成的底层语义索引。

建议字段：

| 字段 | 说明 |
|------|------|
| `id` | 主键 |
| `repository_version_id` | 所属仓库快照 |
| `file_path` | 文件路径 |
| `symbol_path` | 可选，类/函数/命名空间路径 |
| `chunk_index` | 块序号 |
| `chunk_type` | `file_summary` / `code_block` / `symbol_body` / `readme` |
| `language` | 源码语言 |
| `start_line` / `end_line` | 行范围 |
| `content_raw` | 原始块内容 |
| `content_normalized` | 规范化后文本 |
| `content_hash` | 内容哈希 |
| `token_count` | Token 数 |
| `embedding_model` | 使用的嵌入模型 |
| `embedding_vector` | 向量 |
| `created_at` | 创建时间 |

关键约束：

- 索引：`(repository_version_id, file_path, chunk_index)`
- 唯一性建议用 `(repository_version_id, content_hash, chunk_index)` 或业务可接受的组合约束

### 6.5.2 生成内容向量表：`wiki_embedding_chunks`

该表服务于 Wiki 内容理解，是内容检索、历史比较、推荐相似页面的底层索引。

建议字段：

| 字段 | 说明 |
|------|------|
| `id` | 主键 |
| `wiki_version_id` | 所属 Wiki 版本 |
| `wiki_page_id` | 所属页面 |
| `chunk_index` | 块序号 |
| `chunk_type` | `title` / `summary` / `section` / `faq` / `table_text` |
| `content_raw` | 原始文本 |
| `content_hash` | 内容哈希 |
| `token_count` | Token 数 |
| `embedding_model` | 嵌入模型 |
| `embedding_vector` | 向量 |
| `created_at` | 创建时间 |

### 6.5.3 为什么不能只靠 `is_code` 区分

因为这两类数据在以下方面完全不同：

- 生命周期不同
- 更新触发条件不同
- 召回策略不同
- 元数据字段不同
- 未来索引参数也可能不同

因此必须是物理分表，而不是逻辑分表。

## 6.6 任务与版本关系重构

当前 `tasks` 表已经存在，但在 V2 中建议明确它与版本之间的关系。

建议增加或明确以下字段：

| 字段 | 说明 |
|------|------|
| `repository_id` | 逻辑仓库 |
| `target_branch` | 目标分支 |
| `requested_repository_version_id` | 若用户指定刷新当前版本，则直接绑定 |
| `resolved_repository_version_id` | 实际生成使用的仓库快照 |
| `result_wiki_version_id` | 生成出的 Wiki 版本 |
| `refresh_strategy` | `current` / `latest` |
| `force_refresh` | 是否强制刷新 |
| `config_hash` | 生成配置摘要 |
| `dedup_scope` | 去重范围 |

这样后续每个任务都能回答以下问题：

- 这次是基于哪个版本生成的
- 为什么没有生成新版本
- 这次生成是否只是重跑
- 结果落到了哪个 `wiki_version`

## 7. 刷新与版本发现语义

这是 V2 交互设计中的关键部分，必须在后端语义上一次定义清楚。

## 7.1 刷新当前版本

适用场景：

- 用户正在浏览某个历史版本
- 希望在不切换代码快照的前提下重新生成内容
- 用于更换模型、Prompt、生成档位后的重跑

处理逻辑：

1. 读取当前页面对应的 `repository_version_id`
2. 如果未勾选 `强制刷新`，先检查是否已有同配置生成结果
3. 命中则直接返回已有 `wiki_version`
4. 未命中则创建新的 `wiki_version` 生成任务

## 7.2 刷新最新版本

适用场景：

- 用户希望同步远端仓库最新状态

处理逻辑：

1. 根据 `repositoryId` 和选定分支查询远端 HEAD
2. 获取最新 `commit_sha`
3. 查询本地是否已有 `(repository_id, branch_name, commit_sha)` 对应的 `repository_version`
4. 若不存在，则创建新的 `repository_version`
5. 再基于该快照去决定是否需要生成新的 `wiki_version`

## 7.3 强制刷新

强制刷新只影响“是否允许在相同版本、相同配置下继续重跑”，不改变版本发现逻辑。

建议规则：

- `force_refresh = false`
  - 如果目标版本与当前最新已生成版本完全一致，直接返回 `no_change`
  - 如果版本不同，则正常生成
- `force_refresh = true`
  - 即使版本一致，也允许新建生成任务
  - 但仍然不重复创建新的 `repository_version`

换句话说：

- `RepositoryVersion` 是不可变快照，不因强制刷新重复创建
- `WikiVersion` 可以因强制刷新而重建

## 7.4 推荐返回语义

刷新接口应返回明确的结果类型，而不是只返回成功或失败：

```json
{
  "task_id": "xxx",
  "repository_id": "xxx",
  "repository_version_id": "xxx",
  "wiki_version_id": "xxx",
  "result_type": "queued",
  "refresh_strategy": "latest",
  "change_status": "changed"
}
```

其中：

- `result_type` 可取 `queued` / `reused` / `no_change`
- `change_status` 可取 `changed` / `unchanged`

这样前端才能准确提示用户“无变化”“复用了已有版本”还是“已开始重新生成”。

## 8. API 与路由改造方案

## 8.1 前端路由重构

建议从：

```text
/[owner]/[repo]
/[owner]/[repo]/slides
/[owner]/[repo]/workshop
```

迁移为：

```text
/repositories/[repositoryId]
/repositories/[repositoryId]/slides
/repositories/[repositoryId]/workshop
```

同时增加版本参数：

- 默认浏览当前发布版本：`/repositories/[repositoryId]`
- 指定 Wiki 版本：`/repositories/[repositoryId]?wikiVersionId=...`
- 指定仓库快照版本：`/repositories/[repositoryId]?repositoryVersionId=...`

推荐原则：

- 页面路径只体现逻辑仓库身份
- 版本通过 Query 参数或子资源接口控制
- 避免把 `branch`、`commit` 直接塞进路径导致 URL 过长和语义复杂

## 8.2 后端接口重构

建议新增以下主接口组：

### 8.2.1 仓库主数据

```text
POST   /api/repositories/import
GET    /api/repositories
GET    /api/repositories/{repositoryId}
PATCH  /api/repositories/{repositoryId}
DELETE /api/repositories/{repositoryId}
```

说明：

- `import` 接口负责根据 `repo_url` 创建或复用仓库记录，并立即返回 `repositoryId`
- 这一步是前端从“用户输入 URL”过渡到“按 repositoryId 跳转”的关键桥梁

### 8.2.2 仓库版本

```text
GET  /api/repositories/{repositoryId}/versions
GET  /api/repositories/{repositoryId}/versions/{repositoryVersionId}
POST /api/repositories/{repositoryId}/versions/discover
GET  /api/repositories/{repositoryId}/versions/latest?branch=main
```

### 8.2.3 Wiki 版本

```text
GET  /api/repositories/{repositoryId}/wiki
GET  /api/repositories/{repositoryId}/wiki/versions
GET  /api/repositories/{repositoryId}/wiki/versions/{wikiVersionId}
POST /api/repositories/{repositoryId}/wiki/refresh
POST /api/repositories/{repositoryId}/wiki/publish
POST /api/repositories/{repositoryId}/wiki/compare
```

### 8.2.4 删除与清理

```text
DELETE /api/repositories/{repositoryId}/wiki
DELETE /api/repositories/{repositoryId}/wiki/versions/{wikiVersionId}
DELETE /api/repositories/{repositoryId}/vectors/code?repositoryVersionId=...
DELETE /api/repositories/{repositoryId}/vectors/wiki?wikiVersionId=...
```

### 8.2.5 任务接口

```text
POST /api/tasks/wiki
GET  /api/tasks/{taskId}
GET  /api/tasks/{taskId}/status
GET  /api/tasks/{taskId}/stream
```

其中 `POST /api/tasks/wiki` 的请求体建议改为：

```json
{
  "repository_id": "uuid",
  "branch": "main",
  "refresh_strategy": "latest",
  "force_refresh": false,
  "generation_profile": "comprehensive",
  "provider": "ollama",
  "model": "gemma4:e2b"
}
```

## 8.3 兼容期策略

为了降低迁移风险，不建议一步删除旧接口。

建议采用双轨兼容：

### 第一阶段

- 保留旧接口
- 新增 `repositoryId` 风格接口
- 前端逐步切换

### 第二阶段

- 旧接口内部全部转调新服务
- 旧路由仅做兼容跳转

### 第三阶段

- 观测稳定后再删除旧接口和旧页面目录

## 9. 面向复杂 Wiki 的生成编排升级

如果目标是 50+ 页复杂 Wiki，生成链路不能只是“先出结构，再逐页写正文”的单线程模型。

V2 建议升级为四阶段编排。

## 9.1 阶段 A：仓库理解与主题提取

输入：

- `repository_version`
- 文件树
- README
- 配置文件
- 代码向量检索能力

输出：

- 仓库主题画像
- 模块边界
- 关键流程
- 优先解释域
- 候选页面池

## 9.2 阶段 B：Wiki 结构规划

输出内容不再只是简单页面列表，而应包括：

- 一级到三级目录
- 每页目标
- 页面粒度
- 依赖关系
- 必须引用的关键文件
- 是否需要专门的总览页、附录页、词汇表页

建议把结构规划结果单独存为：

- `wiki_versions.structure_json`
- 或 `task_artifacts` 中的结构工件

这样后续即使正文生成失败，也能恢复继续跑。

## 9.3 阶段 C：页面生成与交叉引用补全

页面生成不应只关心单页正文，还应补齐：

- 页面摘要
- 相关页面
- 来源文件
- 核心符号
- 前置阅读建议

这一步完成后再写 `wiki_page_relations`。

## 9.4 阶段 D：全局收敛与编排修正

这是当前很多 Wiki 生成系统缺失的一步，但对大规模复杂文档非常重要。

建议增加最终收敛过程，专门检查：

- 是否有重复页面
- 是否有过大的页面需要拆分
- 是否有过小页面需要合并
- 是否缺少总览页
- 页面标题风格是否一致
- 引用链是否完整

这一步将直接决定“50 页以上 Wiki”是否真的可读。

## 9.5 未来增强：增量页面重建

后续应进一步支持：

- 根据变更文件推断受影响模块
- 根据模块映射推断受影响页面
- 仅对受影响页面重新生成

这对大型仓库成本控制非常关键。V2 第一轮可以先实现“整版重建”，但任务图与表结构必须为“页面级增量”预留接口。

## 10. 检索与问答升级方案

V2 之后，Ask、Wiki、Slides、Workshop 都应共享统一的知识底座。

## 10.1 双向量召回策略

建议问答检索分三步：

1. 优先检索 `code_embedding_chunks`
2. 辅助检索 `wiki_embedding_chunks`
3. 再结合页面树和页面关系做结果重排

这会比当前单向量表策略更稳定，因为：

- 代码问题优先依赖代码块
- 概念问题优先依赖 Wiki 段落
- 复杂问题可以同时利用两类结果

## 10.2 差异问答

有了 `repository_versions` 和 `wiki_versions` 后，可以逐步支持：

- “这个模块相较上个版本有哪些变化”
- “为什么本次 Wiki 多了一个新的章节”
- “认证模块在 `commit A` 和 `commit B` 之间如何演变”

这类能力会显著提升 Heimdall 的产品价值。

## 10.3 结果溯源

长期建议把每个页面、每个问答结果都绑定来源：

- 来源页面
- 来源文件
- 来源版本
- 来源任务

这样后续不论是比较、导出、审计、回滚都会更稳。

## 11. 前端交互升级方案

## 11.1 版本切换器

仓库主页需要新增版本切换器，至少支持：

- 当前发布版本
- 当前分支最新代码版本
- 历史 Wiki 版本
- 历史仓库快照版本

推荐 UI 文案：

- 当前浏览版本
- 基于提交
- 分支
- 生成时间
- 生成配置

## 11.2 刷新面板

刷新操作建议改为显式表单，而不是单一按钮。

最小交互项：

- 分支选择
- 刷新策略：`当前版本` / `最新版本`
- 是否强制刷新
- 生成档位：`简洁` / `完整`
- Provider / Model

## 11.3 项目列表主键改造

项目列表卡片应直接以 `repositoryId` 作为跳转与删除主键。

建议列表接口返回：

```json
{
  "repository_id": "uuid",
  "display_name": "owner/repo",
  "repo_type": "gitlab",
  "default_branch": "main",
  "latest_wiki_version_id": "uuid",
  "published_wiki_version_id": "uuid",
  "updated_at": "2026-05-14T12:00:00Z"
}
```

## 11.4 版本对比入口

V2 先不要求做完整可视化 diff，但要先预留入口：

- 对比两个仓库快照版本
- 对比两个 Wiki 版本

第一阶段可以只返回结构化摘要：

- 新增页面
- 删除页面
- 标题变化
- 内容变化较大的页面

## 12. 数据迁移与落地策略

V2 不是纯新增能力，还涉及现有数据迁移与接口切换。建议按以下顺序推进。

## 12.1 阶段一：统一主标识与导入链路

目标：

- 新增 `POST /api/repositories/import`
- 前端在用户输入仓库地址后，先拿 `repositoryId` 再跳转
- 项目列表、仓库页、Slides、Workshop 全部改为 `repositoryId` 路由
- 新增按 `repositoryId` 读取/删除 Wiki 的接口

收益：

- 先把主轴统一
- 不立即引入复杂版本迁移，风险最低

## 12.2 阶段二：引入 `repository_versions`

目标：

- 创建新表
- 将现有每个仓库的“当前主分支结果”回填为一个初始 `repository_version`
- 生成任务开始显式解析远端 HEAD

迁移策略：

- 历史老数据若拿不到准确 `commit_sha`，可先写入 `unknown` 或通过当前远端补探测，但必须在字段上标记“可信度”
- 建议新增 `version_source_confidence` 字段，区分 `exact` / `inferred` / `unknown`

## 12.3 阶段三：引入 `wiki_spaces` 与 `wiki_versions`

目标：

- 把原有 `wikis` + `wiki_pages` 数据迁移到新模型
- 每条历史 Wiki 先映射为一个默认 `wiki_space`
- 每份现有页面结果回填为一个初始 `wiki_version`

注意：

- 不要直接覆盖旧表后再迁移
- 应采用“新表回填 + 双写兼容 + 验证后切换”

## 12.4 阶段四：拆分双向量表

目标：

- 新增 `code_embedding_chunks`
- 新增 `wiki_embedding_chunks`
- 代码嵌入与 Wiki 内容嵌入分开重建

策略：

- 旧 `embedding_documents` 先保留只读
- 新链路优先写新表
- 问答检索先支持双表回退
- 验证完成后再废弃旧表

## 12.5 阶段五：页面关系与复杂编排能力

目标：

- 新增 `wiki_page_relations`
- 页面生成后补关系
- 新增全局收敛步骤
- 逐步支持大规模复杂 Wiki

## 13. 数据库表示例

以下为推荐的新表集合，供后续细化为正式迁移脚本：

### 核心主表

- `repositories`
- `repository_versions`
- `wiki_spaces`
- `wiki_versions`
- `wiki_pages`
- `wiki_page_relations`
- `tasks`
- `task_steps`
- `task_artifacts`

### 向量表

- `code_embedding_chunks`
- `wiki_embedding_chunks`

### 辅助表

- `repository_version_diffs`
- `wiki_version_diffs`
- `wiki_publish_logs`

说明：

- `task_steps` 用于记录结构规划、页面生成、收敛修正等阶段进度
- `task_artifacts` 用于保存结构规划结果、差异摘要、页面生成中间工件

## 14. 关键架构决策

## AD1：前端路由与对外 API 统一使用 `repositoryId`

理由：

- 契约统一
- 路由稳定
- 更符合 REST 风格
- 为权限、分享、版本切换打基础

## AD2：引入 `RepositoryVersion` 作为仓库快照锚点

理由：

- 解决“代码版本”和“生成版本”混淆问题
- 为增量、差异、历史回看奠定基础

## AD3：引入 `WikiVersion` 作为生成结果锚点

理由：

- 支持同代码快照下多轮生成
- 支持发布、回滚、重建

## AD4：代码向量与 Wiki 向量必须物理分表

理由：

- 检索目标不同
- 生命周期不同
- 元数据不同
- 后续索引策略不同

## AD5：发布态与历史态分离

理由：

- 用户默认浏览发布态
- 管理端可切换历史态
- 便于回滚与比较

## 15. 验收标准

## 15.1 第一阶段验收

- 首页输入仓库地址后，可先得到 `repositoryId` 再跳转
- 项目列表卡片、删除、详情页全部不再依赖 `owner/repo`
- `GET/DELETE Wiki` 均支持按 `repositoryId`
- 旧链接访问后可自动兼容跳转到新链接

## 15.2 第二阶段验收

- 同一仓库同一分支不同提交可产生多个 `repository_version`
- 刷新最新版本时，能够识别“无变化”与“有新版本”
- 强制刷新不会重复创建 `repository_version`

## 15.3 第三阶段验收

- 同一 `repository_version` 可对应多个 `wiki_version`
- 前端可以切换历史 Wiki 版本
- 可设置某个版本为发布态

## 15.4 第四阶段验收

- 代码向量与 Wiki 向量分表落地
- Ask 问答可同时利用双向量域
- 旧向量表可平滑下线

## 15.5 第五阶段验收

- 能稳定生成 50 页以上 Wiki
- 目录层级可达到 3 层以上
- 页面间关系与引用链可用
- 生成失败后可从中间工件恢复

## 16. 风险与应对

## 16.1 历史数据缺少准确提交哈希

风险：

- 旧 Wiki 很可能无法精确映射到真实历史提交

应对：

- 允许迁移为 `unknown` 或 `inferred`
- 在 UI 上明确标识版本可信度

## 16.2 一次性切换路由风险过大

风险：

- 旧收藏链接失效
- 前端多个页面联动回归范围大

应对：

- 先加新路由
- 再做旧路由跳转
- 最后删除旧目录

## 16.3 双向量表重建成本高

风险：

- 首次迁移时会产生较大计算开销

应对：

- 分批回填
- 先按活跃仓库优先
- 旧表回退能力保留一个版本周期

## 16.4 大规模 Wiki 生成质量不稳定

风险：

- 页面可能重复、松散、结构失衡

应对：

- 新增全局收敛阶段
- 引入页面关系与结构工件
- 对超大仓库优先做模块级拆分生成

## 17. 推荐实施顺序

如果只看投入产出比，建议按以下顺序执行：

1. 先统一 `repositoryId` 路由与 API
2. 再引入 `repository_versions`
3. 再引入 `wiki_versions`
4. 然后拆双向量表
5. 最后补页面关系、差异比较、复杂编排

这样做的原因是：

- 先统一主键，整个系统主轴才稳定
- 先挂仓库快照，版本体系才有锚点
- 先有版本，再谈差异、发布、复杂内容
- 先把底层结构稳住，再做大规模高质量生成

## 18. 对下一步落地的直接建议

建议你把下一轮实际开发拆成 3 个可独立合并的里程碑。

### 里程碑 M1：主标识统一

改造范围：

- `frontend/src/app/[owner]/[repo]/**`
- `frontend/src/components/ProcessedProjects.tsx`
- `frontend/src/app/page.tsx`
- `backend/Heimdall.Api/Controllers/ProjectsController.cs`
- `backend/Heimdall.Api/Controllers/WikiCacheController.cs`
- 新增 `RepositoriesController`

目标：

- 页面、接口、任务请求全部可按 `repositoryId` 工作

### 里程碑 M2：版本化底座

改造范围：

- 新增 `RepositoryVersion` 实体、配置、仓储、迁移
- 改造 Wiki 刷新流程
- 改造任务结果落库

目标：

- 真正具备仓库快照多版本能力

### 里程碑 M3：双向量 + 复杂 Wiki 预备能力

改造范围：

- 新增双向量表
- 改造检索服务
- 新增页面关系与结构工件

目标：

- 为高质量大规模 Wiki 生成打底

## 19. 结论

V2 的本质不是“把几个字段换成 `repositoryId`”，而是要把 Heimdall 从“当前仓库状态的生成缓存系统”升级为“以仓库主身份、代码快照版本、Wiki 生成版本、双向量知识域”为核心的仓库知识平台。

如果这次升级设计正确，后续你要实现的长期目标，包括：

- 50 页以上复杂 Wiki
- 历史版本回看
- 差异对比
- 页面级增量重建
- 更强的 Ask、Slides、Workshop 派生能力

都将建立在统一且可持续演进的基础之上。

从改造优先级来看，最值得立刻启动的是：

1. `repositoryId` 路由与 API 统一
2. `RepositoryVersion` 快照版本模型
3. `WikiVersion` 生成版本模型
4. 双向量表拆分

这四步做完，Heimdall 才算真正拥有面向下一阶段增长的架构底座。
