# Heimdall.Frontend 前端架构设计文档

## 1. 设计目标与理念

### 1.1 核心设计目标

Heimdall 前端是一个基于 Next.js 16 App Router 的单页应用，提供 AI 驱动的代码仓库知识库的可视化交互界面。

| 目标 | 描述 |
|------|------|
| **极致简约的交互** | 用户仅需输入仓库 URL 即可一键完成 Wiki 生成、问答、幻灯片、工作坊的完整流程 |
| **流式响应体验** | 通过 SSE 流式传输聊天补全，实现打字机效果的实时内容输出 |
| **丰富的可视化** | 集成 Mermaid 图表渲染、代码语法高亮、数学公式（KaTeX）、Markdown 富文本呈现 |
| **暗色/亮色主题** | 基于 `next-themes` 的主题系统，使用 CSS 自定义属性实现全组件主题切换 |
| **配置持久化** | 基于 URL 的仓库配置 localStorage 缓存，用户再次访问同一仓库时自动恢复配置 |
| **BFF 代理模式** | Next.js API Routes 作为 BFF（Backend For Frontend）层，代理转发请求至 .NET 后端 |

### 1.2 架构设计理念

- **薄前端、厚后端**：前端负责 UI 呈现与用户交互，业务逻辑（Wiki 生成、RAG 检索、LLM 调用）全部在后端完成
- **配置驱动路由**：所有配置（Provider、Model、语言、文件过滤）通过 URL Query 参数在页面间传递，确保可分享、可书签
- **组件局部状态**：采用 React 原生 `useState` 管理组件状态，无全局状态库（无 Redux/Zustand），保持架构简单
- **渐进式加载**：Wiki 页面先尝试缓存，缓存未命中才触发生成任务；幻灯片/工作坊在页面挂载时自动触发

---

## 2. 架构全景图

```plantuml
@startuml
!theme plain
title Heimdall.Frontend 架构全景图

package "用户浏览器" {
  [用户界面] as UI
}

package "Next.js 前端 (端口 3000)" {
  package "页面层 (App Router)" as Pages {
    [Home Page\n/] as Home
    component "Repo Wiki Page\n/[owner]/[repo]" as Wiki
    component "Slides Page\n/[owner]/[repo]/slides" as Slides
    component "Workshop Page\n/[owner]/[repo]/workshop" as Workshop
    [Projects Page\n/wiki/projects] as Projects
  }

  package "API Routes (BFF 代理层)" as API_Routes {
    [api/auth/status] as AuthStatus
    [api/auth/validate] as AuthValidate
    [api/chat/stream] as ChatStream
    [api/models/config] as ModelsConfig
    component "api/tasks/[task]" as TasksProxy
    [api/wiki/projects] as WikiProjects
  }

  package "组件库 (Components)" as Components {
    [Ask] as AskComp
    [ConfigurationModal] as ConfigModal
    [Markdown] as MD
    [Mermaid] as MermaidComp
    [ModelSelectionModal] as ModelModal
    [ProcessedProjects] as ProcProj
    [TokenInput] as Token
    [UserSelector] as UserSel
    [WikiTreeView] as TreeView
    [WikiTypeSelector] as WikiType
    [ThemeToggle] as Theme
  }

  package "上下文 (Context)" {
    [LanguageContext] as LangCtx
  }

  package "Hooks" as Hooks {
    [useProcessedProjects] as UsePP
  }

  package "工具函数 (Utils)" as Utils {
    [getRepoUrl] as GetRepo
    [response] as Resp
    [sse] as SSEParser
    [taskRequest] as TaskReq
    [urlDecoder] as URLDec
  }

  package "类型定义 (Types)" {
    [RepoInfo] as RepoType
    [WikiPage] as WikiPageType
    [WikiStructure] as WikiStructType
  }

  package "国际化 (i18n)" {
    [messages/zh.json] as ZH
  }
}

package "Next.js 配置" {
  [next.config.ts\n(Rewrites + Standalone)] as NextConf
}

cloud ".NET 后端 (端口 8001)" as Backend

UI --> Pages
Pages --> Components
Pages --> Hooks
Pages --> Utils
Pages --> API_Routes
Components --> Hooks
Components --> Utils
API_Routes --> NextConf
NextConf --> Backend : 直接 Rewrite
API_Routes --> Backend : 代理转发
Pages --> LangCtx
LangCtx --> ZH

@enduml
```

