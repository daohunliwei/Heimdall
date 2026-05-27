## Purpose

开发调试体验——涵盖调试模式开关与页数截断、标准化调试脚本体系、环境变量模板机制及项目文档维护。
## Requirements
### Requirement: 调试模式开关
系统 SHALL 支持全局 Debug Mode 开关，通过数据库系统设置表持久化，支持运行时热切换，无需重启服务。

#### Scenario: 开启调试模式
- **WHEN** 管理员通过 API 或设置页面将 Debug Mode 设为 `true`，并设置 `MaxDebugPages=5`
- **THEN** 后续所有 Wiki 生成任务最多生成 5 页内容，超出部分被截断

#### Scenario: 关闭调试模式
- **WHEN** 管理员将 Debug Mode 设为 `false`
- **THEN** 后续 Wiki 生成任务恢复正常全量生成

#### Scenario: 运行时切换立即生效
- **WHEN** 管理员在某个 Wiki 任务执行过程中切换 Debug Mode 开关
- **THEN** 当前正在执行的任务不受影响（使用任务启动时的配置快照），下一个新任务使用新配置

### Requirement: 调试模式页数截断
当 Debug Mode 开启时，Wiki 生成管线 SHALL 在 Stage 5（页面生成）执行前，将生成页面列表截断至 `MaxDebugPages` 上限。截断 SHALL 优先保留顶级页面（`ParentId == null` 或 `Depth == 0`），再按拓扑序填充至上限。

#### Scenario: 页面列表截断——少于上限
- **WHEN** 结构规划输出 3 个页面，`MaxDebugPages=5`
- **THEN** 全部 3 个页面正常生成，不截断

#### Scenario: 页面列表截断——超出上限
- **WHEN** 结构规划输出 20 个页面，`MaxDebugPages=5`
- **THEN** 仅前 5 个页面（按拓扑序，优先顶级页面）进入生成阶段，剩余 15 页被跳过

#### Scenario: 截断标记
- **WHEN** Debug Mode 截断了页面列表
- **THEN** 任务日志 SHALL 记录截断信息，生成的 Wiki 版本元数据中包含 `debug_truncated: true` 标记

### Requirement: 调试模式配置 API
系统 SHALL 提供 RESTful API 管理 Debug Mode 配置。

#### Scenario: 查询调试配置
- **WHEN** 前端请求 `GET /api/admin/debug-config`
- **THEN** 返回 `{ "enabled": false, "maxDebugPages": 5 }`

#### Scenario: 更新调试配置
- **WHEN** 管理员 PUT `{ "enabled": true, "maxDebugPages": 3 }` 到 `/api/admin/debug-config`
- **THEN** 系统更新数据库中的配置值并返回 200

#### Scenario: 页数上限校验
- **WHEN** 管理员设置 `maxDebugPages=0` 或负数
- **THEN** 系统返回 400 错误

### Requirement: 标准化调试启动脚本
系统 SHALL 在 `scripts/` 目录提供一套 PowerShell 调试脚本，覆盖开发全流程：环境准备、密钥注入、服务启动/停止、数据重置。

#### Scenario: 一键启动开发环境
- **WHEN** 开发者执行 `scripts/dev-start.ps1`
- **THEN** 脚本按顺序：检查 PostgreSQL → 生成 `.env` → 提示填写密钥 → 数据库迁移 → 并行启动前后端

#### Scenario: 环境变量安全注入
- **WHEN** 开发者执行 `scripts/setup-env.ps1`
- **THEN** 交互式引导用户填入各 Provider 的 API Key，密钥输入时屏幕回显掩码

#### Scenario: 数据重置
- **WHEN** 开发者执行 `scripts/dev-reset.ps1`
- **THEN** 脚本清空 Wiki 缓存、任务记录和代码索引数据（保留仓库和用户数据）

#### Scenario: 服务停止
- **WHEN** 开发者执行 `scripts/dev-stop.ps1`
- **THEN** 脚本优雅停止后端和前端进程

### Requirement: 环境变量模板机制
系统 SHALL 在 `scripts/` 目录维护 `dev.env.example` 模板文件，列出所有可配置环境变量及其说明和默认值。`.env` 和 `scripts/dev.env` 文件 SHALL 被 `.gitignore` 排除。

#### Scenario: 模板文件完整性
- **WHEN** 新增 Provider 或配置项
- **THEN** `scripts/dev.env.example` SHALL 同步更新，包含新配置项的键名、中文说明和示例值

#### Scenario: 密钥安全防护
- **WHEN** 开发者尝试 `git add .env`
- **THEN** Git 自动忽略，`.env` 不会被提交到仓库

### Requirement: README 与 CLAUDE.md/AGENTS.md 文档维护
README.md SHALL 基于当前系统实际能力编写，至少包含：项目简介、技术栈、架构概览、快速开始、Provider 配置指南、API 端点索引、调试指南、Docker 部署、常见故障排查。项目根目录 SHALL 分别维护 `CLAUDE.md`（Claude Code 专用）和 `AGENTS.md`（通用 Agent），两者内容独立不互相引用。

#### Scenario: 新开发者上手
- **WHEN** 新开发者按 README 操作
- **THEN** 从克隆仓库到看到首页运行，全过程不超过 15 分钟

#### Scenario: Claude Code 正确识别
- **WHEN** Claude Code 在项目目录启动
- **THEN** Claude Code 读取 CLAUDE.md 中的专用指令，不再因引用链断裂而丢失上下文
