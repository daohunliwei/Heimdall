# Heimdall.Backend 后端架构设计文档

## 1. 设计目标与理念

### 1.1 核心设计目标

Heimdall 是一个 AI 驱动的代码仓库知识库自动生成系统，其后端承担着将任意代码仓库（GitHub/GitLab/Bitbucket/本地）转化为结构化 Wiki 文档、交互式问答、演示幻灯片以及工作坊培训材料的核心职责。

| 目标 | 描述 |
|------|------|
| **多 Provider 可插拔** | 支持 OpenAI、Google Gemini、Azure OpenAI、AWS Bedrock、Ollama、MiniMax、DashScope、OpenRouter 等 8 种 LLM Provider 的热切换 |
| **RAG 增强生成** | 基于向量嵌入的代码检索增强生成，实现深层次代码理解 |
| **多任务流水线** | 统一的任务编排框架，支持 Wiki 生成、深度问答、幻灯片生成、工作坊生成四种任务类型 |
| **容错与降级** | Wiki 结构解析失败时降级为目录树结构；页面生成失败时生成占位内容；RAG 上下文过大时回退至无上下文模式 |
| **缓存策略** | 两层文件缓存 —— Wiki 内容缓存与向量嵌入索引缓存，避免重复计算 |
| **异步解耦** | SSE 流式响应，前端断连不中断后端任务执行 |

### 1.2 架构设计理念

- **管道（Pipeline）模式**：Chat 流程遵循 验证 → 上下文构建 → Prompt 构造 → Provider 解析 → LLM 调用 → 响应处理 的清晰管道
- **策略（Strategy）模式**：通过 `IChatProvider` / `IEmbeddingProvider` 接口实现多 Provider 可替换
- **适配器（Adapter）模式**：`OpenAiCompatibleChatProvider` 以单一实现适配 OpenAI / OpenRouter / DashScope 三种服务
- **生命周期感知**：Wiki 任务使用 `IHostApplicationLifetime` 感知应用关闭，使用链接的 `CancellationToken` 合并用户取消、超时、关闭信号

---

## 2. 架构全景图