---

## 3. 路由架构

```plantuml
@startuml
!theme plain
title Next.js 路由架构与页面关系

skinparam componentStyle rectangle

[Home Page\n/] as HOME
component "Repo Wiki Page\n/[owner]/[repo]" as WIKI
component "Slides Page\n/[owner]/[repo]/slides" as SLIDES
component "Workshop Page\n/[owner]/[repo]/workshop" as WORKSHOP
[Projects Page\n/wiki/projects] as PROJECTS

HOME -right-> WIKI : "点击生成 → 携带 Query 参数\n(owner, repo, type, token, provider,\nmodel, language, file_filters...)"
HOME -right-> PROJECTS : "导航链接"

WIKI -right-> SLIDES : "幻灯片按钮 → 携带 Query 参数"
WIKI -right-> WORKSHOP : "工作坊按钮 → 携带 Query 参数"

SLIDES --> WIKI : "返回 Wiki 链接"
WORKSHOP --> WIKI : "返回 Wiki 链接"

note bottom of HOME
  输入仓库 URL
  配置 Provider/Model/Token
  展示最近处理的项目
end note

note bottom of WIKI
  Wiki 页面查看器
  侧边栏树形导航
  AI 问答模态框
  导出 Markdown/JSON
end note

note bottom of SLIDES
  全屏幻灯片播放
  键盘导航
  导出 HTML 文件
end note

note bottom of WORKSHOP
  工作坊内容展示
  Markdown 渲染
  导出 .md 文件
end note

@enduml
```

### 3.1 路由参数传递模式

所有配置通过 URL Query 参数在页面间传递，关键参数如下：

| 参数 | 类型 | 描述 |
|------|------|------|
| `token` | string | 仓库访问令牌 |
| `type` | string | 仓库类型 (github/gitlab/bitbucket/local) |
| `repo_url` | string | 仓库完整 URL |
| `local_path` | string | 本地路径 |
| `provider` | string | LLM Provider ID |
| `model` | string | 模型 ID |
| `is_custom_model` | string | 是否为自定义模型 |
| `custom_model` | string | 自定义模型名称 |
| `language` | string | 生成语言 |
| `excluded_dirs` | string | 排除目录 (逗号分隔) |
| `excluded_files` | string | 排除文件 (逗号分隔) |
| `included_dirs` | string | 仅包含目录 (逗号分隔) |
| `included_files` | string | 仅包含文件 (逗号分隔) |
| `comprehensive` | string | Wiki 类型 (comprehensive/concise) |

---

## 4. 组件树与层次结构

```plantuml
@startuml
!theme plain
title 组件树层次结构

package "RootLayout (layout.tsx)" {
  [ThemeProvider] as TP
  [LanguageProvider] as LP
}

package "Home Page (page.tsx)" {
  [ConfigurationModal] as CM_HOME
  [ProcessedProjects] as PP_HOME
  [ThemeToggle] as TT_HOME
  [Mermaid (Demo)] as MD_DEMO
}

package "Repo Wiki Page (page.tsx)" {
  [WikiTreeView] as WTV
  [Markdown] as MD_WIKI
  [Ask] as ASK_WIKI
  [ModelSelectionModal] as MSM
  [ThemeToggle] as TT_WIKI
}

package "Slides Page (page.tsx)" {
  [ThemeToggle] as TT_SLIDES
}

package "Workshop Page (page.tsx)" {
  [Markdown] as MD_WORKSHOP
  [ThemeToggle] as TT_WORKSHOP
}

package "Projects Page (page.tsx)" {
  [ProcessedProjects] as PP_PROJ
}

package "ConfigurationModal" {
  [UserSelector] as US_CM
  [WikiTypeSelector] as WTS_CM
  [TokenInput] as TI_CM
}

package "Ask" {
  [Markdown] as MD_ASK
  [ModelSelectionModal] as MSM_ASK
}

package "ModelSelectionModal" {
  [WikiTypeSelector] as WTS_MSM
  [UserSelector] as US_MSM
  [TokenInput] as TI_MSM
}

TP --> LP
CM_HOME --> US_CM
CM_HOME --> WTS_CM
CM_HOME --> TI_CM
ASK_WIKI --> MD_ASK
ASK_WIKI --> MSM_ASK
MSM_ASK --> WTS_MSM
MSM_ASK --> US_MSM
MSM_ASK --> TI_MSM
MSM --> WTS_MSM
MSM --> US_MSM
MSM --> TI_MSM
PP_HOME --> [useProcessedProjects]
PP_PROJ --> [fetch API]

@enduml
```

