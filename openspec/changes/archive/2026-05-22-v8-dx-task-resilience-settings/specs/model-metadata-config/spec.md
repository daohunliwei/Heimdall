## MODIFIED Requirements

### Requirement: 全局设置页面——Provider 配置 Tab
前端 `/admin/settings` 页面 SHALL 包含"Provider 管理"Tab，**以卡片式布局替代当前表格视图**展示所有 Provider 的模型元数据列表。每张 Provider 卡片 SHALL 包含：Provider 名称与类型图标、连接状态指示灯、模型数量摘要、可展开的模型详情区。模型详情区以标签组形式展示每个模型的：名称、计费类型标签、上下文窗口（格式化如 `128K`）、最大输出 Token（格式化如 `32K`）、填充比例进度条、缓存支持图标。SHALL 支持行内编辑和删除。

#### Scenario: 查看 Provider 卡片列表
- **WHEN** 管理员打开 /admin/settings 页面的"Provider 管理"Tab
- **THEN** 页面以 2 列网格展示所有 Provider 卡片，每张卡片顶部有 Provider 名称、连接状态指示灯（绿/黄/灰）和模型数量

#### Scenario: 展开 Provider 卡片查看模型详情
- **WHEN** 管理员点击某 Provider 卡片的展开按钮
- **THEN** 卡片展开显示该 Provider 下所有模型的参数行，上下文窗口和最大输出以格式化数值显示，填充比例以进度条呈现

#### Scenario: 编辑模型元数据
- **WHEN** 管理员点击某模型的参数行
- **THEN** 弹出编辑表单，包含所有元数据字段，修改后保存即调用 PUT API 更新

#### Scenario: 连接状态——已配置且密钥可用
- **WHEN** 某 Provider 的环境变量密钥已设置（如 `OPENAI_API_KEY` 不为空）且模型元数据已加载
- **THEN** 该 Provider 卡片连接指示灯为绿色

#### Scenario: 连接状态——有默认配置但无密钥
- **WHEN** 某 Provider 在 `generator.json` 中有默认配置但其 API Key 环境变量未设置
- **THEN** 该 Provider 卡片连接指示灯为黄色

## ADDED Requirements

### Requirement: 系统配置展示 Tab
全局设置页面 SHALL 新增"系统配置"Tab，以分组折叠面板展示当前运行时配置和环境变量状态。

#### Scenario: 查看服务配置
- **WHEN** 管理员切换到"系统配置"Tab 并展开"服务配置"面板
- **THEN** 显示认证模式、注册开关、管线版本、调试模式状态等配置项的当前值及来源标记（环境变量 / 配置文件 / 默认值）

#### Scenario: 查看 Provider 密钥状态
- **WHEN** 管理员展开"Provider 密钥状态"面板
- **THEN** 以表格列出所有 Provider 的密钥环境变量名、设置状态（已设置/未设置）、已设置密钥的掩码显示（如 `sk-***abc123`）

#### Scenario: 配置来源标记
- **WHEN** 某配置项的值来自环境变量
- **THEN** 该行末尾显示"ENV"来源标签；来自配置文件则显示"FILE"标签；使用默认值则显示"DEFAULT"标签

### Requirement: 调试设置 Tab
全局设置页面 SHALL 新增"调试设置"Tab，包含 Debug Mode 开关（Toggle）和最大调试页数输入框。

#### Scenario: 开启调试模式
- **WHEN** 管理员在"调试设置"Tab 中拨动 Debug Mode Toggle 为"开"，设置最大页数为 5
- **THEN** 系统调用 `PUT /api/admin/debug-config` 保存设置，后续 Wiki 任务按 5 页上限生成

#### Scenario: 页数范围校验
- **WHEN** 管理员输入最大调试页数为 0 或超过 20
- **THEN** 前端显示验证提示"页数范围 1-20"，阻止保存