```plantuml
@startuml
!theme plain
title Heimdall.Backend 架构全景图

package "客户端层" {
  [Next.js 前端] as FE
  [外部 API 调用者] as EXT
}

package "ASP.NET Core 管道" {
  [CORS 中间件]
  [路由中间件]
  [控制器路由] as Router
}

package "控制器层 (Controllers)" as Controllers {
  [SystemController] as SC
  [ConfigurationController] as CC
  [ChatController] as CHC
  [RepositoryController] as RC
  [ExportController] as EC
  [WikiCacheController] as WCC
  [ProjectsController] as PC
  [TasksController] as TC
}

package "服务层 (Services)" {
  package "Chat & Streaming" {
    [ChatOrchestratorService] as COS
    [ChatStreamService] as CSS
    [PromptTemplateService] as PTS
  }

  package "RAG 管道" {
    [RagContextService] as RCS
    [RepositoryEmbeddingService] as RES
    [ConversationMemoryService] as CMS
  }

  package "任务编排" {
    [WikiTaskService] as WTS
    [AskTaskService] as ATS
    [SlidesTaskService] as STS
    [WorkshopTaskService] as WKTS
    [TaskLlmService] as TLS
    [TaskPromptService] as TPS
    [TaskRequestUtilityService] as TRUS
    [WikiMarkdownNormalizer] as WMN
  }

  package "Provider 层" {
    [ProviderRegistry] as PR
    interface IChatProvider
    interface IEmbeddingProvider
  }

  package "基础服务" {
    [HeimdallConfigService] as HCS
    [RepositoryAccessService] as RAS
    [AuthorizationService] as AS
    [WikiCacheService] as WCS
    [WikiExportService] as WES
    [ProcessedProjectService] as PPS
    [SystemInfoService] as SIS
    [TextUtilityService] as TUS
  }
}

package "Provider 实现" {
  [GoogleChatProvider] as GCP
  [MiniMaxChatProvider] as MCP
  [OpenAiCompatibleChatProvider] as OCP
  [OllamaChatProvider] as OLCP
  [AzureChatProvider] as ACP
  [BedrockChatProvider] as BCP
  [OpenAiEmbeddingProvider] as OEP
  [GoogleEmbeddingProvider] as GEP
  [OllamaEmbeddingProvider] as OLEP
  [BedrockEmbeddingProvider] as BEP
}

package "外部服务" {
  [GitHub / GitLab / Bitbucket API] as GIT
  [OpenAI API] as OAI
  [Google Gemini API] as GEM
  [Azure OpenAI] as AZR
  [AWS Bedrock] as AWS
  [Ollama Local] as OLL
  [MiniMax API] as MM
  [DashScope API] as DS
  [OpenRouter API] as OR
}

package "存储层" {
  database "文件系统" {
    [Wiki 缓存 (JSON)] as WCACHE
    [嵌入索引 (JSON)] as ECACHE
    [配置文件 (JSON)] as CONFIG
    [Git 仓库克隆] as REPOS
  }
}

FE --> Router : HTTP/SSE
EXT --> Router : HTTP
Router --> Controllers

SC ..> SIS
CC ..> HCS
CC ..> AS
CHC ..> COS
CHC ..> CSS
RC ..> RAS
EC ..> WES
WCC ..> WCS
WCC ..> AS
PC ..> PPS
TC --> WTS
TC --> ATS
TC --> STS
TC --> WKTS

COS --> HCS
COS --> PTS
COS --> PR
COS --> RCS
COS --> TUS
CSS --> TUS
RCS --> RES
RCS --> RAS
RCS --> PR
RCS --> TUS
RES --> RAS
RES --> PR
RES --> HCS
RES --> TUS
WTS --> COS
WTS --> HCS
WTS --> RAS
WTS --> TLS
WTS --> TPS
WTS --> TRUS
WTS --> WCS
ATS --> COS
ATS --> TRUS
STS --> TLS
STS --> TPS
STS --> TRUS
STS --> WTS
WKTS --> TLS
WKTS --> TPS
WKTS --> TRUS
WKTS --> WTS
TLS --> PR
TLS --> TRUS
PR --> IChatProvider
PR --> IEmbeddingProvider

GCP --> GEM
MCP --> MM
OCP --> OAI
OCP --> OR
OCP --> DS
OLCP --> OLL
ACP --> AZR
BCP --> AWS
OEP --> OAI
GEP --> GEM
OLEP --> OLL
BEP --> AWS
RAS --> GIT
RAS --> REPOS
WCS --> WCACHE
RES --> ECACHE
HCS --> CONFIG

@enduml
```

---

## 3. 领域模型图

