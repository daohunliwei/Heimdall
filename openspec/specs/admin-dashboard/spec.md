## Purpose

管理后台仪表板——涵盖 Dashboard 统计指标聚合、API 端点及前端仪表板页面。
## Requirements
### Requirement: 仪表板统计指标聚合
系统 SHALL 通过 `DashboardService` 从多个仓储聚合仪表板指标：总任务数、完成率、活跃用户数、Token 使用量等。聚合 SHALL 在数据库端完成，不得全表加载后客户端聚合。

#### Scenario: 获取仪表板统计
- **WHEN** 管理员请求 `GET /api/admin/dashboard`
- **THEN** 系统返回 JSON 包含：总任务数、已完成/失败/运行中任务数、Token 消耗总量、活跃用户数、Wiki 版本数

#### Scenario: 统计数据的数据库端聚合
- **WHEN** `DashboardService` 计算仪表板指标
- **THEN** 使用 `SqlFunc.AggregateCount`/`AggregateSum` 在数据库端聚合，不将全表数据加载到内存

### Requirement: 仪表板 API 端点
系统 SHALL 通过 `DashboardController` 提供 `GET /api/admin/dashboard` 端点，返回聚合后的仪表板数据。端点 SHALL 要求 Admin 角色认证。

#### Scenario: 未认证访问拦截
- **WHEN** 未登录用户或 Viewer 角色用户请求仪表板端点
- **THEN** 返回 401 Unauthorized 或 403 Forbidden

### Requirement: 前端仪表板页面
前端 `/admin/dashboard` 页面 SHALL 展示统计卡片行（总任务数、完成率、Token 消耗、活跃用户）和可视化图表。

#### Scenario: 仪表板加载
- **WHEN** Admin 用户访问 `/admin/dashboard`
- **THEN** 页面调用 `GET /api/admin/dashboard` 获取统计数据，渲染统计卡片
