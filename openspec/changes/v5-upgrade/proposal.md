## Why

Heimdall V4 在生产使用中暴露出四个严重问题：Provider 层硬编码的 system prompt 与任务级 prompt 指令冲突导致 LLM 输出格式混乱（间歇性 JSON 解析失败）；前端多处 UI 错乱与交互缺陷；Slides 和 Workshop 功能因缺失模型参数完全不可用；控制台日志被 SQL 语句淹没，无法有效诊断问题。V5 需系统性修复这些核心痛点，将项目从"可用"提升为"可靠"。

## What Changes

- **BREAKING**: 移除所有 Provider 中硬编码的 system prompt，将提示词管理权收归至数据层的 PromptTemplate 系统
- 新增基于 EF Core 的 PromptTemplate 数据库实体与 CRUD 管理接口，支持按任务类型、Provider、输出格式等维度的提示词片段存储与动态拼接
- 修复首页主题切换按钮错位、Wiki 版本默认加载逻辑、仓库快照选择器、刷新/生成弹窗引导文案等前端 UI 问题
- Wiki 页面版本选择器新增前端记忆能力（localStorage），默认按时间倒序加载最新版本
- Slides 与 Workshop 功能接入模型选择机制，界面显式展示当前使用的模型名称
- 新增日志分类与过滤系统：将 SQL 日志拆分为独立日志类别，支持运行时开关控制；增加任务执行的结构化进度日志

## Capabilities

### New Capabilities
- `prompt-management`: 提示词统一管理系统 — 数据库存储 PromptTemplate 实体，支持按任务类型/Provider/输出格式的组合查询与片段合并，替换所有 Provider 中的硬编码 system prompt
- `frontend-ui-fixes`: 前端界面修复与改进 — 修复主题切换错位、Wiki 版本选择记忆、快照选择器、刷新/生成弹窗引导文案等交互问题
- `model-selection`: 模型选择与显示 — Slides/Workshop 功能接入模型选择器，在操作界面显式展示当前模型名称并提供清晰的选项说明
- `logging-enhancements`: 日志分类与进度追踪 — SQL 日志独立分类可开关过滤，任务执行增加结构化进度日志（当前步骤、参数信息、页码进度等）

### Modified Capabilities
<!-- 本次为全新蓝图，无现有 spec 变更 -->

## Impact

- **后端 Provider 层**: OllamaChatProvider、OpenAIChatProvider、GoogleChatProvider 等需移除硬编码 system prompt，改为从 IPromptTemplateService 获取拼装后的提示词
- **后端 Core 层**: 新增 PromptTemplate 实体、IPromptTemplateRepository、IPromptTemplateService、PromptMergeEngine；日志相关新增 IStructuredLogger 接口与 LogCategoryFilter
- **后端 API 层**: 新增 PromptTemplateController 管理接口、更新 Slides/Workshop 控制器以接收 model 参数
- **后端 Repository 层**: 新增 PromptTemplate 配置与迁移、新增结构化日志记录方法
- **前端**: 修改首页布局、Wiki 版本选择器、快照选择器、RefreshPanel、Slides/Workshop 页面组件、新增模型选择器组件、修改日志组件
- **数据库**: 新增 PromptTemplates 表（EF Core 迁移）