```plantuml
@startuml
!theme plain
title Heimdall.Backend 核心领域模型

class RepoInfo {
  + owner: string
  + repo: string
  + type: string
  + repoUrl: string
  + token: string
  + localPath: string
  --
  描述代码仓库的身份信息
}

class WikiStructure {
  + id: string
  + title: string
  + description: string
  + pages: WikiPage[]
  + sections: WikiSection[]
  + rootSections: WikiSection[]
  --
  LLM 生成的 Wiki 完整结构
}

class WikiPage {
  + id: string
  + title: string
  + content: string
  + filePaths: string[]
  + importance: string
  + relatedPages: string[]
  + parentId: string
  + isSection: bool
  + children: string[]
  --
  Wiki 中单个页面的完整定义
}

class WikiSection {
  + id: string
  + title: string
  + pages: string[]
  + subsections: string[]
  --
  Wiki 页面分组层级结构
}

class WikiCacheData {
  + wikiStructure: WikiStructure
  + generatedPages: Dictionary
  + repoUrl: string
  + repo: string
  + provider: string
  + model: string
  + language: string
  --
  持久化到磁盘的完整缓存
}

class EmbeddedDocument {
  + id: string
  + text: string
  + filePath: string
  + fileType: string
  + isCode: bool
  + isImplementation: bool
  + tokenCount: int
  + vector: float[]
  --
  带有嵌入向量的代码/文档片段
}

class RepositoryIndexCache {
  + repository: string
  + embedderType: string
  + filterSignature: string
  + documents: EmbeddedDocument[]
  --
  整个仓库的向量索引缓存
}

class ChatCompletionRequest {
  + messages: ChatMessage[]
  + repo: string
  + language: string
  + deepResearch: bool
  + filePath: string
  --
  前端发起的聊天请求
}

class TaskRequestBase {
  + repoUrl: string
  + owner: string
  + repo: string
  + type: string
  + token: string
  + provider: string
  + model: string
  + language: string
  + excludedDirs: string[]
  + excludedFiles: string[]
  + includedDirs: string[]
  + includedFiles: string[]
  --
  所有任务请求的基类
}

class WikiTaskRequest {
  + comprehensive: bool
  + forceRefresh: bool
}
class AskTaskRequest {
  + question: string
  + history: DialogTurn[]
  + deepResearch: bool
  + filePath: string
}
class SlidesTaskRequest {
  + forceRefresh: bool
  + comprehensive: bool
}
class WorkshopTaskRequest {
  + forceRefresh: bool
  + comprehensive: bool
}

RepoInfo "1" -- "1" WikiCacheData
WikiCacheData "1" -- "1" WikiStructure
WikiStructure "1" -- "*" WikiPage
WikiStructure "1" -- "*" WikiSection
WikiSection "1" -- "*" WikiSection : subsections
WikiSection "1" -- "*" WikiPage : pages
RepositoryIndexCache "1" -- "*" EmbeddedDocument
RepoInfo "1" -- "1" RepositoryIndexCache
TaskRequestBase <|-- WikiTaskRequest
TaskRequestBase <|-- AskTaskRequest
TaskRequestBase <|-- SlidesTaskRequest
TaskRequestBase <|-- WorkshopTaskRequest
TaskRequestBase "1" -- "1" RepoInfo : 构建自

@enduml
```

---

## 4. 领域调用关系图

```plantuml
@startuml
!theme plain
title Heimdall.Backend 服务依赖与调用关系

skinparam componentStyle rectangle

[Program.cs (DI 容器)] as DI

package "配置层" {
  [HeimdallConfigService] as HCS
}

package "工具层" {
  [TextUtilityService] as TUS
  [PromptTemplateService] as PTS
  [WikiMarkdownNormalizer] as WMN
}

package "认证层" {
  [AuthorizationService] as AS
}

package "系统信息" {
  [SystemInfoService] as SIS
}

package "缓存层" {
  [WikiCacheService] as WCS
  [ProcessedProjectService] as PPS
}

package "导出层" {
  [WikiExportService] as WES
}

package "仓库访问层" {
  [RepositoryAccessService] as RAS
}

package "Provider 层" {
  [ProviderRegistry] as PR
  [<< interface >> IChatProvider] as ICP
  [<< interface >> IEmbeddingProvider] as IEP
}

package "RAG 层" {
  [ConversationMemoryService] as CMS
  [RepositoryEmbeddingService] as RES
  [RagContextService] as RCS
}

package "Chat 层" {
  [ChatStreamService] as CSS
  [ChatOrchestratorService] as COS
}

package "任务编排层" {
  [TaskRequestUtilityService] as TRUS
  [TaskPromptService] as TPS
  [TaskLlmService] as TLS
  [WikiTaskService] as WTS
  [AskTaskService] as ATS
  [SlidesTaskService] as STS
  [WorkshopTaskService] as WKTS
}

DI --> HCS
DI --> TUS
DI --> PTS
DI --> AS
DI --> SIS
DI --> WCS
DI --> WES
DI --> RAS
DI --> PR
DI --> RES
DI --> RCS
DI --> CSS
DI --> COS
DI --> TRUS
DI --> TPS
DI --> TLS
DI --> WTS
DI --> ATS
DI --> STS
DI --> WKTS
DI --> ICP
DI --> IEP

WCS --> HCS
PPS --> WCS
WES --> TUS
RAS --> HCS
RAS --> TUS
PR --> HCS
PR --> ICP
PR --> IEP
RES --> RAS
RES --> PR
RES --> HCS
RES --> TUS
RCS --> RES
RCS --> RAS
RCS --> PR
RCS --> TUS
COS --> HCS
COS --> PTS
COS --> PR
COS --> RCS
COS --> TUS
CSS --> TUS
TRUS --> HCS
TRUS --> TUS
TLS --> PR
TLS --> TRUS
WTS --> COS
WTS --> HCS
WTS --> RAS
WTS --> TLS
WTS --> TPS
WTS --> TRUS
WTS --> WCS
ATS --> COS
ATS --> TRUS
STS --> TLS
STS --> TPS
STS --> TRUS
STS --> WTS
WKTS --> TLS
WKTS --> TPS
WKTS --> TRUS
WKTS --> WTS

@enduml
```

