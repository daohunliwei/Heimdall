## MODIFIED Requirements

### Requirement: ChatOptions.Tools 注入
系统 SHALL 继续通过 `ChatOptions.Tools` 注入 Tool Call 所需的 `AIFunction` 列表，但 Tool Call 的配置读取、默认值处理和阶段开关判定 SHALL 统一通过 `ToolCallConfigurationService` 完成。

#### Scenario: Stage 3 开关统一由配置服务判定
- **WHEN** `WikiTaskService` 需要判断 Stage 3 是否开启 Tool Call
- **THEN** 系统调用 `ToolCallConfigurationService` 的语义化接口获取结果
- **AND** 不直接在 `WikiTaskService` 中读取 `ToolCall.Stage3.Enabled` 键值

#### Scenario: Stage 5 开关统一由配置服务判定
- **WHEN** `WikiTaskService` 需要判断 Stage 5 是否开启 Tool Call
- **THEN** 系统调用 `ToolCallConfigurationService` 的语义化接口获取结果
- **AND** `ToolCall.Enabled` 的全局总开关由同一服务一并处理

### Requirement: Tool Call 配置的唯一事实来源
系统 SHALL 以 `ToolCallConfigurationService` 作为唯一的 Tool Call 配置事实来源。任何新入口若需要 Tool Call 阶段控制，也必须复用该服务，而不是自行读取 `SystemSetting`。

#### Scenario: 配置读取失败时统一降级
- **WHEN** `SystemSetting` 读取失败、缺失或格式非法
- **THEN** `ToolCallConfigurationService` 统一返回“全部关闭”或等价安全降级结果
- **AND** 调用侧不再自行实现降级逻辑

#### Scenario: 后续新入口复用同一配置口
- **WHEN** 未来 `Ask`、`Chat` 或其他任务入口需要增加 Tool Call 开关
- **THEN** 直接扩展 `ToolCallConfigurationService`
- **AND** 不新增第二套配置读取实现
