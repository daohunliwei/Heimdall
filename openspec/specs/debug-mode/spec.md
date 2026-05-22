## ADDED Requirements

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
- **THEN** 任务日志 SHALL 记录截断信息（截断前页面数、截断后页面数、被跳过的页面标题列表）
- **AND** 生成的 Wiki 版本元数据中包含 `debug_truncated: true` 标记

### Requirement: 调试模式配置 API
系统 SHALL 提供 RESTful API 管理 Debug Mode 配置：`GET /api/admin/debug-config`（查询状态）、`PUT /api/admin/debug-config`（更新开关和页数上限）。

#### Scenario: 查询调试配置
- **WHEN** 前端请求 `GET /api/admin/debug-config`
- **THEN** 返回 `{ "enabled": false, "maxDebugPages": 5 }`

#### Scenario: 更新调试配置
- **WHEN** 管理员 PUT `{ "enabled": true, "maxDebugPages": 3 }` 到 `/api/admin/debug-config`
- **THEN** 系统更新数据库中的配置值并返回 200，后续任务按新配置执行

#### Scenario: 页数上限校验
- **WHEN** 管理员设置 `maxDebugPages=0` 或负数
- **THEN** 系统返回 400 错误，提示"最大调试页数必须为正整数"