---

## 5. Wiki 生成完整流水线

### 5.1 流程图

```plantuml
@startuml
!theme plain
title Wiki 生成任务完整流水线

|前端|
start
:POST /tasks/wiki;
|后端|
:TaskController 接收请求;
:TaskRequestUtilityService 解析 RepoInfo;

|WikiTaskService|
if (缓存命中 && !ForceRefresh?) then (是)
  :直接返回缓存;
  stop
else (否)
endif

:创建链接 CancellationToken\n(用户取消 + 超时 + 应用关闭);
:RepositoryAccessService\n准备仓库 (git clone / 本地);

partition "阶段一：生成 Wiki 结构" {
  :TaskPromptService\n构建结构分析 Prompt\n(文件树 + README + 语言);
  :TaskLlmService\n调用 LLM 返回 XML 结构;

  :解析 XML 结构;
  if (XML 解析成功?) then (是)
    :构建 WikiStructure 对象;
  else (否)
    :降级：从目录树构建结构;
  endif
}

partition "阶段二：逐页生成内容" {
  while (还有未生成的页面?) is (是)
    :TaskPromptService\n构建页面生成 Prompt\n(相关页面 + 源文件 + 语言);
    :ChatOrchestratorService\n调用 LLM 生成页面内容;
    :WikiMarkdownNormalizer\n清洗与规范化 Markdown;
    if (生成成功?) then (是)
      :存入 generatedPages;
    else (否)
      :生成错误占位页面;
    endif
  endwhile (否)
}

:WikiCacheService 写入缓存;
:构建 WikiTaskResponse 返回;

|前端|
:接收并渲染 Wiki 页面;
stop

@enduml
```

### 5.2 时序图

