## ADDED Requirements

### Requirement: Slides/Workshop 模型参数必填校验

Slides 和 Workshop 任务创建时，`model` 参数 SHALL 为必填字段。若请求未提供，后端 SHALL 使用系统默认配置或返回明确的错误提示（而非透传空值至 LLM API 导致 400 错误）。

#### Scenario: Slides 生成未提供 model 参数
- **WHEN** 用户发送 `POST /api/tasks/slides` 请求体缺少 `model` 字段
- **THEN** 系统从环境变量 `HEIMDALL_DEFAULT_MODEL` 读取默认模型，或从 Provider 的默认配置中获取
- **AND** 若系统级默认模型也不存在，返回 400 错误并提示 "model 参数为必填项，请选择模型"

#### Scenario: Workshop 生成正常提供 model 参数
- **WHEN** 用户发送 `POST /api/tasks/workshop` 请求体包含 `model: "gemma4:e2b"` 和 `provider: "ollama"`
- **THEN** 系统将 model 和 provider 正确传递至 LLM Provider 调用，任务成功创建

#### Scenario: Slides 页面 URL 中无 model 参数
- **WHEN** 用户从 Wiki 页面跳转到 Slides 页面，URL 查询参数中无 `model` 字段
- **THEN** Slides 页面在发起任务前展示模型选择弹窗，要求用户先选择模型

### Requirement: 界面显式展示当前模型名称

Slides 和 Workshop 页面 SHALL 在顶部栏显式展示当前使用的模型名称，让用户清楚知道由哪个模型生成内容。

#### Scenario: Slides 页面展示当前模型
- **WHEN** Slides 页面已加载并开始/完成生成
- **THEN** 页面顶部栏显示"当前模型：gemma4:e2b (Ollama)"的标签
- **AND** 标签颜色与 Provider 类型关联（如 Ollama 绿色、OpenAI 蓝色）

#### Scenario: Workshop 页面展示当前模型
- **WHEN** Workshop 页面已加载并开始/完成生成
- **THEN** 页面顶部栏显示"当前模型：qwen3 (Ollama)"的标签

### Requirement: 模型选择器统一交互

Slides、Workshop、RefreshPanel 中使用的模型/Provider 选择器 SHALL 统一使用 `UserSelector` 组件，提供一致的交互体验和清晰的选项说明。

#### Scenario: 模型选择器展示 Provider 和 Model 两级选项
- **WHEN** 用户在 Slides/Workshop 页面或刷新弹窗中打开模型选择器
- **THEN** 选择器展示 Provider 列表（如 Ollama、OpenAI、Google），每个 Provider 下列出可用 Model 列表
- **AND** 每个 Model 选项附带简短的说明（如模型大小、适用场景）
- **AND** 当前已选择的 Provider/Model 组合有选中样式标记

#### Scenario: 切换模型后重新生成
- **WHEN** 用户在 Slides 页面切换模型并点击"重新生成"
- **THEN** 系统使用新选择的 provider 和 model 参数重新调用 `POST /api/tasks/slides`，URL 参数同步更新