---

## 5. 数据流架构

```plantuml
@startuml
!theme plain
title 前端数据流全景图

skinparam componentStyle rectangle

package "配置来源" {
  [URL Query Params] as URL
  [localStorage Cache] as LS
  [API /models/config] as MODELS_API
  [API /auth/status] as AUTH_API
}

package "组件状态" {
  [Home: repoUrl, config]
  [Wiki: wikiData, activePage, loading]
  [Ask: messages, loading, researchStages]
  [Slides: slides, currentSlide, loading]
  [Workshop: content, loading]
}

package "API 调用 (副作用)" {
  [POST /api/tasks/wiki] as TASK_WIKI
  [POST /api/tasks/ask] as TASK_ASK
  [POST /api/tasks/slides] as TASK_SLIDES
  [POST /api/tasks/workshop] as TASK_WORKSHOP
  [GET /api/wiki_cache] as CACHE_GET
  [DELETE /api/wiki_cache] as CACHE_DEL
  [POST /export/wiki] as EXPORT
  [GET /api/wiki/projects] as PROJ_LIST
  [DELETE /api/wiki/projects] as PROJ_DEL
}

package "Next.js Rewrites (直接转发)" {
  [api/wiki_cache/* → backend]
  [local_repo/structure → backend]
  [export/wiki/* → backend]
  [api/auth/status → backend]
  [api/auth/validate → backend]
  [api/lang/config → backend]
}

URL --> [组件状态] : 页面初始化参数
LS --> [组件状态] : 恢复已缓存的配置
MODELS_API --> [组件状态] : Provider/Model 选项
AUTH_API --> [组件状态] : 是否需要认证

[组件状态] --> TASK_WIKI
[组件状态] --> TASK_ASK
[组件状态] --> TASK_SLIDES
[组件状态] --> TASK_WORKSHOP
[组件状态] --> CACHE_GET
[组件状态] --> CACHE_DEL
[组件状态] --> EXPORT
[组件状态] --> PROJ_LIST
[组件状态] --> PROJ_DEL

[组件状态] --> LS : 保存 URL 配置缓存

TASK_WIKI --> CACHE_GET : 完成后刷新缓存
CACHE_DEL --> TASK_WIKI : 清除后重新生成

@enduml
```

---

## 6. Wiki 页面交互时序图