```plantuml
@startuml
!theme plain
title Wiki 生成完整时序图

actor 用户 as User
participant "Next.js\n前端" as FE
participant "TasksController" as TC
participant "WikiTaskService" as WTS
participant "TaskRequestUtilityService" as TRUS
participant "RepositoryAccessService" as RAS
participant "TaskPromptService" as TPS
participant "TaskLlmService" as TLS
participant "ChatOrchestratorService" as COS
participant "WikiMarkdownNormalizer" as WMN
participant "WikiCacheService" as WCS
participant "LLM Provider" as LLM
database "文件系统" as FS

User -> FE : 输入仓库 URL 点击生成
FE -> TC : POST /tasks/wiki
TC -> WTS : GenerateWikiAsync(request)
WTS -> TRUS : BuildRepoInfo(request)
TRUS --> WTS : RepoInfo

WTS -> WCS : GetWikiCache(repoInfo)
WCS -> FS : 检查缓存文件
FS --> WCS : 缓存不存在
WCS --> WTS : null (需要生成)

WTS -> RAS : PrepareRepositoryAsync(repoInfo)
RAS -> FS : git clone --depth=1
FS --> RAS : 仓库就绪

== 阶段一：生成 Wiki 结构 ==

WTS -> TPS : BuildWikiStructurePromptAsync(fileTree, readme, language)
TPS --> WTS : 完整 Prompt (XML 格式要求)

WTS -> TLS : GenerateAsync(prompt)
TLS -> LLM : HTTP Request (generateContent)
LLM --> TLS : XML 结构响应
TLS --> WTS : 原始 XML 文本

WTS -> WTS : 解析 XML → WikiStructure
note right of WTS : 若解析失败\n降级为目录树结构

== 阶段二：逐页生成内容 ==

loop 每个 WikiPage
  WTS -> COS : GenerateAsync(pagePrompt)
  COS -> COS : 构建 RAG 上下文
  COS -> LLM : 带上下文的 LLM 请求
  LLM --> COS : 页面 Markdown 内容
  COS --> WTS : 生成的页面内容

  WTS -> WMN : Normalize(markdown)
  WMN --> WTS : 清洗后的 Markdown
end

WTS -> WCS : SaveWikiCache(repoInfo, cacheData)
WCS -> FS : 写入 JSON 缓存文件

WTS --> TC : WikiTaskResponse
TC --> FE : JSON 响应 (含结构 + 所有页面)
FE -> FE : 渲染 Wiki 页面
FE --> User : 展示 Wiki 文档

@enduml
```

---

## 6. Chat 交互与 SSE 流式响应

### 6.1 流程图

```plantuml
@startuml
!theme plain
title Chat 管道处理流程

|Controller|
start
:POST /chat/completions/stream;
:ChatController 接收 ChatCompletionRequest;

|ChatOrchestratorService|
:验证请求 (消息列表非空，最后一条为用户消息);

:构建 ConversationMemory\n从消息历史提取对话轮次;

partition "RAG 上下文构建" {
  :检查输入大小;
  if (输入 > 8000 tokens?) then (是)
    :跳过检索，使用空上下文;
  else (否)
    :RagContextService 构建检索上下文;
    :读取当前文件内容;
    :RepositoryEmbeddingService\n准备嵌入文档;
    :嵌入用户查询;
    :余弦相似度排序\n(实现文件 +0.25 加分);
    :按文件路径分组;
    :取 Top-20 相关片段;
  endif
}

:构建复合 Prompt XML\n(系统指令 + 对话历史 + 文件内容 + 上下文 + 查询);

:ProviderRegistry 解析 Provider;

if (DeepResearch?) then (是)
  :追踪迭代计数;
  :选择对应阶段的 SystemPrompt;
  :标记 [DEEP RESEARCH];
else (否)
  :使用标准 SystemPrompt;
endif

:调用 provider.GenerateAsync();

if (上下文过大导致 Token 溢出?) then (是)
  :回退：清空上下文重试;
endif

|ChatStreamService|
:设置 SSE 响应头;
:文本分割为 160 字符块;
:逐块写入 data: ...\n\n;
:写入 data: [DONE];

stop

@enduml
```

### 6.2 时序图

