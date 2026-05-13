namespace Heimdall.Core.Services.Tasks;

public sealed class TaskPromptService
{
    public string BuildWikiStructurePrompt(
        string owner, string repo, string fileTree, string readme,
        string language, bool comprehensive)
    {
        var detailLevel = comprehensive ? "请进行详尽分析，覆盖所有主要目录和关键文件" : "请进行概要分析";

        return $$"""
你是一个资深软件架构师和技术文档专家。请分析以下代码仓库并生成 Wiki 结构。

仓库名称：{{owner}}/{{repo}}
输出语言：{{language}}
分析深度：{{detailLevel}}

## 仓库文件树

{{fileTree}}

## README 内容

{{readme}}

## 输出要求

请以 XML 格式输出 Wiki 结构，使用以下格式：

```xml
<wiki_structure>
  <title>仓库 Wiki 标题</title>
  <description>仓库简要描述</description>
  <sections>
    <section id="section-id">
      <title>分区标题</title>
      <pages>
        <page_ref>page-id</page_ref>
      </pages>
      <subsections>
        <section_ref>subsection-id</section_ref>
      </subsections>
    </section>
  </sections>
  <pages>
    <page id="page-id">
      <title>页面标题</title>
      <description>页面简要描述</description>
      <importance>high|medium|low</importance>
      <relevant_files>
        <file_path>src/path/to/file.cs</file_path>
      </relevant_files>
      <related_pages>
        <related>other-page-id</related>
      </related_pages>
      <parent_section>section-id</parent_section>
    </page>
  </pages>
</wiki_structure>
```

请确保：
- 每个重要模块/目录都有对应的分区
- 代码文件按功能模块分组
- 页面之间建立合理的关联
- 标记重要性级别（high/medium/low）
""";
    }

    public string BuildWikiPagePrompt(
        string pageId, string pageTitle, string pageDescription,
        string owner, string repo, string repoType, string repoUrl,
        string fileTree, string language)
    {
        return $$"""
你是一个资深技术文档专家。请为以下代码仓库的 Wiki 页面生成详细内容。

仓库：{{owner}}/{{repo}} ({{repoType}})
页面：{{pageTitle}}
页面描述：{{pageDescription}}
输出语言：{{language}}

## 仓库文件树

{{fileTree}}

## 要求

请以 Markdown 格式生成页面内容，包含：
1. 页面概述
2. 核心功能/模块说明
3. 关键代码片段（如适用）
4. 相关依赖和接口
5. 最佳实践和使用示例

请使用专业的技术文档风格，中文输出。
""";
    }

    public string BuildRagQueryPrompt(string question, string context)
    {
        return $"""
基于以下代码仓库上下文回答问题：

## 相关代码上下文

{context}

## 用户问题

{question}

请给出准确、详细的中文回答，引用相关代码文件路径。
""";
    }
}
