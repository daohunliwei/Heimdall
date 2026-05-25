# 架构文档拆分方案（Task 1）

> 最后更新：2026-05-25
>
> 本文档用于完成 `split-architecture-docs` 的 `Task 1`：盘点现有架构文档并设计拆分边界。

---

## 1. 目标与拆分原则

当前 `docs/architecture/architecture.md` 同时承载系统总览、分层架构、领域模型、核心流程、前后端设计、API、数据库、配置、架构决策、路线图和附录，单文件体量过大，已经不适合作为长期维护的权威文档载体。

本次拆分方案遵循以下原则：

1. **按稳定主题拆分，不按字数拆分**：每篇文档只围绕一个长期稳定的架构主题展开。
2. **入口页只保留系统级事实**：`architecture.md` 继续作为统一入口，但不再承载各专题的完整细节正文。
3. **一类事实只落一个主文档**：避免“同一张架构图、同一份约束说明”在多个文件中重复维护。
4. **支持独立阅读**：读者只看某个专题文档，也能理解该主题的职责、流程、边界与关联模块。
5. **为后续扩写留出空间**：后续新增流程图、设计取舍、边界条件时，优先扩写对应专题文档，而不是回填入口页。

---

## 2. 当前文档主题盘点

现有 `architecture.md` 的一级章节已经天然形成 12 个主题簇，可以直接作为拆分依据：

| 当前章节 | 主题判断 | 建议去向 |
|------|------|------|
| 1. 项目概述 | 系统级摘要 | 保留在入口页 |
| 2. 架构演进历程 | 演进历史与版本脉络 | 下沉到“演进路线图”专题 |
| 3. 系统架构全景 | 系统总览与跨模块关系 | 保留摘要到入口页，完整内容下沉到“系统全景”专题 |
| 4. 分层架构设计 | 后端四层分离与依赖规则 | 下沉到“分层架构”专题 |
| 5. 核心领域模型 | 版本模型、任务工件、索引模型 | 下沉到“领域模型”专题 |
| 6. Wiki 生成管线（8 阶段） | 核心业务流水线 | 下沉到“Wiki 生成管线”专题 |
| 7. AI Provider 架构 | Provider 抽象、模型分层、成本追踪 | 下沉到“AI Provider 架构”专题 |
| 8. 前端架构 | 路由、组件、BFF、状态、Hook | 下沉到“前端架构”专题 |
| 9. API 端点总览 | 对外接口分组 | 下沉到“API 总览”专题 |
| 10. 数据库设计 | 表结构、约束、索引策略 | 下沉到“数据库设计”专题 |
| 11. 配置与环境变量 | 配置优先级、环境变量、配置文件 | 下沉到“配置与环境变量”专题 |
| 12. 关键架构决策（AD） | 架构决策记录 | 下沉到“架构决策”专题 |
| 13. 演进路线图 | 已完成里程碑与未来规划 | 与第 2 章合并到“演进路线图”专题 |
| 14. 附录 | 依赖、调试、归档说明 | 下沉到“附录与归档”专题 |

---

## 3. 入口页保留边界

重构后的 `docs/architecture/architecture.md` 应只承担“总览入口页”职责，建议保留以下内容：

### 3.1 必须保留的内容

1. **项目一句话定义**：Heimdall 是什么、解决什么问题、产出什么结果。
2. **当前技术栈摘要表**：后端、前端、数据库、ORM、AI 抽象、代码分析、认证方式。
3. **系统全景摘要图**：保留一张最高层级架构图，用于建立全局认知。
4. **核心架构原则**：例如四层依赖方向、版本化底座、数据库唯一信源、后台任务统一入队。
5. **专题目录与职责说明**：每篇子文档解决什么问题，适合什么读者。
6. **推荐阅读顺序**：新人上手、后端开发、前端开发、平台治理等不同阅读路径。
7. **跨模块关系说明**：哪些主题存在强依赖，例如“Wiki 生成管线”依赖“领域模型”“AI Provider”“数据库设计”。
8. **迁移说明**：说明原单文件内容已拆分到多个专题文档，旧章节的事实应以新专题文档为准。

### 3.2 不再保留的内容

1. **完整的章节级详细正文**：例如前端组件职责矩阵、完整 API 路由表、数据库表总览等。
2. **长表格与长清单**：如全部环境变量、所有 API 端点、完整组件矩阵、所有依赖包列表。
3. **专题内部流程细节**：例如 Tree-sitter 索引流程、三层代码理解流程、Provider 工厂细节。
4. **专题内部设计权衡**：例如 Token 估算模式、BFF 代理细节、任务恢复字段说明。

