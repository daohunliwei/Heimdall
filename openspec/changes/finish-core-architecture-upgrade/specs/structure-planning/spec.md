## MODIFIED Requirements

### Requirement: AST L1 数据注入结构规划提示词
结构规划提示词 SHALL 从 DB 加载基础模板后，注入 AST L1 层数据：(1) 类型层级图——每个模块的关键类、继承链、接口实现、公开方法签名；(2) 调用拓扑——AST 提取的调用者和被调用者关系，以可读的"A → B → C"格式呈现；(3) 设计模式证据——AST 检测到的模式、参与类和置信度。格式为结构化 Markdown，便于 LLM 解析和引用。注入量受 ContextPackingService 预算约束。

#### Scenario: L1 数据完整注入
- **WHEN** 仓库有 50 个关键类，AST 提取了完整的类型层级和调用拓扑
- **THEN** 提示词 User 消息包含所有关键类的"类型层级"段和"调用拓扑"段，按模块分组
- **AND** 超出 ContextPackingService 预算时低优先级类被裁剪

#### Scenario: L1 数据缺失时降级
- **WHEN** 仓库文件语言无 tree-sitter 语法支持，AST 数据不可用
- **THEN** 提示词仅包含文件树和 README 等可用上下文，明确标注"AST 分析不可用"

### Requirement: 三种可配置的结构规划策略（AST 增强）
LlmJson 策略 SHALL 接收上述 AST L1 数据作为提示词上下文。Deterministic 策略 SHALL 使用 AST 类型层级和调用拓扑优化模块分组和页面聚合。LlmEnhanced 策略 SHALL 在算法骨架生成后，为 LLM 润色阶段注入 AST L1 数据以生成更准确的 title/description。策略配置方式不变。

#### Scenario: Deterministic 策略利用 AST 拓扑优化分组
- **WHEN** AST 调用拓扑显示 ControllerA/ControllerB/ControllerC 三者的被调用方法高度重叠
- **THEN** Deterministic 策略将三者合并为同一 Section，而非按目录分别创建