```plantuml
@startuml
!theme plain
title Chat SSE 流式交互时序图

actor 用户 as User
participant "Ask 组件" as Ask
participant "Next.js\nAPI Route" as NextAPI
participant "ChatController" as CC
participant "ChatOrchestratorService" as COS
participant "ConversationMemoryService" as CMS
participant "RagContextService" as RCS
participant "RepositoryEmbeddingService" as RES
participant "ProviderRegistry" as PR
participant "IChatProvider" as CP
participant "ChatStreamService" as CSS
participant "LLM API" as LLM

User -> Ask : 输入问题
Ask -> NextAPI : POST /api/chat/stream\n(ChatCompletionRequest)
NextAPI -> CC : 代理转发
CC -> COS : GenerateAsync(request)

COS -> COS : 验证请求合法性

COS -> CMS : HydrateFromMessages(messages)
CMS --> COS : 对话轮次列表

COS -> RCS : BuildContextAsync(query, repoInfo, filePath)
RCS -> RES : PrepareEmbeddedDocumentsAsync()
RES -> RES : 检查嵌入缓存 / 重新嵌入
RCS -> RES : EmbedAsync(userQuery)
RCS -> RCS : 余弦相似度排序\nTop-20 结果
RCS --> COS : ChatContextResult

COS -> COS : 构建完整 Prompt XML

COS -> PR : ResolveChatProvider(providerId)
PR --> COS : IChatProvider 实例

COS -> CP : GenerateAsync(providerRequest, ct)
CP -> LLM : HTTP POST (含完整 Prompt)
LLM --> CP : 流式/完整响应
CP --> COS : 生成的文本

COS --> CC : 完整响应文本
CC -> CSS : StreamAsync(response, httpContext)
CSS -> CSS : 分割为 160 字符块

loop 每个文本块
  CSS -> NextAPI : SSE: data: \<chunk\>\n\n
  NextAPI -> Ask : 流式接收
  Ask -> Ask : 增量渲染 Markdown
end

CSS -> NextAPI : SSE: data: [DONE]\n\n
NextAPI -> Ask : 流结束
Ask --> User : 完整回答展示

@enduml
```

---

## 7. RAG（检索增强生成）流水线

```plantuml
@startuml
!theme plain
title RAG 嵌入与检索流水线

|RepositoryEmbeddingService|
start
:接收 RepoInfo;

if (嵌入缓存存在?) then (是)
  :从缓存加载 RepositoryIndexCache;
  :验证嵌入维度一致性;
  stop
else (否)
endif

:RepositoryAccessService\n准备仓库 (git clone);
:RepositoryAccessService\n读取仓库文档;
:按扩展名过滤文件\n(代码 .cs/.ts/.py 等，文档 .md/.txt 等);

partition "文本分割" {
  :按配置的切分策略切分\n(word/char/line);
  :代码文件: max 80K tokens\n文档文件: max 8K tokens;
  :TextSplitterDefinition\n(chunkSize=350, chunkOverlap=100);
}

partition "批量嵌入" {
  if (Ollama?) then (是)
    :逐条顺序嵌入 (Ollama 限制);
  else (否)
    :批量并行嵌入;
  endif
}

:验证向量维度一致性;
:构建 RepositoryIndexCache;
:写入缓存文件 (JSON);

|RagContextService|
:嵌入用户查询;

:计算余弦相似度;
:实现文件加权 (+0.25);

:按文件路径分组去重;
:取 Top-K 结果 (K=20);
:构建结构化上下文 XML;

stop

@enduml
```

---

## 8. Provider 可插拔架构