```plantuml
@startuml
!theme plain
title Wiki 页面完整交互时序图

actor 用户 as User
participant "Home Page" as Home
participant "ConfigurationModal" as ConfigModal
participant "Repo Wiki Page" as WikiPage
participant "WikiTreeView" as TreeView
participant "Markdown" as MD
participant "Ask" as AskComp
participant "Next.js\nAPI Route" as API
participant ".NET Backend" as Backend
database "localStorage" as LS

== 阶段一：首页配置 ==
User -> Home : 输入仓库 URL
Home -> Home : URLDecoder 解析\n(owner, repo, type, localPath)
Home -> LS : 读取 heimdallRepoConfigCache
LS --> Home : 该 URL 的已缓存配置 (如果有)

Home -> ConfigModal : 打开配置模态框
ConfigModal -> API : GET /api/models/config
API -> Backend : 代理转发
Backend --> API : Provider + Model 列表
API --> ConfigModal : 渲染 Provider/Model 下拉框

ConfigModal -> API : GET /api/auth/status
API -> Backend : 代理转发
Backend --> API : { authRequired: bool }
API --> ConfigModal : 如需认证则显示授权码输入框

User -> ConfigModal : 配置语言/模型/过滤器/令牌
ConfigModal -> LS : 保存配置到 heimdallRepoConfigCache

User -> ConfigModal : 点击 "生成 Wiki"

== 阶段二：Wiki 页面加载 ==
Home -> WikiPage : 导航至 /[owner]/[repo]?...\n(携带所有 Query 参数)
WikiPage -> WikiPage : useSearchParams() 提取配置
WikiPage -> WikiPage : 构建 RepoInfo 对象

WikiPage -> API : GET /api/wiki_cache?owner=...&repo=...&lang=...
API -> Backend : 查询缓存
Backend --> API : 缓存结果 (hit 或 miss)

alt 缓存命中
  API --> WikiPage : WikiCacheData
  WikiPage -> WikiPage : 设置 wikiData\n(无需生成)
else 缓存未命中
  WikiPage -> API : POST /api/tasks/wiki
  API -> Backend : 启动 Wiki 生成任务
  Backend -> Backend : 阶段一：生成 Wiki 结构
  Backend -> Backend : 阶段二：逐页生成内容
  Backend -> Backend : 写入缓存
  Backend --> API : WikiTaskResponse
  API --> WikiPage : { wiki_structure, generated_pages }
end

WikiPage -> TreeView : 传入 sections + pages\n构建侧边栏树
WikiPage -> MD : 渲染当前选中页面

== 阶段三：页面交互 ==
User -> TreeView : 点击页面标题
TreeView -> WikiPage : onPageSelect(pageId)
WikiPage -> MD : 更新当前页面

User -> AskComp : 点击 AI 助手按钮
User -> AskComp : 输入问题
AskComp -> API : POST /api/tasks/ask
API -> Backend : 执行问答 (支持 DeepResearch)
Backend --> API : AskTaskResponse { content, stages }
API --> AskComp : 显示回答 + 研究阶段

User -> WikiPage : 点击导出按钮
WikiPage -> API : POST /export/wiki
API -> Backend : 导出 Wiki
Backend --> API : 文件 Blob (Markdown/JSON)
API --> WikiPage : 触发浏览器下载

@enduml
```

---

## 7. Ask 深度研究流程序列图

```plantuml
@startuml
!theme plain
title Ask 深度研究 (Deep Research) 交互流程

actor 用户 as User
participant "Ask 组件" as Ask
participant "Markdown" as MD
participant "ModelSelectionModal" as MSM
participant "Next.js API" as API
participant "ChatOrchestratorService" as COS
participant "LLM" as LLM

User -> Ask : 输入问题，开启 "Deep Research"
Ask -> API : POST /api/tasks/ask\n{ question, deepResearch: true, history }
API -> COS : 深度研究管道

== 迭代 1 ==
COS -> COS : 选择 SystemPrompt #1\n(初始分析与计划)
COS -> LLM : 含仓库上下文的 Prompt
LLM --> COS : 输出（含计划）
COS --> API : Stage: plan (迭代 1)
API --> Ask : 流式显示计划阶段

== 迭代 2-4 (中间轮次) ==
loop 迭代 2 至 4
  COS -> COS : 选择 SystemPrompt #2\n(中间研究推进)
  COS -> COS : 将上一轮输出注入对话历史
  COS -> LLM : 含历史上下文的 Prompt
  LLM --> COS : 补充分析内容
  COS --> API : Stage: update (迭代 N)
  API --> Ask : 流式显示更新阶段
end

== 检测完成条件 ==
COS -> COS : 检测 "## Final Conclusion"\n或结论性语言标记

alt 检测到完成信号
  COS -> COS : 选择 SystemPrompt #3\n(最终结论)
  COS -> LLM : 生成最终结论
  LLM --> COS : 结论内容
  COS --> API : Stage: conclusion + complete: true
else 达到最大迭代数 (5)
  COS -> COS : 强制生成结论
  COS -> LLM : 强制总结
  LLM --> COS : 总结内容
  COS --> API : Stage: conclusion + complete: true
end

API --> Ask : { content, stages[], complete, iterations }
Ask -> Ask : 构建阶段导航\n(Plan → Update → Conclusion)
Ask -> MD : 渲染当前阶段内容

User -> Ask : 点击阶段标签切换查看
Ask -> MD : 渲染对应阶段内容

User -> Ask : 点击下载按钮
Ask -> Ask : 生成 .md 文件下载

@enduml
```

---

## 8. 幻灯片生成与播放流程

