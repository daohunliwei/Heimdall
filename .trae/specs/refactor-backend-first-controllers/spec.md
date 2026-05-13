# 后端主导重构与 Controller 拆分 Spec

## Why
当前项目的生成提示词、仓库处理编排、页面生成细节同时分散在 Next.js 页面、前端组件、前端代理路由与后端服务中，导致职责边界模糊，前后端演进成本高。与此同时，`backend/DeepWiki.Api/Program.cs` 承担了路由注册、请求处理、文件读写、鉴权判断与部分业务流程，不符合 ASP.NET Core 面向 Controller 与分层组织的最佳实践，需要通过一次结构性重构收敛到“后端主导、前端呈现”的架构。

## What Changes
- 将 Wiki 结构生成、Wiki 页面生成、演示文稿生成、训练营生成、问答系统提示词与关键编排逻辑统一迁移到 C# 后端
- 收敛前端职责为结果呈现、用户输入采集、配置表单与状态展示，不再在前端拼装核心生成提示词或承载仓库处理细节
- 将 `backend/DeepWiki.Api/Program.cs` 中的业务型 Minimal API 路由拆分为按领域分类的 Controller
- 为缓存、项目列表、导出、配置、聊天、仓库读取等能力建立清晰的 DTO、应用服务与基础设施边界
- 将当前集中在单文件中的 API 模型按业务领域拆分为多个参数类与返回类
- 为 Controller 方法、服务公共方法、请求参数类、响应参数类及其字段补齐完整中文注释
- 统一异常处理、鉴权入口与请求验证方式，避免继续在 Controller 或入口文件中散落实现细节
- **BREAKING**：现有前端直接控制生成细节的调用方式与请求载荷将调整为以后端契约为准
- **BREAKING**：后端接口的组织方式将从 `Program.cs` 中的 Minimal API 调整为 Controller 路由

## Impact
- Affected specs: 后端接口架构、前后端职责边界、生成流程编排、配置管理、缓存管理、导出流程、注释规范
- Affected code: `backend/DeepWiki.Api/Program.cs`、`backend/DeepWiki.Api/Models/ApiModels.cs`、`backend/DeepWiki.Api/Services/**`、`backend/DeepWiki.Api/config/*.json`、`src/app/[owner]/[repo]/page.tsx`、`src/app/[owner]/[repo]/slides/page.tsx`、`src/app/[owner]/[repo]/workshop/page.tsx`、`src/components/Ask.tsx`、`src/components/ConfigurationModal.tsx`、`src/components/ModelSelectionModal.tsx`、`src/components/UserSelector.tsx`、`src/app/api/**/route.ts`

## ADDED Requirements
### Requirement: 后端主导生成编排
系统 SHALL 由 C# 后端统一负责 Wiki、问答、演示文稿、训练营等内容生成过程中的提示词管理、上下文组装、模型选择解析、仓库读取策略与输出格式约束。

#### Scenario: 前端请求 Wiki 生成
- **WHEN** 用户在前端发起 Wiki 生成或刷新操作
- **THEN** 前端仅提交仓库标识、用户配置与必要的生成意图参数
- **THEN** 后端负责拼装系统提示词、补充仓库上下文、决定调用顺序并返回生成结果
- **THEN** 前端不再内嵌核心 Wiki 结构生成提示词与页面生成提示词

### Requirement: 前端仅承担展示与配置职责
系统 SHALL 将 Next.js 前端限制为结果展示、表单配置、进度反馈、错误提示与交互控制层，不再在页面组件中保留核心生成规则、仓库解析流程细节或模型输出结构约束。

#### Scenario: 用户配置生成参数
- **WHEN** 用户在前端选择语言、模型、过滤规则或生成模式
- **THEN** 前端仅负责收集配置并调用后端接口
- **THEN** 前端不负责决定提示词模板、章节结构约束、训练营大纲规则或幻灯片生成规范
- **THEN** 同类生成能力的规则调整主要通过后端配置或后端服务完成

### Requirement: Controller 分层承载接口能力
系统 SHALL 使用 ASP.NET Core Controller 作为主要 API 入口，并按业务领域拆分至少包含聊天、配置、仓库、缓存、项目列表与导出等类别的 Controller。