```plantuml
@startuml
!theme plain
title Provider 策略模式架构

interface IChatProvider {
  + {abstract} ProviderId: string
  + {abstract} GenerateAsync(ProviderChatRequest, CancellationToken): Task<string>
}

interface IEmbeddingProvider {
  + {abstract} EmbedderType: string
  + {abstract} EmbedAsync(string, CancellationToken): Task<float[]>
  + {abstract} EmbedBatchAsync(IReadOnlyList<string>, CancellationToken): Task<List<float[]>>
}

class GoogleChatProvider {
  ProviderId: "google"
  API: Gemini v1beta
  Model: gemini-2.5-flash / gemini-2.5-pro
}
class MiniMaxChatProvider {
  ProviderId: "minimax"
  API: MiniMax Chat Completions
  Model: MiniMax-Text-01 / abab7-chat
}
class OpenAiCompatibleChatProvider {
  ProviderId: "openai" / "openrouter" / "dashscope"
  API: OpenAI-compatible
  Model: 按配置选择
}
class OllamaChatProvider {
  ProviderId: "ollama"
  API: Ollama Local
  Model: 按配置选择
}
class AzureChatProvider {
  ProviderId: "azure"
  API: Azure OpenAI
  Model: 按部署名选择
}
class BedrockChatProvider {
  ProviderId: "bedrock"
  API: AWS Bedrock Runtime
  Model: Claude / Titan / Cohere / AI21
}

class OpenAiEmbeddingProvider {
  EmbedderType: "openai"
  Model: text-embedding-3-small (256d)
}
class GoogleEmbeddingProvider {
  EmbedderType: "google"
  Model: gemini-embedding-001
}
class OllamaEmbeddingProvider {
  EmbedderType: "ollama"
  Model: 按配置选择
}
class BedrockEmbeddingProvider {
  EmbedderType: "bedrock"
  Model: amazon.titan-embed-text-v2:0 (256d)
}

class ProviderRegistry {
  - _chatProviders: IEnumerable<IChatProvider>
  - _embeddingProviders: IEnumerable<IEmbeddingProvider>
  + ResolveChatProvider(providerId): IChatProvider
  + ResolveEmbeddingProvider(embedderType): IEmbeddingProvider
}

class HeimdallConfigService {
  + GetDefaultProvider(): string
  + GetEmbedderType(): string
  + GetProviderConfig(providerId): ProviderDefinition
}

IChatProvider <|.. GoogleChatProvider
IChatProvider <|.. MiniMaxChatProvider
IChatProvider <|.. OpenAiCompatibleChatProvider
IChatProvider <|.. OllamaChatProvider
IChatProvider <|.. AzureChatProvider
IChatProvider <|.. BedrockChatProvider
IEmbeddingProvider <|.. OpenAiEmbeddingProvider
IEmbeddingProvider <|.. GoogleEmbeddingProvider
IEmbeddingProvider <|.. OllamaEmbeddingProvider
IEmbeddingProvider <|.. BedrockEmbeddingProvider
ProviderRegistry --> IChatProvider : IEnumerable 注入
ProviderRegistry --> IEmbeddingProvider : IEnumerable 注入
ProviderRegistry --> HeimdallConfigService : 查找配置

@enduml
```

---

## 9. 配置系统架构

```plantuml
@startuml
!theme plain
title 分层配置系统

package "配置来源 (优先级从高到低)" {
  [命令行参数] as CLI
  [环境变量] as ENV
  [运行时配置文件\n(HEIMDALL_RUNTIME_CONFIG_PATH)] as RT_CFG
  [appsettings.json] as APP_CFG
}

package "JSON 配置文件" {
  file "config/generator.json" as GEN_CFG {
    [LLM Provider 定义\n(defaultProvider, providers{})]
  }
  file "config/embedder.json" as EMB_CFG {
    [嵌入器 & 检索器配置\n(embedder, retriever, textSplitter)]
  }
  file "config/lang.json" as LANG_CFG {
    [支持语言列表\n(supportedLanguages{}, default)]
  }
  file "config/repo.json" as REPO_CFG {
    [仓库过滤规则\n(fileFilters, maxSizeMb)]
  }
}

package "HeimdallConfigService" {
  [加载 JSON 配置]
  [环境变量占位符替换\n(${VAR_NAME})]
  [Provider/Model 解析]
  [超时配置获取]
  [ModelConfigResponse 构建]
}

package "环境变量 (关键)" {
  [HEIMDALL_CONFIG_DIR]
  [HEIMDALL_DATA_DIR]
  [HEIMDALL_STORAGE_DIR]
  [HEIMDALL_AUTH_MODE]
  [HEIMDALL_AUTH_CODE]
  [HEIMDALL_DEFAULT_PROVIDER]
  [HEIMDALL_EMBEDDER_TYPE]
  [HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES]
  [API Keys (多 Provider)]
}

CLI --> ENV
ENV --> RT_CFG
RT_CFG --> APP_CFG
HeimdallConfigService --> GEN_CFG
HeimdallConfigService --> EMB_CFG
HeimdallConfigService --> LANG_CFG
HeimdallConfigService --> REPO_CFG
HeimdallConfigService --> ENV

@enduml
```

---

## 10. 部署架构

