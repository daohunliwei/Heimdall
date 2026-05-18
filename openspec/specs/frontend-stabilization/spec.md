## ADDED Requirements

### Requirement: API 类型层与统一客户端
系统前端 SHALL 维护与后端 API 响应一致的 TypeScript 类型定义层（`src/types/api.ts`），并通过统一的 `apiClient` 封装所有 HTTP 请求，包含错误处理、超时、重试逻辑。

#### Scenario: API 响应类型不匹配时优雅降级
- **WHEN** 后端返回的 JSON 字段与前端类型定义不一致
- **THEN** 系统 SHALL 在控制台记录警告，并对缺失字段使用安全默认值，不导致页面崩溃

#### Scenario: 网络请求失败时统一错误展示
- **WHEN** 任意 API 请求返回 4xx/5xx 或网络超时
- **THEN** 对应页面区域 SHALL 展示统一的错误提示组件，包含错误信息与重试按钮

### Requirement: 仓库详情页布局重构
仓库详情页 SHALL 拆分为独立子组件（WikiBrowser、SideNav、PageContent、ActionBar），每个子组件独立管理自身加载与错误状态。

#### Scenario: Wiki 页面树加载
- **WHEN** 用户进入仓库详情页且存在已发布 WikiVersion
- **THEN** 左侧导航 SHALL 展示页面树，支持多层嵌套展开/折叠，当前页面高亮

#### Scenario: 无 Wiki 数据时的空态引导
- **WHEN** 仓库无任何 WikiVersion
- **THEN** 页面 SHALL 展示空态引导，包含"生成 Wiki"按钮与简要说明

### Requirement: 统一加载态与错误态组件
系统 SHALL 提供 `<LoadingState />`、`<ErrorState />`、`<EmptyState />` 三个通用组件，所有页面区域 MUST 使用这些组件而非自定义 loading/error 展示。

#### Scenario: 长时间任务进行中
- **WHEN** Wiki 生成任务正在执行
- **THEN** 页面 SHALL 展示带进度条的 LoadingState，实时更新进度百分比与阶段描述

### Requirement: Ask/Slides/Workshop 版本上下文一致性
Ask、Slides、Workshop 页面 SHALL 自动继承仓库详情页当前选中的 `wikiVersionId`，用户无需手动选择版本。

#### Scenario: 切换版本后使用 Ask
- **WHEN** 用户在仓库页面切换到历史 WikiVersion 后打开 Ask
- **THEN** Ask SHALL 基于该历史版本的知识库进行问答，而非最新版本

### Requirement: 响应式布局与主题适配
所有页面 SHALL 适配 desktop（≥1024px）与 tablet（768-1023px）两种视口，深色/浅色主题切换 SHALL 无样式异常。

#### Scenario: 窄屏侧边栏折叠
- **WHEN** 视口宽度 < 1024px
- **THEN** Wiki 侧边导航 SHALL 自动折叠为抽屉模式，通过汉堡按钮触发展开