#### Scenario: 维护者查看后端接口入口
- **WHEN** 维护者检查后端 Web API 项目结构
- **THEN** 可在 `Controllers` 目录下按职责找到对应 Controller
- **THEN** `Program.cs` 仅保留启动配置、中间件注册、依赖注入与 Controller 映射等入口职责
- **THEN** 不再在 `Program.cs` 中直接编写成段的业务路由处理逻辑

### Requirement: 应用服务与基础设施职责清晰
系统 SHALL 将文件系统读写、缓存存取、仓库访问、导出处理、聊天编排、配置读取等逻辑从入口层迁移到职责清晰的服务中，并通过明确接口组织依赖关系。

#### Scenario: 删除 Wiki 缓存
- **WHEN** 用户调用删除缓存接口
- **THEN** Controller 只负责接收请求、调用应用服务并返回结果
- **THEN** 鉴权校验、路径解析、文件删除与错误处理在专用服务或统一机制中完成
- **THEN** 该流程不再直接写在 `Program.cs` 或前端代理逻辑中

### Requirement: DTO 与参数模型按领域拆分
系统 SHALL 将当前集中式 API 模型拆分为按业务领域组织的请求类、响应类与配置类，避免单一模型文件承载所有接口契约。

#### Scenario: 开发者新增聊天接口字段
- **WHEN** 开发者需要修改聊天请求或响应字段
- **THEN** 可以在聊天领域对应的模型文件中定位参数类
- **THEN** 不需要在超大模型聚合文件中查找无关结构
- **THEN** 新增字段的注释、校验与序列化约束与该领域一起维护

### Requirement: 中文注释完整覆盖公共接口与参数字段
系统 SHALL 为所有 Controller 方法、公开服务方法、请求参数类、响应参数类以及这些参数类中的字段提供完整中文注释。

#### Scenario: 维护者查看接口参数定义
- **WHEN** 维护者阅读请求 DTO、响应 DTO 或配置 DTO
- **THEN** 每个类具有中文用途说明
- **THEN** 每个公开字段或属性具有中文注释，说明字段含义、取值语义与关键约束
- **THEN** Controller 方法的用途、参数与返回结果具有完整中文注释

### Requirement: 生成规则后端可配置化
系统 SHALL 支持将提示词模板、生成规则、输出格式要求与默认参数优先放在后端服务或后端配置中管理，以便集中调整与复用。

#### Scenario: 调整幻灯片生成规则
- **WHEN** 维护者需要修改幻灯片布局规则或训练营生成模板
- **THEN** 可以在后端提示词模板服务或后端配置中完成调整
- **THEN** 不需要进入前端页面组件修改大段生成提示词
- **THEN** 前端接口与 UI 可以在不理解内部生成细节的情况下继续工作

## MODIFIED Requirements
### Requirement: 前后端职责边界
系统 SHALL 以“后端负责生成与编排、前端负责展示与配置”为默认职责边界。前端仍可保留必要的请求状态管理、用户输入校验与界面交互逻辑，但不得继续承载核心生成提示词、仓库解析策略、页面结构生成规则或模型输出约束的主实现。

### Requirement: 后端入口文件职责
系统 SHALL 将 `backend/DeepWiki.Api/Program.cs` 限制为应用启动入口文件，仅包含服务注册、配置装载、中间件管线、鉴权注册、异常处理注册与 Controller 映射。与单个业务用例直接相关的请求处理、文件操作、缓存操作、导出操作与响应拼装不得继续留在该文件中。

### Requirement: 接口文档可维护性
系统 SHALL 使后端接口的路径、参数模型、返回模型与控制器职责具备可读性和可维护性，使开发者能够基于 Controller、DTO 与服务命名快速理解接口用途，而不依赖阅读前端页面逻辑反推后端行为。

## REMOVED Requirements
### Requirement: 前端页面内嵌核心生成提示词
**Reason**: 前端内嵌大量提示词和生成约束会导致职责混乱、复用困难，并放大页面组件复杂度  
**Migration**: 将提示词模板与生成规则迁移到后端模板服务、配置文件或应用服务中，前端只保留配置项与结果展示

### Requirement: Program.cs 直接承载业务路由实现
**Reason**: 入口文件过重会造成接口增长后的维护失控，不利于测试、注释管理与分层扩展  
**Migration**: 以 Controller + 应用服务 + 领域模型或基础设施服务的结构替换现有业务型 Minimal API 实现