```plantuml
@startuml
!theme plain
title 幻灯片生成与全屏播放流程

|用户|
start
:访问 /[owner]/[repo]/slides;

|SlidesPage|
:从 URL 参数提取配置;
:POST /api/tasks/slides\n(内含 Wiki 生成 + 幻灯片计划生成);

|后端|
partition "步骤一：生成 Wiki" {
  :WikiTaskService 生成 Wiki 内容;
}
partition "步骤二：生成幻灯片计划" {
  :TaskPromptService 构建计划 Prompt;
  :LLM 生成幻灯片计划;
  :解析计划中的幻灯片标题列表;
}
partition "步骤三：逐页生成 HTML" {
  while (还有未生成的幻灯片?) is (是)
    :TaskPromptService\n构建单页 HTML Prompt\n(16:9 暗色主题);
    :LLM 生成 HTML 代码;
    :提取 HTML (从代码块或原始 div);
    :WrapSlideHtml()\n注入基础 CSS + Font Awesome;
  endwhile (否)
}

|前端|
:接收 slides[] 数组;

partition "幻灯片播放" {
  :显示第一张幻灯片;
  :渲染 iframe (srcdoc=HTML);
  :挂载键盘事件监听;

  while (用户交互) is (导航)
    if (按键: ArrowRight / Space?) then (下一页)
      :currentSlide += 1;
      :更新 iframe 内容;
    else if (按键: ArrowLeft?) then (上一页)
      :currentSlide -= 1;
      :更新 iframe 内容;
    else if (按键: F?) then (全屏)
      :document.documentElement.requestFullscreen();
    else if (按键: Escape?) then (退出全屏)
      :document.exitFullscreen();
    endif
  endwhile (关闭)
}

:导出为 HTML 文件\n(内联 CSS + JS + Mermaid + Chart.js);

stop

@enduml
```

---

## 9. Markdown 渲染组件架构

```plantuml
@startuml
!theme plain
title Markdown 渲染组件内部架构

package "Markdown 组件" {
  [react-markdown\n(核心渲染引擎)] as RMD
  [remark-gfm\n(GitHub Flavored Markdown)] as GFM
  [remark-math\n(数学公式支持)] as RMATH
  [rehype-raw\n(原始 HTML 支持)] as HRAW
  [rehype-katex\n(LaTeX 渲染)] as HKATEX
}

package "自定义渲染器" {
  [h1-h4 标题渲染器] as H_RENDER
  [代码块渲染器\n(react-syntax-highlighter)] as CODE_RENDER
  [Mermaid 检测 & 渲染] as MERMAID_RENDER
  [链接渲染器] as A_RENDER
  [表格渲染器] as TABLE_RENDER
}

package "Mermaid 组件" {
  [Mermaid 初始化\n(主题 CSS + 配置)] as M_INIT
  [SVG 渲染\n(mermaid.render)] as M_SVG
  [全屏模态框\n(FullScreenModal)] as M_FS
  [Pan & Zoom\n(svg-pan-zoom)] as M_PZ
}

package "辅助功能" {
  [复制按钮\n(代码块)] as COPY
  [特殊标题着色\n(Thought/Action/Observation/Answer)] as COLOR_H2
  [错误状态展示] as ERROR
  [加载状态动画] as LOADING
}

RMD --> GFM
RMD --> RMATH
RMD --> HRAW
RMD --> HKATEX
RMD --> H_RENDER
RMD --> CODE_RENDER
RMD --> A_RENDER
RMD --> TABLE_RENDER
CODE_RENDER --> COPY
CODE_RENDER --> MERMAID_RENDER
H_RENDER --> COLOR_H2
MERMAID_RENDER --> M_INIT
MERMAID_RENDER --> M_SVG
MERMAID_RENDER --> M_FS
M_SVG --> M_PZ
M_SVG --> ERROR
M_SVG --> LOADING

@enduml
```

---

## 10. 组件功能职责矩阵

