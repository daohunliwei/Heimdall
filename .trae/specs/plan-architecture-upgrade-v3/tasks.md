# Tasks

- [x] Task 1: 完成 V3 现状盘点与问题归档
  - [x] SubTask 1.1: 对照 `architecture-upgrade-plan.md`、`architecture-upgrade-planV2.md`、`.trae/specs/upgrade-architecture-v2/*` 整理已完成项、未完成项与范围漂移
  - [x] SubTask 1.2: 结合当前代码确认前端契约、生成链路、任务执行、数据库落库的真实问题
  - [x] SubTask 1.3: 提炼 V3 需要优先解决的 P0/P1 问题，并明确哪些属于止血项、哪些属于结构性改造

- [x] Task 2: 明确 V3 的核心架构决策
  - [x] SubTask 2.1: 定义 V3 的目标边界，区分“阶段性目标”和“长期目标”
  - [x] SubTask 2.2: 给出内容生成与编排的新模型，包括结构工件、Markdown 页面草案、关系工件、全局收敛与渲染后处理
  - [x] SubTask 2.3: 给出前端重构原则，包括统一页面主键、版本切换模型、刷新交互、任务状态与后端契约
  - [x] SubTask 2.4: 给出数据库与任务可靠性原则，包括事务边界、幂等、工件持久化、失败恢复与审计

- [x] Task 3: 完成 Agent Loop 与 Microsoft Agent Framework 技术调研结论
  - [x] SubTask 3.1: 评估当前 Heimdall 是否已经具备引入 Agent 框架的前置条件
  - [x] SubTask 3.2: 比较 `Microsoft Agent Framework` 与现有自研编排在收益、成本、复杂度、可观测性和适配性上的差异
  - [x] SubTask 3.3: 输出明确建议，包括“现在引入、延后引入、局部试点”中的一种推荐路径及原因

- [x] Task 4: 编写 `architecture-upgrade-planV3.md` 的文档大纲与分阶段路线图
  - [x] SubTask 4.1: 定义分阶段实施顺序，确保先修稳定性，再做复杂编排增强
  - [x] SubTask 4.2: 将 V2 未完成项纳入 V3 路线图并重新排序优先级
  - [x] SubTask 4.3: 为每个阶段补充目标、改造范围、验收标准、风险与回滚思路

- [x] Task 5: 产出正式文档并完成一致性校验
  - [x] SubTask 5.1: 在 `doc/architecture/architecture-upgrade-planV3.md` 中完成正式文案
  - [x] SubTask 5.2: 检查文档是否与当前代码现实、V2 遗留问题、长期目标保持一致
  - [x] SubTask 5.3: 检查文档是否对“为什么现在不先上复杂 Agent 框架”或“为什么现在适合局部试点”给出清晰判断

# Task Dependencies

- Task 2 依赖 Task 1，必须先完成现状盘点再定义架构决策
- Task 3 可与 Task 2 并行推进，但最终结论需要并入 Task 4 的路线图
- Task 4 依赖 Task 1、Task 2、Task 3
- Task 5 依赖 Task 4
