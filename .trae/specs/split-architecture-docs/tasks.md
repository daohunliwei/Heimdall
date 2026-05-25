# Tasks

- [x] Task 1: 盘点当前架构文档并设计拆分边界
  - [x] SubTask 1.1: 梳理 `docs/architecture/architecture.md` 的一级与二级章节，识别可稳定独立的主题模块
  - [x] SubTask 1.2: 明确哪些内容保留在入口文档，哪些内容下沉到专题文档
  - [x] SubTask 1.3: 设计拆分后的目录结构、文档命名规范与推荐阅读顺序

- [x] Task 2: 重构入口文档为总览与导航页
  - [x] SubTask 2.1: 将 `docs/architecture/architecture.md` 调整为架构总览、专题索引与阅读指南
  - [x] SubTask 2.2: 保留系统级摘要、跨模块关系、文档职责分配与迁移说明
  - [x] SubTask 2.3: 确保入口文档不再重复承载各专题的完整细节正文

- [x] Task 3: 按专题拆分现有架构内容
  - [x] SubTask 3.1: 创建系统全景、分层架构、领域模型与 Wiki 生成管线等核心专题文档
  - [x] SubTask 3.2: 创建 AI Provider、前端架构、API、数据库、配置与环境变量等专题文档
  - [x] SubTask 3.3: 创建架构决策、演进路线图、附录或归档说明等支撑文档
  - [x] SubTask 3.4: 在拆分过程中补齐每篇专题文档的模块职责、关键流程、依赖关系与设计取舍说明

- [x] Task 4: 统一导航、链接与文档风格
  - [x] SubTask 4.1: 为所有专题文档补充返回入口链接、相邻主题导航或关联阅读链接
  - [x] SubTask 4.2: 统一标题层级、术语命名、表格风格、Mermaid 使用方式和文档模板
  - [x] SubTask 4.3: 清理重复段落与冲突表述，保证架构事实只有一个主要落点

- [x] Task 5: 完成验证与可用性检查
  - [x] SubTask 5.1: 验证入口页能覆盖所有专题文档且无死链
  - [x] SubTask 5.2: 验证每篇专题文档可以脱离原单文件上下文被独立阅读
  - [x] SubTask 5.3: 验证拆分后单文件体量明显下降，且支持后续继续扩写

- [x] Task 6: 修复治理类文档的模板一致性问题
  - [x] SubTask 6.1: 为 `architecture-decisions.md` 补齐核心职责与关键流程段落
  - [x] SubTask 6.2: 为 `evolution-roadmap.md` 补齐核心职责段落，并与统一模板对齐
  - [x] SubTask 6.3: 为 `appendix-and-archive.md` 补齐关键流程或等价结构表达，满足统一专题模板要求

# Task Dependencies

- Task 2 依赖 Task 1，必须先完成边界设计再重构入口文档
- Task 3 依赖 Task 1，可在 Task 2 进行时同步准备专题文档草案
- Task 4 依赖 Task 2 与 Task 3，导航体系必须建立在实际拆分结果之上
- Task 5 依赖 Task 2 至 Task 4，需在全部文档落地后统一验收
- Task 6 依赖 Task 5 的复验结果，用于修复验收阶段发现的模板缺口
