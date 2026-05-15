## ADDED Requirements

### Requirement: 模型分级配置
系统 SHALL 支持用户为 Wiki 生成的不同阶段配置不同的 LLM 模型。配置项 SHALL 包含：结构规划模型、页面生成模型、质量审查模型。

#### Scenario: 用户自定义模型分级
- **WHEN** 用户在仓库设置中配置"页面生成使用 Claude Sonnet，结构规划使用 Claude Haiku"
- **THEN** Wiki 生成按配置选择对应模型，不同阶段调用不同 Provider

#### Scenario: 默认模型推荐
- **WHEN** 用户未配置模型分级
- **THEN** 系统使用默认策略：结构规划用默认 Provider 的廉价模型，页面生成用默认 Provider 的强模型

### Requirement: 成本估算
系统 SHALL 在 Wiki 生成开始前，根据仓库文件数量和选择的模型组合，给出预估成本范围。

#### Scenario: 生成前成本预估
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统返回预估的 LLM 调用次数和 Token 消耗量，展示给用户确认

#### Scenario: 成本超限告警
- **WHEN** 实际消耗超过预估的 150%
- **THEN** 系统在任务日志中记录告警，并暂停后续页面生成等待用户确认

### Requirement: 小模型质量警告
当用户配置的页面生成模型参数规模低于推荐阈值（默认 20B 参数）时，系统 SHALL 给出质量风险警告。

#### Scenario: 小模型警告
- **WHEN** 用户选择 Ollama Qwen2.5-7B 作为页面生成模型
- **THEN** 系统显示警告"当前模型参数较低，可能产生不准确的代码引用和示例代码，建议使用 30B+ 模型或 DeepSeek-V3 API"

### Requirement: 模型不可知性
系统 SHALL 支持用户使用任何兼容 OpenAI API 格式的模型服务（包括 Ollama 本地模型、DeepSeek API、Google Gemini、Bedrock），不强制绑定特定供应商。

#### Scenario: 接入自定义 API 端点
- **WHEN** 用户配置自定义 API 端点和模型名称
- **THEN** 系统按 OpenAI 兼容格式调用该端点，页面生成正常进行
