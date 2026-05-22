## ADDED Requirements

### Requirement: Provider 卡片式可视化
全局设置页面的 Provider 配置 Tab SHALL 以卡片布局替代当前表格视图。每张 Provider 卡片 SHALL 包含：Provider 名称（含类型图标）、连接状态指示灯（绿色=可用密钥/已配置，黄色=仅默认配置无自定义密钥，灰色=未配置）、模型数量、展开/折叠按钮。

#### Scenario: Provider 卡片基础展示
- **WHEN** 管理员打开全局设置页面的 Provider 配置 Tab
- **THEN** 页面以 2 列网格展示所有 Provider 卡片，每张卡片顶部显示 Provider 名称、类型图标（如 Ollama 羊驼图标、OpenAI 六边形图标）和连接状态指示灯

#### Scenario: 连接状态——已配置且可用
- **WHEN** OpenAI Provider 的 `OPENAI_API_KEY` 环境变量已设置且模型元数据已加载
- **THEN** OpenAI 卡片的状态指示灯为绿色，Tooltip 显示"已配置 · 3 个模型可用"

#### Scenario: 连接状态——默认配置无密钥
- **WHEN** MiniMax Provider 在 `generator.json` 中有默认配置但 `MINIMAX_API_KEY` 环境变量未设置
- **THEN** MiniMax 卡片的状态指示灯为黄色，Tooltip 显示"默认配置就绪 · 缺少 API Key"

#### Scenario: 连接状态——未配置
- **WHEN** 某 Provider 既无环境变量密钥也无 `generator.json` 默认配置
- **THEN** 该 Provider 卡片的状态指示灯为灰色，Tooltip 显示"未配置"

### Requirement: Provider 卡片内模型展示
展开 Provider 卡片后 SHALL 以标签组形式展示该 Provider 下所有模型的关键参数。每个模型一行，SHALL 显示：模型名称、计费类型标签（按次/按Token）、上下文窗口（格式化如 `128K`）、最大输出（格式化如 `32K`）、填充比例进度条、缓存支持图标。

#### Scenario: 模型参数可视化
- **WHEN** 管理员展开 OpenAI Provider 卡片
- **THEN** 卡片内显示 3 个模型的参数行，每行包含格式化后的参数值，上下文窗口和最大输出以 `K` 为单位显示，填充比例以迷你进度条呈现

#### Scenario: 模型编辑入口
- **WHEN** 管理员点击某模型的参数行
- **THEN** 弹出编辑弹窗（复用现有编辑弹窗逻辑），修改后保存

#### Scenario: 空模型列表
- **WHEN** 某 Provider 在数据库中无自定义元数据且 `generator.json` 中也无默认模型
- **THEN** 展开卡片后显示"暂无模型配置"占位文字

### Requirement: 系统运行时配置展示
全局设置页面 SHALL 包含"系统配置"Tab，展示当前运行时的关键配置项。以分组折叠面板（Accordion）展示：(1) 服务配置（认证模式、注册开关、管线版本、调试模式状态）；(2) 资源配置（数据目录、存储目录、配置目录、超时设置）；(3) Provider 密钥状态（各 Provider 的密钥是否已设置，显示掩码值）。

#### Scenario: 服务配置分组
- **WHEN** 管理员切换到"系统配置"Tab 并展开"服务配置"面板
- **THEN** 显示认证模式、开放注册、管线版本、调试模式状态等配置项的当前值和来源（环境变量 / 配置文件 / 默认值）

#### Scenario: Provider 密钥状态
- **WHEN** 管理员展开"Provider 密钥状态"面板
- **THEN** 以表格列出所有 Provider 的密钥环境变量名称、设置状态（✓ 已设置 / — 未设置），已设置的显示掩码值如 `sk-***abc123`

#### Scenario: 配置来源标记
- **WHEN** 某配置项的值来自环境变量
- **THEN** 该行末尾显示"环境变量"来源标签；若来自配置文件则显示"配置文件"标签；若使用默认值则显示"默认值"标签

### Requirement: 系统配置 API
系统 SHALL 提供 `GET /api/admin/system-config` 端点返回当前运行时配置和环境变量状态，包含：服务配置项、资源路径配置、各 Provider 密钥设置状态（仅返回是否设置和掩码值，不返回完整密钥）。

#### Scenario: 获取系统配置
- **WHEN** 前端请求 `GET /api/admin/system-config`
- **THEN** 返回 JSON 包含 `serviceConfig`、`resourceConfig`、`providerKeyStatus` 三个分组，敏感值以掩码形式返回

#### Scenario: 未认证访问拦截
- **WHEN** 未登录用户请求 `GET /api/admin/system-config`
- **THEN** 返回 401 Unauthorized

### Requirement: 全局设置页面 Tab 重组
全局设置页面 SHALL 重组为三个 Tab："Provider 管理"（原有的 Provider 元数据 CRUD，升级为卡片式）、"系统配置"（运行时配置和环境变量状态展示）、"调试设置"（Debug Mode 开关和页数上限设置）。

#### Scenario: Tab 切换保持状态
- **WHEN** 管理员在"Provider 管理"Tab 中展开了某 Provider 卡片，然后切换到"系统配置"Tab 再切回
- **THEN** Provider 卡片的展开/折叠状态保持（不重置）

#### Scenario: 调试设置 Tab
- **WHEN** 管理员切换到"调试设置"Tab
- **THEN** 页面显示 Debug Mode 开关（Toggle 组件）、最大调试页数输入框（默认 5，范围 1-20）、当前状态提示文字
