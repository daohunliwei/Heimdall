## MODIFIED Requirements

### Requirement: Provider 计费模型元数据
系统 SHALL 为每个 Provider/Model 组合维护计费模型元数据。**变更**：元数据从 `generator.json` 硬编码改为数据库驱动，支持运行时通过全局设置页面修改。元数据字段新增 ContextFillRatio（上下文填充比例，默认 0.65）和 ContextWarningThreshold（上下文警戒阈值，默认 0.90）。

#### Scenario: 数据库优先加载
- **WHEN** 系统启动或首次访问某模型元数据
- **THEN** 先查 `provider_model_metadata` 表，命中则使用数据库值；未命中则从 `generator.json` 加载并自动插入数据库

#### Scenario: 全局设置页面修改即时生效
- **WHEN** 管理员在全局设置页修改 MiniMax-M2.7 的 MaxContextTokens 为 204800
- **THEN** 系统更新数据库记录并刷新内存缓存，下一次 LLM 调用立即使用新元数据

### Requirement: 上下文窗口智能填充引擎
系统 SHALL 提供 `IContextPackingService` 接口，根据模型的 MaxContextTokens 和 ContextFillRatio 动态分配 prompt 各部分的 Token 预算。**变更**：ContextFillRatio 从硬编码 0.65 改为从模型元数据读取；新增 ContextWarningThreshold 警戒机制。

#### Scenario: 大窗口模型预算分配
- **WHEN** 模型 MaxContextTokens=204800，ContextFillRatio=0.65
- **THEN** 总预算 133120 tokens，系统提示词 2000 + 页面元数据 1500 + 代码片段可用空间约 124620 tokens + 跨页面上下文 5000 tokens

#### Scenario: 警戒阈值触发截断
- **WHEN** 代码片段总长度超出 ContextWarningThreshold 限制
- **THEN** 系统自动截断低优先级内容（跨页面上下文 → 低相关代码片段），日志输出 Warning 级别提醒

### Requirement: CodingPlan 调用策略
当模型 BillingType 为 CodingPlan 时，系统 SHALL 采用"合并调用、填满上下文"策略。**不变**：合并逻辑保持，但合并上限（3 页）和 ContextFillRatio 改为从模型元数据读取。

### Requirement: TokenPlan 调用策略
当模型 BillingType 为 TokenPlan 时，系统 SHALL 采用"单页调用、最大化上下文利用"策略。**变更**：每页代码片段检索量从模型元数据的 ContextFillRatio 读取。

## ADDED Requirements

### Requirement: 全局设置页面 Provider 配置入口
前端 `/admin/settings` 页面 SHALL 包含"Provider 配置"功能模块，替代当前空白页。页面 SHALL 展示所有已注册 Provider 的列表及各自的模型元数据，支持查看、编辑、重置为默认值。

#### Scenario: Provider 列表展示
- **WHEN** 管理员打开 /admin/settings → Provider 配置 Tab
- **THEN** 页面展示 Ollama、MiniMax、OpenAI 等 Provider 列表，每个展开可见旗下模型及元数据

#### Scenario: 编辑元数据并保存
- **WHEN** 管理员修改 MiniMax-M2.7 的 MaxContextTokens 并点击保存
- **THEN** 系统调用 PUT API 更新数据库，页面提示"保存成功"，后续调用立即生效