```plantuml
@startuml
!theme plain
title Docker 部署架构

node "Docker Host" {
  package "heimdall-frontend (Next.js)" {
    [Next.js Server\n(端口 3000)] as NEXT
    [API Routes\n(代理层)]
    [SSR / Static Pages]
  }

  package "heimdall-api (.NET 10)" as heimdall_api {
    [ASP.NET Core\n(端口 8001)]
    [Controllers]
    [Services]
    [Providers]
  }

  database "数据卷" as DataVolumes {
    [heimdall-data\n(Wiki 缓存)]
    [heimdall-storage\n(仓库克隆 + 嵌入缓存)]
  }

  NEXT --> heimdall_api : HTTP (内部网络)
}

cloud "外部 AI 服务" as ExternalAI {
  [OpenAI]
  [Google Gemini]
  [AWS Bedrock]
  [Azure OpenAI]
  [MiniMax]
  [Ollama Local]
}

heimdall_api --> ExternalAI : HTTPS

@enduml
```

---

## 11. 控制器与 API 端点总览

| 方法 | 路由 | 控制器 | 描述 |
|------|------|--------|------|
| `GET` | `/` | SystemController | 应用信息与端点列表 |
| `GET` | `/health` | SystemController | 健康检查 |
| `GET` | `/lang/config` | ConfigurationController | 支持的语言列表 |
| `GET` | `/auth/status` | ConfigurationController | 认证状态查询 |
| `POST` | `/auth/validate` | ConfigurationController | 验证授权码 |
| `GET` | `/models/config` | ConfigurationController | 可用 Provider 与模型列表 |
| `POST` | `/chat/completions/stream` | ChatController | SSE 流式聊天补全 |
| `GET` | `/local_repo/structure` | RepositoryController | 本地目录结构与 README |
| `POST` | `/export/wiki` | ExportController | 导出 Wiki (Markdown/JSON) |
| `GET` | `/api/wiki_cache` | WikiCacheController | 获取缓存 Wiki |
| `POST` | `/api/wiki_cache` | WikiCacheController | 保存缓存 Wiki |
| `DELETE` | `/api/wiki_cache` | WikiCacheController | 删除缓存 (需认证) |
| `GET` | `/api/processed_projects` | ProjectsController | 已处理项目列表 |
| `POST` | `/tasks/wiki` | TasksController | 生成 Wiki |
| `POST` | `/tasks/ask` | TasksController | 问答 (支持 DeepResearch) |
| `POST` | `/tasks/slides` | TasksController | 生成幻灯片 |
| `POST` | `/tasks/workshop` | TasksController | 生成工作坊 |

---

## 12. 关键设计决策

### 12.1 全单例服务注册
所有服务注册为 **Singleton**，避免复杂的生命周期管理。Provider 通过 `IEnumerable<T>` 多实现注入。

### 12.2 Wiki 生成双阶段架构
Wiki 生成分为两个独立阶段：**结构规划**（LLM 生成 XML 结构）→ **逐页生成**（每页独立调用 ChatOrchestratorService），确保每页内容质量且支持独立失败降级。

### 12.3 XML 中间格式
Wiki 结构使用 XML 作为 LLM 与后端之间的中间格式，利用 `XDocument` 进行严格解析，同时内置常见 XML 错误的修复逻辑（如 `<parent_section>` 标签未闭合修复）。

### 12.4 前端断连不中断任务
Wiki 任务通过 `IHostApplicationLifetime` 感知进程关闭，但不因 HTTP 请求取消而中断 —— 即使用户关闭浏览器，Wiki 生成继续进行，结果写入缓存供下次访问。

### 12.5 文件级 JSON 缓存
缓存采用文件级 JSON 而非数据库，支持跨会话持久化，便于在 Docker 卷中挂载，无需额外的数据库依赖。

### 12.6 Markdown 规范化器
`WikiMarkdownNormalizer` 专门处理 LLM 输出中的常见问题：`<think>` 标签残留（推理模型）、外层 ```markdown 包裹、裸代码块修复、XML 标签转义。