| 组件 | 文件 | 行数 | 核心职责 |
|------|------|------|----------|
| **Ask** | `Ask.tsx` | 489 | AI 问答聊天界面，支持 Deep Research 多阶段研究，对话历史管理，模型切换，流式显示 |
| **ConfigurationModal** | `ConfigurationModal.tsx` | 298 | Wiki 生成前的完整配置：语言、类型、模型、文件过滤、访问令牌、授权码 |
| **Markdown** | `Markdown.tsx` | 176 | Markdown 到 React 的渲染管道，支持 GFM、数学公式、代码高亮、Mermaid、原始 HTML |
| **Mermaid** | `Mermaid.tsx` | 408 | Mermaid 图表 SVG 渲染，支持暗/亮主题、全屏缩放、svg-pan-zoom 交互 |
| **ModelSelectionModal** | `ModelSelectionModal.tsx` | 259 | AI 模型选择模态框（Ask 组件和 Wiki 刷新共用），局部状态先存后提交 |
| **ProcessedProjects** | `ProcessedProjects.tsx` | 268 | 已处理项目列表的卡片/列表视图切换，支持搜索过滤与删除 |
| **TokenInput** | `TokenInput.tsx` | 107 | 平台选择 + 访问令牌输入，支持 GitHub/GitLab/Bitbucket |
| **UserSelector** | `UserSelector.tsx` | 522 | Provider/Model 下拉选择，自定义模型切换，高级文件过滤选项（排除/仅包含目录和文件） |
| **WikiTreeView** | `WikiTreeView.tsx` | 183 | Wiki 页面层级树形导航，递归节渲染，重要性指示器，展开/折叠 |
| **WikiTypeSelector** | `WikiTypeSelector.tsx` | 78 | 综合型 vs 简洁型 Wiki 切换 |
| **ThemeToggle** | `theme-toggle.tsx` | 31 | 暗色/亮色主题切换按钮 |

---

## 11. BFF 代理与 Rewrite 策略

```plantuml
@startuml
!theme plain
title Next.js BFF 代理层架构

node "Next.js 前端" {
  package "直接 Rewrite (next.config.ts)" {
    [/api/wiki_cache/** → backend]
    [/local_repo/** → backend]
    [/export/wiki/** → backend]
    [/api/auth/** → backend]
    [/api/lang/** → backend]
  }

  package "API Route 代理 (src/app/api/)" {
    [api/auth/status/route.ts\n→ 添加错误处理]
    [api/auth/validate/route.ts\n→ 添加错误处理]
    [api/models/config/route.ts\n→ 添加错误处理]
    component "api/tasks/[task]/route.ts\n→ 白名单校验 + 流式适配"
    [api/wiki/projects/route.ts\n→ GET + DELETE 双重代理]
    [api/chat/stream/route.ts\n→ SSE 流式转发]
  }
}

cloud ".NET 后端\n(端口 8001)" as BE

note right of "直接 Rewrite (next.config.ts)"
  简单透传：Next.js 直接
  转发请求和响应，不经过
  JavaScript 代码处理。
  适用于无需额外处理的
  GET/简单 API 请求。
end note

note right of "API Route 代理 (src/app/api/)"
  有额外处理逻辑的代理：
  - 参数校验（如 tasks/[task]
    仅允许 wiki/ask/slides/workshop）
  - 请求/响应头处理
  - SSE 流式传输适配
  - 统一错误格式
end note

@enduml
```

### 11.1 Rewrite vs API Route 代理策略

| 策略 | 适用场景 | 示例 |
|------|----------|------|
| **直接 Rewrite** | 无需额外处理的简单请求 | 缓存 CRUD、认证状态、语言配置 |
| **API Route 代理** | 需要参数校验、错误转换、流式处理 | 任务创建（白名单校验）、聊天（SSE 适配） |

---

## 12. 国际化架构

```plantuml
@startuml
!theme plain
title 国际化系统设计

package "LanguageContext" {
  [language: string] as LANG
  [setLanguage: fn] as SETL
  [messages: object] as MSG
  component "supportedLanguages: string[]" as SUPP
}

package "messages/zh.json" {
  [common]
  [loading]
  [home]
  [form]
  [footer]
  [ask]
  [repoPage]
  [nav]
  [projects]
  [slides]
  [workshop]
}

[RootLayout] --> LanguageContext : 包裹全应用
LanguageContext --> "messages/zh.json" : 加载消息

note right of LanguageContext
  当前仅支持中文 (zh)。
  扩展其他语言时：
  1. 添加 messages/en.json
  2. 更新 supportedLanguages
  3. 放开 setLanguage 逻辑
end note

@enduml
```

---

## 13. 关键类型定义

