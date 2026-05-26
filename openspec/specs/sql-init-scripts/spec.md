## ADDED Requirements

### Requirement: SQL 初始化脚本目录
系统 SHALL 在仓库根目录提供 `/SqlScripts` 目录，存放完整的 PostgreSQL 数据库初始化 SQL 脚本，命名规范为 `Init_xxx.sql`。

#### Scenario: 脚本目录结构
- **WHEN** 查看 `/SqlScripts` 目录
- **THEN** 目录包含 `Init_Tables.sql`（建表）、`Init_Indexes.sql`（索引）、`Init_Extensions.sql`（扩展）、`Init_SeedData.sql`（种子数据）等脚本文件

#### Scenario: 脚本可独立执行
- **WHEN** 在 PostgreSQL 客户端（psql、pgAdmin）中对空数据库执行 `Init_Tables.sql`
- **THEN** 所有业务表创建成功，表结构与 SqlSugar Code First 生成的完全一致

### Requirement: 建表脚本覆盖所有实体表
`Init_Tables.sql` SHALL 包含所有实体对应的 CREATE TABLE 语句，并与当前 PostgreSQL 表结构保持一致。

#### Scenario: 所有业务表创建
- **WHEN** 执行 `Init_Tables.sql`
- **THEN** `users`、`repositories`、`tasks`、`task_artifacts`、`task_llm_call_logs`、`wiki_pages`、`repository_versions`、`wiki_spaces`、`wiki_versions`、`wiki_page_relations`、`prompt_templates`、`repository_prompt_overrides`、`prompt_template_histories`、`system_settings`、`code_index_entries`、`code_index_chunks`、`llm_call_metrics`、`provider_model_metadata` 等表全部创建

### Requirement: 索引脚本覆盖性能关键索引
`Init_Indexes.sql` SHALL 包含所有性能关键的数据库索引定义。

#### Scenario: 索引创建
- **WHEN** 执行 `Init_Indexes.sql`
- **THEN** 所有外键列、常用查询列（如 `task_id`、`repository_id`、`status`、`created_at`）的索引创建成功

### Requirement: 扩展脚本声明必需扩展
`Init_Extensions.sql` SHALL 包含 CREATE EXTENSION 语句，声明当前实际使用的 PostgreSQL 扩展。

#### Scenario: 扩展脚本与当前实现一致
- **WHEN** 执行 `Init_Extensions.sql`
- **THEN** 脚本仅声明当前运行链路真实依赖的扩展

### Requirement: 种子数据脚本
`Init_SeedData.sql` SHALL 包含系统运行必需的基础数据（如默认 Prompt 模板、默认系统设置）。

#### Scenario: 种子数据插入
- **WHEN** 执行 `Init_SeedData.sql`
- **THEN** 默认 PromptTemplate 记录和 SystemSettings 记录插入成功

### Requirement: 脚本同步维护约定
系统 SHALL 在任何数据模型变更时同步更新 SQL 脚本，确保脚本与 Code First 生成的表结构一致。

#### Scenario: 新增实体时更新脚本
- **WHEN** 开发者新增实体类并添加 Code First 支持
- **THEN** 开发者同时更新 `Init_Tables.sql` 添加对应的 CREATE TABLE 语句
- **AND** 开发者同时更新 `Init_Indexes.sql` 添加必要的索引
