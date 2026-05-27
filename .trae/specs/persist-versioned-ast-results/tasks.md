# Tasks

- [ ] Task 1: 设计 AST 版本化数据模型与事务边界
  - [ ] SubTask 1.1: 设计 AST 主版本实体，绑定 `RepositoryVersion`，补齐分支、提交、解析配置、状态、统计与时间字段
  - [ ] SubTask 1.2: 设计 AST 明细实体或等价持久化模型，覆盖语法树投影、符号、调用边、依赖边、声明级分块与设计模式提示
  - [ ] SubTask 1.3: 明确唯一索引、复用条件、失败状态与事务边界，保证同一快照重复执行时结果可复用且不产生脏数据

- [ ] Task 2: 打通 AST 解析结果落库链路
  - [ ] SubTask 2.1: 在 AST 解析阶段产出统一的持久化 DTO，避免只保留内存态分析结果
  - [ ] SubTask 2.2: 在仓储层落地 AST 主版本与明细数据的批量写入能力
  - [ ] SubTask 2.3: 在任务链路中记录 AST 持久化状态、统计结果与恢复锚点

- [ ] Task 3: 支持按分支和 commit 的多版本共存与复用
  - [ ] SubTask 3.1: 建立按 `RepositoryVersion`、`branch`、`commitSha` 与解析配置查询 AST 版本的读取路径
  - [ ] SubTask 3.2: 实现同一 `RepositoryVersion` + 相同解析配置的复用逻辑，避免重复明细写入
  - [ ] SubTask 3.3: 验证同仓库不同分支、同分支不同提交、同提交不同分支场景下的隔离与共存规则

- [ ] Task 4: 让 Wiki 版本绑定其依赖的 AST 版本
  - [ ] SubTask 4.1: 扩展 `WikiVersion` 或等价关联模型，记录 AST 版本标识
  - [ ] SubTask 4.2: 调整 Wiki 主链路，确保先解析或复用 AST 版本，再持久化 `WikiVersion`
  - [ ] SubTask 4.3: 在任务结果摘要、工件摘要和版本化知识读取路径中暴露 AST 版本元信息

- [ ] Task 5: 完成验证与回归保护
  - [ ] SubTask 5.1: 增加实体、仓储与服务层验证，覆盖 AST 明细完整落库与版本复用规则
  - [ ] SubTask 5.2: 增加 Wiki 与 AST 版本关联验证，确保不存在指向失败 AST 版本的 Wiki 成功态
  - [ ] SubTask 5.3: 运行后端构建与相关测试，确认新增版本化持久化链路不破坏现有 Wiki 生成

# Task Dependencies

- Task 2 依赖 Task 1，只有先明确数据模型与事务边界，才能安全打通 AST 落库链路
- Task 3 依赖 Task 1 和 Task 2，多版本共存与复用需要建立在稳定的 AST 主记录和明细记录之上
- Task 4 依赖 Task 2 和 Task 3，Wiki 只能绑定已经可复用、可追溯的 AST 版本
- Task 5 依赖 Task 1 至 Task 4，验证需要覆盖完整的建模、落库、复用和 Wiki 关联链路
