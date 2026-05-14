# Tasks

- [ ] Task 1: 完成阶段 0 止血与契约收敛
  - [ ] SubTask 1.1: 梳理并修正 Wiki 刷新、任务创建、任务状态返回的唯一正式接口与返回字段
  - [x] SubTask 1.2: 改造 `TasksController` 与 `TaskQueueService`，确保 Wiki 任务只通过统一后台执行器运行
  - [x] SubTask 1.3: 修正 `WikiVersionController`、`WikiCacheController` 中 `WikiId` / `WikiSpaceId` / `WikiVersionId` 混用问题
  - [ ] SubTask 1.4: 改造前端仓库页、刷新面板、版本切换逻辑，移除刷新后回退到旧 `generateWikiTask` 的双链路
  - [ ] SubTask 1.5: 验证前端页面、刷新、版本切换、任务态与后端契约保持一致

- [x] Task 2: 完成阶段 1 任务可靠性与版本闭环
  - [x] SubTask 2.1: 设计并落地任务阶段状态与 `task_artifacts` 工件持久化模型
  - [x] SubTask 2.2: 为结构规划、页面批次、关系补全、收敛报告、渲染结果建立工件写入与读取路径
  - [x] SubTask 2.3: 明确 Wiki 主数据、版本数据、页面数据、任务状态的事务边界与完成态校验
  - [x] SubTask 2.4: 取消主链路中的火忘式 `Task.Run`，将代码向量与 Wiki 向量写入纳入可观测执行阶段
  - [x] SubTask 2.5: 验证任务失败恢复、重试、完成态一致性与版本回写一致性

- [x] Task 3: 完成阶段 2 生成模型重构
  - [x] SubTask 3.1: 将结构规划从 XML 主导迁移为 JSON DTO 或严格结构化文本工件
  - [x] SubTask 3.2: 重构页面生成链路，输出标准 Markdown 页面草案、Frontmatter 与必要结构块元数据
  - [x] SubTask 3.3: 落地全局收敛服务，覆盖重复检测、拆页合页、标题风格统一、前置阅读与交叉引用修补
  - [x] SubTask 3.4: 增加渲染后处理环节，输出供前端稳定消费的页面树与页面内容结构
  - [x] SubTask 3.5: 验证页面内容不再依赖原始 HTML 作为主表达格式

- [x] Task 4: 让 Ask、Slides、Workshop 并轨到统一知识底座
  - [x] SubTask 4.1: 改造 Ask 链路，显式继承当前 `RepositoryVersion` / `WikiVersion` 并接入稳定的双向量检索
  - [x] SubTask 4.2: 改造 Slides 生成链路，基于版本化页面和渲染工件派生内容
  - [x] SubTask 4.3: 改造 Workshop 生成链路，基于版本化页面和渲染工件派生内容
  - [x] SubTask 4.4: 验证 Ask、Slides、Workshop 与当前浏览版本保持一致

- [ ] Task 5: 完成数据库、迁移与调试环境联调验证
  - [ ] SubTask 5.1: 使用提供的 PostgreSQL 环境完成迁移、连接、向量能力与主链路配置校验
  - [ ] SubTask 5.2: 使用提供的 Ollama 向量与生成服务完成双向量写入、任务执行与生成验证
  - [ ] SubTask 5.3: 以 `http://gitlab.beisencorp.com/AppCenter/Beisen.AppCenter.Ops` 作为目标仓库完成端到端验证
  - [ ] SubTask 5.4: 完成后端构建、前端构建、关键接口联调与必要问题修复

- [x] Task 6: 补齐文档、验收与收尾确认
  - [x] SubTask 6.1: 根据实际落地情况回写或补充 `architecture-upgrade-planV3.md` 中需要同步的实施说明
  - [x] SubTask 6.2: 按检查表逐项验证所有关键能力
  - [x] SubTask 6.3: 明确本轮未纳入实现的 Agent Framework 试点边界与后续前置条件

- [ ] Task 7: 补齐验收失败项并重新复验
  - [ ] SubTask 7.1: 移除前端仓库页对 `/api/tasks/wiki` 的回退兜底，彻底收敛为 `/wiki/refresh -> task_id -> /tasks/{id}/status` 单链路
  - [ ] SubTask 7.2: 修正刷新异常语义，避免把真实失败映射为 `no_change`
  - [ ] SubTask 7.3: 使用提供的 PostgreSQL、Ollama 与目标仓库完成真实端到端联调并留存可核验证据
  - [ ] SubTask 7.4: 修复联调过程中出现的剩余问题，并重新勾选 `Task 1`、`Task 5` 与检查表未通过项

# Task Dependencies

- Task 1 是整个改造的前置依赖，必须先统一入口与契约
- Task 2 依赖 Task 1，只有任务入口和版本读取收敛后，才能建立可靠的阶段状态与工件模型
- Task 3 依赖 Task 2，生成模型重构需要建立在可恢复、可持久化的任务框架之上
- Task 4 依赖 Task 1 与 Task 3，Ask、Slides、Workshop 需要消费统一版本模型与新生成工件
- Task 5 依赖 Task 1 至 Task 4，联调验证必须在主改造完成后执行
- Task 6 依赖 Task 5
- Task 7 依赖 Task 6 的验收结果，用于处理本轮复验未通过项
