## Context

当前实现已经完成 `Microsoft.Extensions.AI` 10.6.0 升级与 `IChatClient` 主路径落地，但仍有五类架构债务需要一次性收口：

1. 文档、注释、OpenSpec 规格仍大量描述 `BM25 + pgvector`、10 阶段与向量嵌入等能力，而代码现实已经是 `BM25` 主导、8 阶段主流程、无向量链路
2. `ChatClientFactory` 已经被标记为废弃，但 `ChatController` 和 `AskTaskService` 仍继续依赖，导致 Keyed DI 与工厂并存
3. `Chat` 与 `Ask` 仍以“大 Prompt”方式组织历史、证据与问题，`ChatMessage` 多角色能力没有被正确发挥
4. Tool Call 配置已抽出 `ToolCallConfigurationService`，但使用方仍存在重复读取配置的空间
5. 多 Provider 注册、自定义后端消息转换、`ChatOptions` 使用方式仍存在“部分官方、部分项目自定义”的混搭形态

本次设计以“按当前代码现实收口”为原则，不引入向量检索，不继续扩展 Telemetry，只围绕一致性、可维护性和 MEAI 官方推荐用法做收敛。

## Goals / Non-Goals

**Goals:**
- 以当前代码实现为准，修正文档、注释、OpenSpec 规格中的过时描述
- 删除 `ChatClientFactory` 在运行时调用链中的角色，仅保留 Keyed DI 获取 `IChatClient`
- 让 `ChatController`、`AskTaskService`、`TaskLlmService` 等入口全面采用多角色 `ChatMessage` 链路
- 统一 `ToolCallConfigurationService` 为唯一 Tool Call 配置入口
- 统一 Provider 注册、消息映射、`ChatOptions` 使用方式，向 MEAI 官方编程模型收口
- 保持现有流式、非流式、Tool Call、任务日志和 Wiki 生成能力继续工作

**Non-Goals:**
- 不恢复 `Embedding`、`pgvector`、向量召回或混合检索
- 不新增 Telemetry exporter、dashboard、trace 关联或监控面板演进
- 不改造前端交互协议，仅在后端调整消息组织与实现方式
- 不改变 Wiki 主业务目标和产物格式

## Decisions

### 决策 1：文档、注释、规格以当前代码实现为准

**选择**：以代码中的真实运行链路为权威来源，系统性修正文档、注释和 OpenSpec baseline 规格中的过时描述。

**理由**：当前误导最严重的不是代码本身，而是“文档和注释承诺了不存在的能力”。如果不先消除这部分噪音，后续任何检索、问答或 RAG 改造都容易建立在错误前提之上。

**落点**：
- `docs/architecture/**`
- `README.md`
- `openspec/specs/wiki-generation-pipeline/spec.md`
- `openspec/specs/hybrid-code-retrieval/spec.md`
- `AskTaskService`、`HybridSearchService` 等核心注释

### 决策 2：Keyed DI 成为唯一 `IChatClient` 获取模式

**选择**：删除 `ChatClientFactory` 在运行时的依赖链，所有需要动态选择 Provider 的位置统一使用 `IServiceProvider.GetRequiredKeyedService<IChatClient>(providerId)`。

**替代方案**：继续保留 `ChatClientFactory` 作为过渡层。

**理由**：
- `ChatClientFactory` 已不再提供真实增值能力
- `ConcurrentDictionary` 缓存与 Singleton DI 重复
- 默认客户端 fallback 会掩盖 Provider 注册错误
- 双轨模式会让后来者继续写出新依赖

**约束**：
- 运行时禁止 fallback 到默认 `IChatClient`
- 新代码不得继续注入 `ChatClientFactory`
- 若需要统一异常信息，可通过扩展方法而不是工厂层完成

### 决策 3：引入统一的多角色消息构建器

**选择**：增加一个集中式消息构建组件，负责把系统提示、版本约束、证据上下文、历史对话与当前问题组合成 `List<ChatMessage>`，而不是在控制器和服务里手写字符串拼装。

**理由**：
- 当前 `ChatController` 丢弃历史，只取最后一条用户消息
- 当前 `AskTaskService` 把历史与证据压成单条 `User` 消息
- 多处重复拼接字符串，难以保持角色边界一致

**消息组织策略**：
- `System`：稳定的行为约束、输出语言、版本绑定规则
- `User / Assistant`：保留真实历史轮次与顺序
- `User`：单独承载“当前版本证据包”或“补充上下文”消息
- `User`：当前最新问题单独成消息
- `Tool`：仅由 `FunctionInvokingChatClient` 产生和回写

### 决策 4：`TaskLlmService` 以消息为主，字符串为辅

**选择**：将 `TaskLlmService` 的主入口调整为接收 `IReadOnlyList<ChatMessage>` + `ChatOptions`，字符串 Prompt 入口只保留为一层便捷包装。

