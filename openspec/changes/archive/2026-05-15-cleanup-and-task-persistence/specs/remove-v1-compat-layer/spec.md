## ADDED Requirements

### Requirement: 删除旧 Wiki 实体
系统 SHALL 删除 `Heimdall.Core.Entities.Wiki` 实体类及对应的 `WikiConfiguration`、`IWikiRepository`、`WikiRepository` 文件。`AppDbContext` 中 SHALL 移除 `Wikis` DbSet 及相关配置。

#### Scenario: 编译通过
- **WHEN** 删除 Wiki 实体及相关文件
- **THEN** `dotnet build` SHALL 零错误通过

#### Scenario: 数据库表移除
- **WHEN** 应用新迁移
- **THEN** `wikis` 表 SHALL 被删除

### Requirement: WikiPage 移除旧外键
`WikiPage` 实体 SHALL 删除 `WikiId` 属性和 `Wiki` 导航属性。`WikiVersionId` SHALL 改为非空必填（`Guid` 而非 `Guid?`）。

#### Scenario: 页面必须绑定版本
- **WHEN** 创建新的 WikiPage
- **THEN** `WikiVersionId` SHALL 为必填字段，不可为 null

### Requirement: 删除 WikiCacheController
系统 SHALL 删除 `Heimdall.Api.Controllers.WikiCacheController`。前端所有 Wiki 缓存相关请求已通过 `WikiVersionController` 处理。

#### Scenario: API 路由不冲突
- **WHEN** 删除 WikiCacheController
- **THEN** 其他 WikiVersion 相关 API SHALL 正常响应

### Requirement: 清理 Repository 导航
`Core.Entities.Repository` SHALL 移除 `Wikis` 导航集合属性。

#### Scenario: Repository 实体无旧引用
- **WHEN** 访问 Repository 实体
- **THEN** 不存在 Wikis 导航属性

### Requirement: Dashboard 统计迁移
`DashboardService` SHALL 使用 `WikiVersion` 替代 `Wiki` 进行 Wiki 数量统计。

#### Scenario: 仪表盘 Wiki 计数正确
- **WHEN** 查询仪表盘统计数据
- **THEN** Wiki 数量 SHALL 来自 `WikiVersion` 表计数
