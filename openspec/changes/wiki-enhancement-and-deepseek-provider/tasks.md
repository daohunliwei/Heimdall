## 1. DeepSeek Provider 实现

- [ ] 1.1 创建 `DeepSeekChatProvider.cs`，实现 `IChatProvider` 接口，支持 reasoning_content 和 thinking 配置
- [ ] 1.2 实现非流式 `GenerateAsync` 方法，请求体包含 `thinking` 节点和 `max_tokens`
- [ ] 1.3 实现流式 `GenerateWithMetricsAsync` 方法，解析 SSE 流中 delta.content 和 delta.reasoning_content
- [ ] 1.4 在 `ProviderRegistry.cs` 中注册 DeepSeek Provider，从 `generator.json` 读取配置
- [ ] 1.5 在 `generator.json` 中添加 `providers.deepseek` 配置段（ApiBase、默认模型列表）
- [ ] 1.6 配置 DeepSeek 模型默认元数据（MaxContextTokens=1048576, MaxOutputTokens=384000, ContextFillRatio=0.85）

## 2. 模型上下文窗口与输出长度分离

- [ ] 2.1 修改 `TaskPromptService` 的 prompt 构建逻辑，使用 MaxContextTokens * ContextFillRatio 计算输入预算
- [ ] 2.2 修改 Pipeline Stage 5（页面生成）的 Provider 调用，传入 MaxOutputTokens 作为 max_tokens 参数
- [ ] 2.3 修改 Pipeline Stage 4（结构规划）的 Provider 调用，传入 MaxOutputTokens 作为 max_tokens 参数
- [ ] 2.4 更新 ContextWarningThreshold 触发后的截断策略：按跨页面上下文 → 仓库文档 → 低相关代码片段顺序裁剪

## 3. Wiki 目录层级结构修复

- [ ] 3.1 在结构规划 JSON 解析器中增加 `ValidateAndFixHierarchy` 方法，验证 parentId 引用有效性和层级完整性
- [ ] 3.2 修改页面生成排序逻辑，改为 BFS 树形拓扑序遍历（根节点 → 逐层展开），确保父页面先于子页面生成
- [ ] 3.3 修复前端 Wiki 树形组件，根据 parentId 构建嵌套树数据结构并递归渲染
- [ ] 3.4 树形组件支持节点展开/折叠交互和当前页面自动展开祖先路径

## 4. 仓库阅读性文档注入

- [ ] 4.1 在 `WikiTaskService` 的 Stage 2 中新增 `CollectRepositoryDocuments` 方法，扫描并收集仓库根目录及 docs/、.github/ 下的 .md 文件
- [ ] 4.2 实现文档优先级排序（AGENTS.md > CLAUDE.md > README.md > 其他）和超预算裁剪逻辑
- [ ] 4.3 修改 Stage 4 结构规划提示词构建，注入仓库文档内容作为架构参考
- [ ] 4.4 修改 Stage 5 页面生成提示词构建，根据页面主题有选择地注入相关文档内容

## 5. 集成验证与调试

- [ ] 5.1 使用 `调试环境-Home.md` 中的 DeepSeek 配置启动后端服务，验证 DeepSeekChatProvider 非流式调用
- [ ] 5.2 验证 DeepSeekChatProvider 流式调用和 reasoning_content 收集
- [ ] 5.3 对 libgit2sharp 仓库触发完整 Wiki 生成，验证目录树是否为多层嵌套结构（非平铺）
- [ ] 5.4 验证生成的 Wiki 页面内容是否引用了 AGENTS.md/README.md 中的架构信息
- [ ] 5.5 验证模型输入/输出分离后大窗口模型是否充分填充上下文并正确设置 max_tokens
