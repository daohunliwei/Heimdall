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

### Requirement: 全局设置页面——Provider 配置 Tab
前端 `/admin/settings` 页面 SHALL 包含"Provider 配置"Tab，展示所有 Provider 的模型元数据列表。每行 SHALL 显示：Provider 名称、模型名称、计费类型、上下文窗口、最大输出 Token、输入/输出价格、缓存支持。SHALL 支持行内编辑和删除。

#### Scenario: 查看 Provider 模型列表
- **WHEN** 管理员打开 /admin/settings 页面的 Provider 配置 Tab
- **THEN** 页面展示按 Provider 分组的模型列表，每行包含 MaxContextTokens、BillingType、价格等关键字段

#### Scenario: 编辑模型元数据
- **WHEN** 管理员点击某模型的"编辑"按钮
- **THEN** 弹出编辑表单，包含所有元数据字段，修改后保存即调用 PUT API 更新

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
