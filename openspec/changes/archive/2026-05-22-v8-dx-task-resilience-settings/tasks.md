## 1. 调试工作流与脚本体系

- [x] 1.1 创建 `.env.example` 模板文件，列出所有环境变量及说明、示例值和默认值，确保 `.env` 在 `.gitignore` 中
- [x] 1.2 创建 `scripts/dev-start.ps1` 一键启动脚本：检查 PostgreSQL Docker、生成 `.env`、执行迁移、并行启动前后端
- [x] 1.3 创建 `scripts/setup-env.ps1` 环境变量交互式配置脚本，引导填写 Provider 密钥并生成 `.env` 文件
- [x] 1.4 创建 `scripts/dev-reset.ps1` 数据重置脚本：清空 Wiki 缓存、任务记录、代码索引（保留仓库和用户数据）
- [x] 1.5 创建 `scripts/dev-stop.ps1` 服务停止脚本：优雅停止前后端进程，可选停止 PostgreSQL

## 2. 文档完善与 CLAUDE.md / AGENTS.md 拆分

- [x] 2.1 基于当前系统实际能力重写 README.md，补全环境准备、Provider 配置指南、调试指南、故障排查等章节
- [x] 2.2 将当前 AGENTS.md 中 Claude Code 专用内容拆分到独立的 CLAUDE.md（含工具使用提示、项目特定约定）
- [x] 2.3 精简 AGENTS.md 为通用 Agent 指令（架构规范、修改原则、常见任务入口），与 CLAUDE.md 无引用依赖

## 3. Debug Mode 后端实现

- [x] 3.1 创建 `SystemSetting` 实体与对应 EF Core 配置，支持 key-value 存储（DebugMode.Enabled、DebugMode.MaxPages）
- [x] 3.2 新增 EF Core 迁移，创建 `SystemSettings` 表并播种默认值（DebugMode=false, MaxPages=5）
- [x] 3.3 实现 `GET /api/admin/debug-config` 和 `PUT /api/admin/debug-config` API 端点
- [x] 3.4 在 Wiki 生成管线 Stage 5 入口注入 Debug Mode 检查逻辑：读取配置快照、按 MaxPages 截断页面列表、记录截断日志、设置 Wiki 版本 `debug_truncated` 标记

## 4. 任务断点续跑

- [x] 4.1 在 `TaskRecord` 实体新增 `ResumeCount` 字段，生成 EF Core 迁移
- [x] 4.2 实现 `POST /api/tasks/{taskId}/resume` 端点：校验任务状态、读取最后检查点、从中断位置继续执行
- [x] 4.3 实现 `TaskResumeService`（`IHostedService`）：启动时扫描僵尸任务（Running 且 5 分钟无更新），按创建时间顺序恢复
- [x] 4.4 实现自动恢复重试上限逻辑（连续失败 3 次后标记 Failed 不再重试）
- [x] 4.5 前端任务列表组件增加"恢复"按钮：对 Failed/Cancelled 且有检查点工件的任务显示恢复按钮，调用 Resume API
- [x] 4.6 在 `Program.cs` 注册 `TaskResumeService` 和 Resume 相关 DI 服务

## 5. 全局设置页面——Provider 卡片式可视化

- [x] 5.1 创建 `ProviderCard` 组件：卡片头部（Provider 名称、类型图标、连接状态指示灯、模型数量、展开/折叠按钮）
- [x] 5.2 创建 `ModelTagGroup` 组件：模型参数可视化标签组（名称、计费标签、上下文窗口、最大输出、填充比例进度条、缓存图标）
- [x] 5.3 重构 `frontend/src/app/admin/settings/page.tsx` 的"Provider 配置"Tab 为卡片式布局（2 列网格），集成 ProviderCard 和 ModelTagGroup
- [x] 5.4 后端新增 `GET /api/admin/provider-status` 端点：返回各 Provider 的连接状态（密钥是否设置、模型列表、配置来源）

## 6. 全局设置页面——系统配置与环境变量展示

- [x] 6.1 创建 `ConfigStatusPanel` 组件：分组折叠面板展示服务配置、资源配置、调试设置（使用 Accordion 布局）
- [x] 6.2 创建 `EnvVarStatusPanel` 组件：Provider 密钥状态表格（环境变量名、设置状态图标、掩码值、来源标签）
- [x] 6.3 实现 `GET /api/admin/system-config` 端点：返回运行时配置分组（serviceConfig、resourceConfig、providerKeyStatus），敏感值掩码
- [x] 6.4 在设置页面新增"系统配置"Tab 和"调试设置"Tab，集成 ConfigStatusPanel 和 EnvVarStatusPanel
- [x] 6.5 在"调试设置"Tab 实现 Debug Mode Toggle 开关和最大页数输入框，对接 Debug Config API

## 7. 集成验证

- [x] 7.1 端到端验证调试脚本全流程：从 `dev-start.ps1` 启动到 Wiki 生成成功
- [x] 7.2 验证 Debug Mode：开启后生成 ≤5 页，关闭后全量生成，运行时切换不影响当前任务
- [x] 7.3 验证任务恢复：手动取消任务 → 点击恢复按钮 → 从检查点继续；Kill 后端进程 → 重启 → 自动恢复僵尸任务
- [x] 7.4 验证设置页面：Provider 卡片展开/折叠、连接状态指示灯正确、系统配置分组展示、敏感值掩码
- [x] 7.5 前端 `npm run build` 和后端 `dotnet build` 均通过
