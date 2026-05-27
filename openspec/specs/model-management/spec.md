## Purpose

模型管理——涵盖模型选择器 UI、分级配置策略、上下文窗口与输出长度元数据、上下文预算动态计算及成本估算。
## Requirements
### Requirement: 模型选择器统一交互
Slides、Workshop、RefreshPanel 中使用的模型/Provider 选择器 SHALL 统一使用 UserSelector 组件，展示 Provider 和 Model 两级选项，附带模型说明。

#### Scenario: Slides/Workshop 模型参数必填校验
- **WHEN** 用户发送任务创建请求缺少 model 字段
- **THEN** 系统从环境变量 HEIMDALL_DEFAULT_MODEL 读取默认模型，若也不存在则返回 400 错误

### Requirement: 模型分级配置（已规划）
系统 SHALL 支持用户为 Wiki 生成的不同阶段配置不同的 LLM 模型。当前阶段模型选择统一使用系统默认 Provider/Model 组合，分阶段模型配置为后续迭代计划。

### Requirement: 模型不可知性
系统 SHALL 支持任何兼容 OpenAI API 格式的模型服务，不强制绑定特定供应商。

### Requirement: 上下文窗口警戒阈值与动态预算
每个模型 SHALL 支持配置 ContextWarningThreshold（默认 0.90）。当 prompt Token 超过 MaxContextTokens * ContextWarningThreshold 时触发截断。预算计算 SHALL 同时考虑输入预算（MaxContextTokens * ContextFillRatio）和输出上限（MaxOutputTokens），二者独立。

#### Scenario: 预警触发与截断
- **WHEN** 模型 MaxContextTokens=128000，prompt 估算超阈值
- **THEN** 系统输出 Warning 并按优先级递减裁剪：跨页面上下文 → 仓库文档 → 低相关性代码 → 基础提示词

#### Scenario: 大窗口模型最大化检索
- **WHEN** 使用 DeepSeek deepseek-v4-pro（MaxContextTokens=1048576, ContextFillRatio=0.85）
- **THEN** 代码片段输入预算约为 891K tokens，max_tokens 设置为 MaxOutputTokens=384000

#### Scenario: 小窗口模型适度检索
- **WHEN** 使用 8K 上下文模型
- **THEN** 代码片段预算自动缩减，优先保留高相关性片段

### Requirement: 模型输出长度独立配置
系统 SHALL 将模型的 MaxOutputTokens 作为 max_tokens 参数独立传递给 Provider API，不受 MaxContextTokens 或 ContextFillRatio 影响。

#### Scenario: 页面生成使用模型的 MaxOutputTokens
- **WHEN** 系统为 Wiki 页面生成调用 LLM，模型 MaxOutputTokens=384000
- **THEN** 请求体中的 max_tokens 参数设置为 384000

### Requirement: 成本估算
系统 SHALL 在 Wiki 生成开始前根据仓库文件数量和模型组合给出预估成本范围。

#### Scenario: 生成前成本预估
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统通过 `CostEstimationService` 返回预估的 LLM 调用次数和 Token 消耗量
