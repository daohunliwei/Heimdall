## ADDED Requirements

### Requirement: 标准化调试启动脚本
系统 SHALL 在 `scripts/` 目录提供一套 PowerShell 调试脚本，覆盖开发全流程：环境准备、密钥注入、服务启动/停止、数据重置。

#### Scenario: 一键启动开发环境
- **WHEN** 开发者执行 `scripts/dev-start.ps1`
- **THEN** 脚本按顺序：(1) 检查 PostgreSQL Docker 容器状态，若未启动则启动；(2) 从 `.env.example` 生成 `.env`（若不存在）；(3) 提示用户填写 Provider 密钥；(4) 执行数据库迁移；(5) 并行启动后端和前端服务

#### Scenario: 环境变量安全注入
- **WHEN** 开发者执行 `scripts/setup-env.ps1`
- **THEN** 脚本读取 `.env.example` 模板，生成 `.env` 文件（已加入 `.gitignore`），交互式引导用户填入各 Provider 的 API Key，密钥输入时屏幕回显掩码

#### Scenario: 数据重置
- **WHEN** 开发者执行 `scripts/dev-reset.ps1`
- **THEN** 脚本清空 Wiki 缓存、任务记录和代码索引数据（保留仓库和用户数据），将数据库恢复到可重新调试的状态

#### Scenario: 服务停止
- **WHEN** 开发者执行 `scripts/dev-stop.ps1`
- **THEN** 脚本优雅停止后端和前端进程，可选是否停止 PostgreSQL 容器

### Requirement: 环境变量模板机制
项目根目录 SHALL 包含 `.env.example` 模板文件，列出所有可配置环境变量及其说明和默认值。`.env` 文件 SHALL 被 `.gitignore` 排除。

#### Scenario: 模板文件完整性
- **WHEN** 新增 Provider 或配置项
- **THEN** `.env.example` SHALL 同步更新，包含新配置项的键名、中文说明和示例值

#### Scenario: 密钥安全防护
- **WHEN** 开发者尝试 `git add .env`
- **THEN** Git 自动忽略，`.env` 不会被提交到仓库

### Requirement: README 文档完善
README.md SHALL 基于当前系统实际能力重写，至少包含以下章节：项目简介、技术栈、架构概览、快速开始（含环境准备和密钥配置）、Provider 支持矩阵与配置指南、API 端点索引、调试指南、Docker 部署、常见故障排查。

#### Scenario: 新开发者上手
- **WHEN** 新开发者按 README 操作
- **THEN** 从克隆仓库到看到首页运行，全过程不超过 15 分钟，无需询问他人

#### Scenario: 调试指南可操作性
- **WHEN** 开发者遇到常见问题（如数据库连接失败、Provider 密钥未配置）
- **THEN** README 的故障排查章节提供明确的错误现象描述和解决步骤

### Requirement: CLAUDE.md 与 AGENTS.md 独立维护
项目根目录 SHALL 分别维护 `CLAUDE.md`（Claude Code 专用指令）和 `AGENTS.md`（通用 Agent 指令），两者内容独立不互相引用。

#### Scenario: Claude Code 正确识别
- **WHEN** Claude Code 在项目目录启动
- **THEN** Claude Code 读取 CLAUDE.md 中的专用指令，不再因引用链断裂而丢失上下文

#### Scenario: 通用 Agent 获取架构指令
- **WHEN** 通用 AI 编码工具读取 AGENTS.md
- **THEN** 获得完整的项目架构、修改原则和常见任务入口，与 CLAUDE.md 内容无依赖关系
