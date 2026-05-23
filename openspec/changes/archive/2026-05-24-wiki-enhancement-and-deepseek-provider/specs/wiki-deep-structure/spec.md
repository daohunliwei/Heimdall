## MODIFIED Requirements

### Requirement: 多层嵌套 Wiki 目录结构
系统 SHALL 支持生成 3-5 层嵌套的 Wiki 目录结构。结构层次模型为：顶层章节（Level 1）→ 子章节（Level 2）→ 页面组（Level 3）→ 详细页面（Level 4）→ 附录/深入页面（Level 5）。每个节点 SHALL 具有 parentId 指向其父节点。

**变更**：结构规划输出 JSON 中的每个页面条目 SHALL 强制包含有效的 `parentId` 字段。根节点 `parentId` 为 `null`。解析器 SHALL 在后处理阶段验证所有 `parentId` 指向已存在的页面 ID，无效引用自动提升到根节点。

#### Scenario: 大型项目 4 层嵌套
- **WHEN** 仓库包含 15+ 模块，500+ 文件
- **THEN** 结构规划输出 4 层嵌套结构，如：系统架构（L1, parentId=null）→ 后端架构（L2, parentId=L1-id）→ 数据层详解（L3, parentId=L2-id）→ EF Core 配置（L4, parentId=L3-id）
- **AND** 每个页面条目包含 `parentId` 字段，值合法且指向已存在页面

#### Scenario: 小型项目 2 层嵌套
- **WHEN** 仓库包含 ≤3 模块，< 50 文件
- **THEN** 结构规划输出 2 层嵌套结构，如：概览（L1, parentId=null）→ 核心模块详解（L2, parentId=L1-id）

#### Scenario: parentId 无效引用自动修正
- **WHEN** LLM 输出 JSON 中某页面的 `parentId` 指向不存在的页面 ID
- **THEN** 解析器将该页面的 `parentId` 设为 `null`（提升为根节点），并记录 Warning 日志

#### Scenario: 页面缺失 parentId 字段
- **WHEN** LLM 输出 JSON 中某页面条目不包含 `parentId` 字段
- **THEN** 解析器根据页面 `depth` 字段推断父节点：depth=1 设为 null，depth>1 在同 sections 内查找 depth-1 的父页面

### Requirement: 拓扑序渐进式页面生成
系统 SHALL 按树形拓扑序生成 Wiki 页面：先生成顶层 overview 页面（Level 1-2），再生成 section 页面（Level 3），最后生成 article/appendix 页面（Level 4-5）。子页面生成时 SHALL 继承父页面的摘要作为上下文。

**变更**：页面生成顺序 SHALL 严格依赖 `parentId` 字段构建的树形结构。BFS 遍历从根节点（parentId=null）开始，逐层生成。同层页面可并行，但子页面必须等父页面完成后才能开始。

#### Scenario: 父页面先于子页面生成
- **WHEN** 页面 A（L2, parentId=null）是页面 B（L3, parentId=A.id）的父页面
- **THEN** 系统确保 A 在 B 之前生成完成，B 的生成 prompt 包含 A 的标题和前 500 字摘要

#### Scenario: 同层页面并行生成
- **WHEN** 页面 C 和 D 同为 Level 3，父页面均已完成
- **THEN** 系统可将 C 和 D 放入同一批次并行生成

#### Scenario: 上下文继承链
- **WHEN** 页面 E（L4, parentId=D.id）的父页面为 D（L3, parentId=B.id），D 的父页面为 B（L2, parentId=null）
- **THEN** E 的生成 prompt 包含 B 的标题+摘要（祖父级）和 D 的标题+摘要（父级），提供完整上下文链

## ADDED Requirements

### Requirement: 前端树形组件层级渲染
前端 Wiki 目录树组件 SHALL 根据页面 `parentId` 构建树形数据结构，递归渲染嵌套节点。根节点（parentId=null）渲染为顶层条目，子节点缩进展示。组件 SHALL 支持节点展开/折叠交互。

#### Scenario: 树形组件渲染多层结构
- **WHEN** 后端返回的页面列表包含 parentId 字段且存在 3 层嵌套关系
- **THEN** 前端构建 3 层嵌套树，根节点在顶层，子节点缩进 16px/层，使用展开/折叠箭头

#### Scenario: 旧数据兼容平铺渲染
- **WHEN** 后端返回的页面列表中所有页面 parentId 为 null（旧版本数据或扁平结构）
- **THEN** 前端按平铺列表渲染，不显示展开/折叠箭头，行为与原来一致

#### Scenario: 当前页面自动展开路径
- **WHEN** 用户浏览某 Wiki 页面且该页面在树的第 3 层
- **THEN** 树形组件自动展开该页面的所有祖先节点，并高亮当前页面条目