```plantuml
@startuml
!theme plain
title 核心 TypeScript 接口定义

interface RepoInfo {
  + owner: string
  + repo: string
  + type: "github" | "gitlab" | "bitbucket" | "local"
  + token: string | null
  + localPath: string | null
  + repoUrl: string | null
}

interface WikiPage {
  + id: string
  + title: string
  + content: string
  + filePaths: string[]
  + importance: "high" | "medium" | "low"
  + relatedPages: string[]
  + parentId?: string
  + isSection?: boolean
  + children?: string[]
}

interface WikiStructure {
  + id: string
  + title: string
  + description: string
  + pages: WikiPage[]
}

interface WikiSection {
  + id: string
  + title: string
  + pages: string[]
  + subsections?: string[]
}

interface AskTaskRequest {
  + repo_url: string
  + question: string
  + history: DialogTurn[]
  + deep_research: boolean
  + filePath: string
  + token: string | null
  + type: string
  + provider: string
  + model: string
  + custom_model?: string
  + language: string
  + excluded_dirs: string[]
  + excluded_files: string[]
}

interface AskTaskResponse {
  + content: string
  + stages: ResearchStage[]
  + complete: boolean
  + iterations: number
}

interface ResearchStage {
  + title: string
  + content: string
  + iteration: number
  + type: "plan" | "update" | "conclusion"
}

interface ProcessedProject {
  + id: string
  + owner: string
  + repo: string
  + name: string
  + repo_type: string
  + submittedAt: string
  + language: string
}

interface ModelConfig {
  + providers: Provider[]
  + defaultProvider: string
}

interface Provider {
  + id: string
  + name: string
  + models: Model[]
  + supportsCustomModel?: boolean
}

interface Model {
  + id: string
  + name: string
}

RepoInfo *-- "1" WikiStructure : 关联
WikiStructure *-- "*" WikiPage
WikiStructure *-- "*" WikiSection
WikiSection *-- "*" WikiSection : 递归嵌套

@enduml
```

---

## 14. 构建与部署配置

```plantuml
@startuml
!theme plain
title 构建与部署流程

|开发环境|
start
:yarn dev (Turbopack);
:端口 3000;
:SERVER_BASE_URL=http://localhost:8001;

|构建|
:yarn build;
:next build;
:output: standalone;

|产物|
:frontend/.next/standalone/;
:包含 Node.js 运行时 + 所有依赖;

|Docker 部署|
:docker-compose up;
:heimdall-frontend 容器 (端口 3000);
:heimdall-api 容器 (端口 8001);
:共享卷: heimdall-data, heimdall-storage;

stop

@enduml
```

### 14.1 关键构建配置

| 配置项 | 值 | 说明 |
|--------|-----|------|
| `output` | `standalone` | 生成自包含的 Node.js 部署包 |
| `SERVER_BASE_URL` | `http://localhost:8001` (默认) | 后端 API 地址，Docker 中通过环境变量覆盖 |
| 包管理器 | Yarn 1.22.22 | 锁定依赖版本 |
| TypeScript | strict mode | 全量类型检查 |
| React 版本 | 19.2.6 | 最新稳定版 |
| Next.js 版本 | 16.2.6 | App Router + Turbopack |

---

## 15. 关键设计决策

### 15.1 薄前端设计
前端不包含任何业务编排逻辑。Wiki 生成、RAG 检索、LLM 调用等全部在后端完成，前端仅负责 UI 呈现与用户交互。这使得前后端可以独立演进。

### 15.2 URL 作为配置载体
所有配置参数通过 URL Query 传递而非全局状态。这带来三个好处：页面可书签保存、可分享链接、刷新不丢失状态。

### 15.3 Next.js Rewrite + API Route 混合代理
简单透传使用 Next.js Rewrite（零 JS 开销），需要校验或流式处理的请求使用 API Route 代理。既保持了性能又保证了灵活性。

### 15.4 组件自包含
每个组件管理自己的状态，通过 Props 接收外部配置和回调。避免了全局状态库的复杂性，使组件可独立测试和复用。

### 15.5 Mermaid SVGPanZoom 集成
Mermaid 组件使用内存中的 SVG 渲染与可选的 `svg-pan-zoom` 集成，支持在全屏模态框中缩放和平移复杂的架构图。