入口页应控制在“读者 5 分钟内建立全局认知”的体量，而不是继续承担查阅全部细节的职责。

---

## 4. 拆分后的目录结构

建议采用“入口页 + 主题分组目录”的结构，而不是所有文件平铺在 `docs/architecture/` 根目录。这样可以在后续继续扩写时保持可维护性。

```text
docs/architecture/
├── architecture.md                     ← 统一入口页（总览 + 导航 + 阅读顺序）
├── split-plan.md                       ← 本拆分方案（Task 1 产物）
├── overview/
│   ├── system-overview.md              ← 系统全景、边界、核心能力
│   ├── layered-architecture.md         ← 四层分离、依赖规则、目录职责、生命周期
│   └── domain-model.md                 ← 版本模型、实体关系、任务工件、索引模型
├── runtime/
│   ├── wiki-pipeline.md                ← 8 阶段 Wiki 管线、结构规划、检索、代码理解、Agent 编排
│   ├── ai-provider-architecture.md     ← MEAI、Provider 工厂、Tier 策略、成本追踪
│   ├── frontend-architecture.md        ← 路由、组件、BFF、状态、Hook、Context、部署
│   └── api-overview.md                 ← API 分组、主路径、职责边界、调用关系
├── persistence/
│   ├── database-design.md              ← 表结构、关键约束、索引与持久化策略
│   └── configuration-and-env.md        ← 配置优先级、环境变量、配置文件
└── governance/
    ├── architecture-decisions.md       ← AD1~AD9 及后续 ADR/AD
    ├── evolution-roadmap.md            ← 演进历史、里程碑、未来方向
    └── appendix-and-archive.md         ← 技术依赖、调试工作流、历史文档归档说明
```

### 4.1 分组理由

1. `overview/`：承载最稳定、跨团队共享度最高的系统骨架事实。
2. `runtime/`：承载运行时行为最强、变化频率相对更高的流程与交互主题。
3. `persistence/`：承载持久化与配置治理，方便后端和运维按职责检索。
4. `governance/`：承载决策、演进和归档信息，避免与运行时设计正文混在一起。

### 4.2 命名规范

1. 文件名统一使用英文 kebab-case，避免空格与中文路径。
2. 文档标题使用中文，便于仓库内统一阅读体验。
3. 每篇专题文档都保留“返回入口页”链接和“关联阅读”链接。
4. 每篇专题文档统一包含以下段落：
   - 文档范围
   - 核心职责
   - 关键结构/流程
   - 依赖关系
   - 设计取舍
   - 关联阅读

---

## 5. 专题文档清单与职责边界

### 5.1 `overview/system-overview.md`

- 承载内容：项目定位、核心能力矩阵、系统全景图、主要模块关系、关键运行路径摘要。
- 不承载内容：某一模块的实现细节、接口清单、数据库表清单。
- 主要来源章节：第 1 章、第 3 章。

### 5.2 `overview/layered-architecture.md`

- 承载内容：四层架构图、依赖方向、目录职责、服务生命周期、DI 约束。
- 不承载内容：实体字段细节、任务阶段流程。
- 主要来源章节：第 4 章。

### 5.3 `overview/domain-model.md`

- 承载内容：`RepositoryVersion` / `WikiVersion` 双版本底座、实体关系、任务工件模型、代码索引模型。
- 不承载内容：具体 API 路由、前端组件职责。
- 主要来源章节：第 5 章。

### 5.4 `runtime/wiki-pipeline.md`

- 承载内容：8 阶段 Wiki 管线、结构规划三策略、Hybrid Retrieval、代码索引、代码理解、大仓库 Agent 编排。
- 不承载内容：Provider 工厂细节、数据库全表设计。
- 主要来源章节：第 6 章。

### 5.5 `runtime/ai-provider-architecture.md`

- 承载内容：MEAI `IChatClient` 抽象、Provider 分类、Keyed DI、模型分层策略、Token 追踪与计费。
- 不承载内容：Wiki 管线业务阶段说明、前端展示逻辑。
- 主要来源章节：第 7 章。

### 5.6 `runtime/frontend-architecture.md`

- 承载内容：路由设计、组件架构、BFF 代理、版本透传、数据流、Hooks、Context、部署决策。
- 不承载内容：后端领域模型、数据库约束。
- 主要来源章节：第 8 章。

### 5.7 `runtime/api-overview.md`

- 承载内容：仓库/Wiki/任务/Admin/其他接口分组、主业务链路、典型调用顺序、接口边界。
- 不承载内容：控制器内部实现、DTO 字段逐项展开。
- 主要来源章节：第 9 章。