**理由**：
- 这样可以让 Chat、Ask、Wiki 阶段性调用共享统一 MEAI 主路径
- Tool Call、流式、Usage、消息日志都可以围绕同一模型组织
- 可以逐步减少“先拼大字符串，再塞进单轮 user message”的历史实现

### 决策 5：Tool Call 配置只保留一个入口

**选择**：所有 `ToolCall.Enabled`、`ToolCall.Stage3.Enabled`、`ToolCall.Stage5.Enabled` 的读取、默认值降级和阶段判定全部收口到 `ToolCallConfigurationService`。

**替代方案**：允许 `WikiTaskService` 或其他任务服务自行读取 `ISystemSettingRepository`。

**理由**：
- Tool Call 是典型的运行期开关，重复实现会产生“同名配置、不同默认值”的风险
- 单一入口更方便后续扩展缓存、指标或灰度策略

**接口方向**：
- `GetConfigAsync()` 保留聚合读取能力
- 新增按阶段判断的轻量接口，如 `IsStageEnabledAsync(stage)` 或等价方法
- 调用侧只消费语义化结果，不处理键名和解析细节

### 决策 6：按官方 MEAI 用法收敛 Provider 与自定义后端

**选择**：统一 Provider 注册帮助方式、消息角色映射和 `ChatOptions` 处理逻辑，保持所有 Provider 都走同一套 Builder 与参数约定。

**落点**：
- OpenAI 兼容 Provider 与自定义后端都统一处理 `ModelId`、`MaxOutputTokens`、`Temperature`、`Tools`
- 自定义后端必须保留 `system / user / assistant / tool` 角色语义，不得再次把消息压扁为单字符串
- 流式与非流式共享相同的消息转换路径

**说明**：Telemetry 仍维持当前 `UseOpenTelemetry()` 接入，不继续扩展 exporter 或 dashboard。

## Risks / Trade-offs

- **输出风格变化风险**：从大 Prompt 切到多角色后，回答的措辞和细节可能变化，需要通过回归问答样本确认质量未下降
- **历史兼容风险**：旧逻辑默认把证据直接嵌入问题，切换后若消息顺序设计不当，可能导致模型忽略证据
- **Provider 行为差异风险**：不同 Provider 对多条 `System` / `User` 消息的响应敏感度不同，自定义后端需要实测
- **文档清理范围大**：仓库内“混合检索 / pgvector / 向量”描述分布广，需要系统化清点，避免漏改
- **工厂移除暴露配置问题**：删除默认客户端 fallback 后，未注册 Provider 会更早失败，但这是期望中的显性化问题

## Migration Plan

### 实施顺序

1. **先收敛规格与文档基线**：更新 OpenSpec baseline、架构文档和注释，统一对外口径
2. **移除工厂依赖**：先改 `ChatController`、`AskTaskService`，再移除 `ChatClientFactory` 注册与引用
3. **引入消息构建器**：先在 Chat、Ask 两条入口切换到多角色消息链路，再向 Wiki 相关任务收敛
4. **收口 Tool Call 配置**：让 `WikiTaskService` 与后续入口只依赖 `ToolCallConfigurationService`
5. **统一 Provider 适配细节**：核对自定义后端和 OpenAI 兼容后端的消息转换与 `ChatOptions` 映射
6. **执行回归验证**：覆盖 Chat、Ask、Wiki 生成与 Tool Call 开关场景

### 回滚策略

- 保留字符串 Prompt 包装入口，在消息构建切换初期作为短期回退手段
- 若某 Provider 在多角色消息链路上出现异常，可临时仅对该 Provider 走兼容转换，但不得恢复 `ChatClientFactory`
- 文档与规格更新不回滚；若代码回退，也应同步修正文档说明

## Validation

- `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj` 通过
- `dotnet test backend/Heimdall.Tests/Heimdall.Tests.csproj` 通过
- 手工验证 `POST /chat/completions/stream`：保留历史对话，不再只取最后一条用户消息
- 手工验证 `POST /tasks/ask/stream`：历史轮次与版本证据以多消息形式参与生成
- 手工验证 Wiki 结构规划与页面生成：Tool Call 开关仍可正常控制 Stage 3 / Stage 5
- 检查仓库文档与注释：不再把 `pgvector`、向量召回或 10 阶段写成当前已落地能力

## Open Questions

1. 多角色消息中“版本证据包”更适合作为单独 `User` 消息，还是拆为多条 `User` 补充消息，需要结合目标模型实测
2. 是否需要为消息构建器新增单元测试样例，覆盖历史轮次、空 history、带 file focus、deepResearch 等典型组合
3. `ChatClientFactory` 是直接删除文件，还是保留空壳并在编译期报错，需要结合当前引用清理节奏决定
