## ADDED Requirements

### Requirement: Provider 模型元数据持久化存储
系统 SHALL 将 Provider 模型元数据持久化到数据库表 `provider_model_metadata`，包含：ProviderKey、ModelName、BillingType、MaxContextTokens、MaxOutputTokens、InputTokenPrice、OutputTokenPrice、CallPrice、RateLimitPerMinute、SupportsCaching、ContextFillRatio、ContextWarningThreshold。

#### Scenario: 数据库优先读取
- **WHEN** 系统需要获取 MiniMax-M2.7 的元数据
- **THEN** 系统先查 `provider_model_metadata` 表，若存在记录则使用数据库值；若不存在则回退到 `generator.json` 中的默认值

#### Scenario: 运行时更新即时生效
- **WHEN** 管理员通过 API 更新某模型的 MaxContextTokens 从 1048576 到 204800
- **THEN** 下一次 LLM 调用立即使用新窗口值，无需重启服务

### Requirement: 模型元数据 CRUD API
系统 SHALL 提供 RESTful API 管理 Provider 模型元数据：`GET /api/admin/provider-metadata`（列表）、`PUT /api/admin/provider-metadata/{provider}/{model}`（创建/更新）、`DELETE /api/admin/provider-metadata/{provider}/{model}`（删除）。

#### Scenario: 列表查询
- **WHEN** 管理员请求 `GET /api/admin/provider-metadata`
- **THEN** 系统返回所有已配置 Provider 的全部模型元数据，按 ProviderKey 分组

#### Scenario: 更新模型元数据
- **WHEN** 管理员 PUT 新元数据到 `/api/admin/provider-metadata/minimax/MiniMax-M2.7`
- **THEN** 系统更新该模型的元数据记录并返回 200，内存缓存同步刷新

#### Scenario: 删除自定义元数据
- **WHEN** 管理员 DELETE `/api/admin/provider-metadata/ollama/gemma4:e2b`
- **THEN** 系统删除自定义记录，后续读取回退到 generator.json 默认值

### Requirement: 上下文窗口警戒阈值
每个模型 SHALL 支持配置 ContextWarningThreshold（默认 0.90）。当单次调用的 prompt Token 数超过 `MaxContextTokens * ContextWarningThreshold` 时，系统 SHALL 输出警告日志，并自动截断低优先级内容（如跨页面上下文）。

#### Scenario: 预警触发
- **WHEN** 模型 MaxContextTokens=128000，ContextWarningThreshold=0.90，某次调用 prompt 估算为 120000 tokens (>115200)
- **THEN** 系统输出 Warning 日志并截断跨页面上下文部分，确保总 Token 不超过预算

#### Scenario: 正常调用不触发
- **WHEN** 模型 MaxContextTokens=204800，ContextWarningThreshold=0.90，prompt 估算为 100000 tokens (<184320)
- **THEN** 系统正常执行，不截断任何内容

### Requirement: 按模型动态获取上下文预算
系统 SHALL 在页面生成阶段根据当前使用的模型的 MaxContextTokens 和 ContextFillRatio 动态计算允许的代码片段检索量，而非使用固定值。

#### Scenario: 大窗口模型最大化检索
- **WHEN** 使用 MiniMax-M2.7（MaxContextTokens=204800）
- **THEN** 代码片段可填充至约 204800 * 0.65 ≈ 133120 tokens 的预算上限

#### Scenario: 小窗口模型适度检索
- **WHEN** 使用 Ollama gemma4:e2b（典型上下文 8192 tokens）
- **THEN** 代码片段预算自动缩减至约 5325 tokens，优先保留高相关性片段

## MODIFIED Requirements

### Requirement: 全局设置页面——Provider 配置 Tab
前端 `/admin/settings` 页面 SHALL 包含"Provider 管理"Tab，以卡片式布局展示所有 Provider 的模型元数据列表。每张 Provider 卡片 SHALL 包含：Provider 名称与类型图标、连接状态指示灯、模型数量摘要、可展开的模型详情区。模型详情区以标签组形式展示每个模型的：名称、计费类型标签、上下文窗口（格式化如 `128K`）、最大输出 Token（格式化如 `32K`）、填充比例进度条、缓存支持图标。SHALL 支持行内编辑和删除。

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