### 5.8 `persistence/database-design.md`

- 承载内容：核心表、关系、约束、索引、CodeFirst 同步策略、恢复方案。
- 不承载内容：环境变量、前端类型定义。
- 主要来源章节：第 10 章。

### 5.9 `persistence/configuration-and-env.md`

- 承载内容：配置优先级、环境变量、配置文件入口、运行时覆盖策略。
- 不承载内容：数据库约束、API 端点矩阵。
- 主要来源章节：第 11 章。

### 5.10 `governance/architecture-decisions.md`

- 承载内容：AD1~AD9，后续新增 AD/ADR 也统一沉淀在此。
- 不承载内容：路线图里程碑、依赖包清单。
- 主要来源章节：第 12 章。

### 5.11 `governance/evolution-roadmap.md`

- 承载内容：版本演进历程、架构升级时间线、里程碑、未来方向。
- 不承载内容：当前系统运行时细节。
- 主要来源章节：第 2 章、第 13 章。

### 5.12 `governance/appendix-and-archive.md`

- 承载内容：技术依赖、调试工作流、历史文档归档与迁移说明。
- 不承载内容：核心架构定义正文。
- 主要来源章节：第 14 章。

---

## 6. 当前章节到新文档的映射关系

| 现有章节 | 新文档 | 入口页是否保留摘要 |
|------|------|------|
| 1. 项目概述 | `architecture.md` + `overview/system-overview.md` | 是 |
| 2. 架构演进历程 | `governance/evolution-roadmap.md` | 是 |
| 3. 系统架构全景 | `overview/system-overview.md` | 是 |
| 4. 分层架构设计 | `overview/layered-architecture.md` | 否 |
| 5. 核心领域模型 | `overview/domain-model.md` | 是 |
| 6. Wiki 生成管线 | `runtime/wiki-pipeline.md` | 是 |
| 7. AI Provider 架构 | `runtime/ai-provider-architecture.md` | 否 |
| 8. 前端架构 | `runtime/frontend-architecture.md` | 否 |
| 9. API 端点总览 | `runtime/api-overview.md` | 否 |
| 10. 数据库设计 | `persistence/database-design.md` | 否 |
| 11. 配置与环境变量 | `persistence/configuration-and-env.md` | 否 |
| 12. 关键架构决策 | `governance/architecture-decisions.md` | 是 |
| 13. 演进路线图 | `governance/evolution-roadmap.md` | 是 |
| 14. 附录 | `governance/appendix-and-archive.md` | 否 |

说明：

1. “入口页保留摘要”表示在入口页只保留主题简介、关键结论和跳转链接。
2. 领域模型、Wiki 管线、架构决策、演进路线图建议在入口页保留短摘要，因为这些主题最能帮助读者建立整体认知。

---

## 7. 推荐阅读顺序

### 7.1 新人或架构评审

1. `architecture.md`
2. `overview/system-overview.md`
3. `overview/layered-architecture.md`
4. `overview/domain-model.md`
5. `runtime/wiki-pipeline.md`
6. `governance/architecture-decisions.md`

### 7.2 后端开发

1. `architecture.md`
2. `overview/layered-architecture.md`
3. `overview/domain-model.md`
4. `runtime/wiki-pipeline.md`
5. `runtime/ai-provider-architecture.md`
6. `persistence/database-design.md`
7. `persistence/configuration-and-env.md`
8. `runtime/api-overview.md`

### 7.3 前端开发

1. `architecture.md`
2. `overview/system-overview.md`
3. `runtime/frontend-architecture.md`
4. `runtime/api-overview.md`
5. `overview/domain-model.md`

### 7.4 平台治理与演进规划

1. `architecture.md`
2. `governance/architecture-decisions.md`
3. `governance/evolution-roadmap.md`
4. `governance/appendix-and-archive.md`

---

## 8. Task 1 验收结论

`Task 1` 的三个子目标已经形成明确结论：

1. **可稳定独立的主题模块**：已识别出 12 个专题文档主题，并按 `overview / runtime / persistence / governance` 进行分组。
2. **入口页与专题页边界**：已明确入口页仅保留系统级摘要、专题目录、阅读顺序、跨模块关系与迁移说明。
3. **目录结构与阅读顺序**：已给出建议目录树、命名规范、专题职责清单和多角色阅读路径。

后续 `Task 2` 可以直接基于本方案将 `architecture.md` 收敛为入口页，`Task 3` 则按本方案创建专题文档并迁移正文。
