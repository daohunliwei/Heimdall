## Why

当前开发调试流程高度依赖人工：每次调试需手动准备测试数据、手动设置环境变量、手动执行命令查看结果，LLM 辅助调试工具（如 Claude Code）也因缺少标准化脚本和安全的密钥管理而难以高效介入。同时 Wiki 生成任务一旦中断（无论人工取消还是异常崩溃）必须从头开始，单次调试耗时过长。此外全局设置页面仍以简陋的表格呈现 Provider 配置，缺少对环境变量和运行时配置的系统化管理界面。这三个问题共同阻碍了 V8 迭代的快速推进。

## What Changes

- **调试工作流与脚本体系建设**：创建标准化的开发环境准备脚本、密钥安全注入脚本、一键启动/停止/重置脚本，让调试流程可重复、可自动化
- **环境变量与密钥安全管理**：建立 `.env.example` 模板机制和安全注入流程，消除密钥硬编码和泄露风险，解决 IDE 阻止执行的安全顾虑
- **README 文档完善**：基于当前实际能力重写 README，补充完整的 Provider 配置矩阵、调试指南、故障排查等内容
- **CLAUDE.md 与 AGENTS.md 分离**：将混在一起的 AI 指令文档拆分为独立的 CLAUDE.md（Claude Code 专用）和 AGENTS.md（通用 Agent 指令），确保 Claude Code 正确识别
- **调试模式开关**：增加全局 Debug Mode 开关，开启后将 Wiki 生成页数限制在可配置上限内（默认 5 页），大幅缩短调试反馈周期
- **任务断点续跑**：任务中断后支持从最后完成的阶段/批次自动恢复（服务启动时）或手动恢复（页面点击恢复按钮），避免重复劳动
- **全局设置页面——Provider 与模型可视化**：以卡片、标签、状态指示灯等现代化 UI 呈现所有已配置 Provider 及其模型列表、连接状态、关键参数
- **全局设置页面——配置与环境变量管理**：展示当前系统运行时的关键配置项和环境变量状态（已设置/未设置/默认值），支持值掩码显示保护敏感信息

## Capabilities

### New Capabilities

- `debug-workflow`: 标准化开发调试脚本体系，涵盖环境准备、密钥注入、服务启停、数据重置等全流程；环境变量安全模板机制；README 文档完善
- `debug-mode`: 全局调试模式开关，开启后 Wiki 生成管线限制最大生成页数，支持运行时切换，大幅缩短调试迭代周期
- `task-resume`: 任务中断后的断点续跑能力，支持服务启动后自动恢复未完成任务和页面手动触发恢复两种模式，从最后完成的检查点继续执行
- `settings-dashboard`: 全局设置页面 UI 重构，以卡片式布局展示 Provider/模型详情和系统配置/环境变量状态，支持可视化管理

### Modified Capabilities

- `wiki-generation-pipeline`: 管线在页面生成阶段需检查 Debug Mode 开关状态，开启时按配置上限截断生成页面列表
- `task-progress-persistence`: 在现有阶段落盘基础上新增任务恢复端点（`POST /api/tasks/{id}/resume`）和启动时自动恢复逻辑，支持从中断点继续执行
- `model-metadata-config`: 全局设置页面中 Provider 配置 Tab 的 UI 从当前表格形式升级为卡片式可视化呈现，新增连接状态和模型能力标签展示

## Impact

- `scripts/` — 新增或重构开发调试脚本（env setup、service start/stop、data reset 等）
- `backend/Heimdall.Api/config/` — 新增 debug 相关配置项
- `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs` — 管线增加 Debug Mode 页数限制和 Resume 逻辑
- `backend/Heimdall.Core/Services/Tasks/AgentOrchestratorService.cs` — 子代理协调增加断点续跑支持
- `backend/Heimdall.Api/Controllers/TasksController.cs` — 新增 Resume 端点
- `backend/Heimdall.Api/Controllers/AdminController.cs` — 新增系统配置/环境变量查询端点
- `backend/Heimdall.Core/Interfaces/` — 新增 Debug Mode 和 Resume 相关接口
- `backend/Heimdall.Repository/` — 可能需要新增 Debug 配置或 Resume 检查点相关迁移
- `frontend/src/app/admin/settings/page.tsx` — 全局设置页面 UI 重构
- `frontend/src/components/` — 新增 Provider 卡片、配置状态等可视化组件
- `README.md` — 内容完善重写
- `CLAUDE.md` — 从 AGENTS.md 拆分，独立维护
- `AGENTS.md` — 精简为通用 Agent 指令
