## Purpose

系统配置管理——涵盖 CodeFirst 自动同步机制、全局设置 Dashboard（Provider 卡片式管理、系统运行时配置展示、调试设置 Tab）。
## Requirements
### Requirement: CodeFirst 配置开关
系统 SHALL 在 appsettings.json 中提供 CodeFirst 配置节，包含 AutoSync 布尔属性，控制启动时是否自动同步数据库结构。环境变量 HEIMDALL_CODEFIRST_AUTOSYNC 可覆盖配置。

#### Scenario: AutoSync 为 true 时自动同步
- **WHEN** CodeFirst.AutoSync 为 true 且应用启动
- **THEN** 系统调用 db.CodeFirst.SetStringDefaultLength(200).InitTables(entityTypes) 自动创建或更新所有实体表

#### Scenario: AutoSync 为 false 时跳过同步
- **WHEN** CodeFirst.AutoSync 为 false 或未配置
- **THEN** 系统跳过同步步骤，输出 Information 日志

### Requirement: 启动时扫描实体类型
系统 SHALL 在启动时自动扫描 Core.Entities 命名空间下所有 SqlSugar 实体类，排除 DTO 和纯数据类。

#### Scenario: 实体类型扫描
- **WHEN** CodeFirst 同步启动
- **THEN** 系统从 Heimdall.Core.Entities 程序集中扫描所有标注了 [SugarTable] 的类型

### Requirement: CodeFirst 同步失败不阻塞启动
系统 SHALL 确保 CodeFirst 同步失败时不影响应用正常启动。同步完成后输出日志摘要（成功表数、失败表数、总耗时）。

#### Scenario: 单表同步失败继续处理
- **WHEN** 某实体因表冲突或权限问题失败
- **THEN** 系统记录 Error 日志，继续处理其余实体

#### Scenario: 全部失败时记录错误并启动
- **WHEN** 所有实体同步均失败
- **THEN** 系统记录 Critical 日志，应用继续启动

### Requirement: Provider 卡片式可视化
全局设置页面的 Provider 配置 Tab SHALL 以卡片布局替代表格视图。每张卡片包含：Provider 名称、连接状态指示灯（绿色=已配置/黄色=仅默认配置/灰色=未配置）、模型数量、展开/折叠按钮。展开后以标签组形式展示模型关键参数。

#### Scenario: Provider 卡片基础展示
- **WHEN** 管理员打开全局设置页面的 Provider 配置 Tab
- **THEN** 页面以网格展示所有 Provider 卡片，每张卡片显示名称、状态指示灯

#### Scenario: 模型参数可视化
- **WHEN** 管理员展开某 Provider 卡片
- **THEN** 卡片内显示模型参数行，上下文窗口和最大输出以 K 为单位，填充比例以迷你进度条呈现

### Requirement: 系统运行时配置展示
全局设置页面 SHALL 包含"系统配置"Tab，以分组折叠面板展示：服务配置（认证模式、管线版本等）、资源配置（数据目录、存储目录等）、Provider 密钥状态（掩码值）。

#### Scenario: 配置来源标记
- **WHEN** 某配置项的值来自环境变量
- **THEN** 该行末尾显示"环境变量"来源标签

### Requirement: 系统配置 API
系统 SHALL 提供 GET /api/admin/system-config 端点返回当前运行时配置和密钥状态（仅掩码，不返回完整密钥）。

#### Scenario: 获取系统配置
- **WHEN** 前端请求 GET /api/admin/system-config
- **THEN** 返回 JSON 包含 serviceConfig、resourceConfig、providerKeyStatus 三个分组

### Requirement: 全局设置页面 Tab 重组
全局设置页面 SHALL 重组为三个 Tab："Provider 管理"（卡片式）、"系统配置"（运行时配置展示）、"调试设置"（Debug Mode 开关和页数上限）。

#### Scenario: Tab 切换保持状态
- **WHEN** 管理员在各 Tab 间切换
- **THEN** Provider 卡片的展开/折叠状态保持
