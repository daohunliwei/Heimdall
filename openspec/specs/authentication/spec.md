## Purpose

用户认证与授权——涵盖 JWT Token 签发与验证、BCrypt 密码哈希、用户 CRUD（注册/删除/角色管理）及前端登录页面。
## Requirements
### Requirement: JWT Token 签发与验证
系统 SHALL 通过 `JwtTokenService` 签发 JWT Token，包含用户 ID、用户名、角色等 Claims。Token 有效期和密钥 SHALL 通过配置项管理。所有 Admin 前缀的 API 端点 SHALL 通过 `[Authorize(Roles = "Admin")]` 中间件校验 JWT。

#### Scenario: 用户登录获取 Token
- **WHEN** 用户提交 `POST /api/auth/login` 包含有效凭据
- **THEN** 系统返回 JWT Token（access_token）、过期时间、用户信息

#### Scenario: Token 验证失败
- **WHEN** 请求包含无效或过期的 JWT Token
- **THEN** 系统返回 401 Unauthorized

#### Scenario: 非 Admin 用户访问管理端点
- **WHEN** Viewer 角色用户访问 Admin 端点
- **THEN** 系统返回 403 Forbidden

### Requirement: 用户 CRUD 与密码安全
系统 SHALL 通过 `UserService` 管理用户。密码 SHALL 使用 BCrypt 哈希存储，明文密码不得写入日志或数据库。

#### Scenario: 创建用户
- **WHEN** 管理员通过 `POST /api/admin/users` 创建用户
- **THEN** 密码经 BCrypt 哈希后存储，返回用户信息（不含密码哈希）

#### Scenario: 用户登录验证
- **WHEN** 用户登录
- **THEN** `UserService.ValidateAndGetUserAsync` 一次查询完成 BCrypt 验证并返回用户对象

#### Scenario: 删除用户
- **WHEN** 管理员通过 `DELETE /api/admin/users/{id}` 删除用户
- **THEN** 用户记录从数据库移除

### Requirement: 角色与权限
系统 SHALL 支持 Admin 和 Viewer 两种角色。Admin 可管理用户、系统设置和 Provider 配置；Viewer 仅可查看仓库和 Wiki 内容。

#### Scenario: 角色分配
- **WHEN** 创建用户时指定角色
- **THEN** 角色持久化到数据库，每次请求通过 JWT Claims 校验

### Requirement: 前端登录页面
前端 `/login` 页面 SHALL 提供用户名/密码登录表单，登录成功后跳转到首页或之前访问的页面。登录状态 SHALL 通过 JWT Token 在客户端持久化。
