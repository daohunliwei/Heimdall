import React, { useMemo } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';
import rehypeKatex from 'rehype-katex';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { tomorrow } from 'react-syntax-highlighter/dist/cjs/styles/prism';
import Mermaid from './Mermaid';
import 'katex/dist/katex.min.css';

interface MarkdownProps {
  content: string;
}

/** 预处理 Wiki 内容：自动包裹 Mermaid、剥离裸 XML 标签、移除裸露元数据 */
function preprocessContent(raw: string): string {
  let result = raw;

  // 1. 剥离裸露的 JSON 元数据块（title: "..." nav_title: "..." ... source_files: [...] prerequisite_pages: [...]）
  result = result.replace(/^(title|nav_title|page_type|importance|summary|tags|source_files|related_pages|prerequisite_pages)\s*:\s*.+$/gm, '');

  // 2. 自动包裹裸 Mermaid 语法
  // graph TD/LR, sequenceDiagram, classDiagram, stateDiagram, gantt, pie
  const mermaidKeys = ['graph TD', 'graph LR', 'graph BT', 'graph RL',
    'sequenceDiagram', 'classDiagram', 'stateDiagram', 'gantt', 'pie', 'flowchart'];
  for (const kw of mermaidKeys) {
    if (result.includes(kw) && !result.includes('```mermaid')) {
      // 找到以关键字开头的行，包裹在 ```mermaid ``` 中
      const lines = result.split('\n');
      const newLines: string[] = [];
      let inMermaid = false;
      for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        const trimmed = line.trim();
        if (!inMermaid && mermaidKeys.some(k => trimmed.startsWith(k))) {
          newLines.push('```mermaid');
          newLines.push(line);
          inMermaid = true;
        } else if (inMermaid) {
          if (trimmed === '' || trimmed.startsWith('```') || trimmed.startsWith('classDef') || trimmed.startsWith('style ') ||
              trimmed.startsWith('subgraph') || trimmed.startsWith('end') || trimmed.match(/^[A-Za-z0-9_-]+(\[|\(|->|-->|---|\.\.|==>)/)) {
            newLines.push(line);
          } else {
            newLines.push('```');
            newLines.push(line);
            inMermaid = false;
          }
        } else {
          newLines.push(line);
        }
      }
      if (inMermaid) newLines.push('```');
      result = newLines.join('\n');
      break; // 只处理一次，避免重复包裹
    }
  }

  // 3. 剥离裸 XML/HTML 标签（来自 .csproj/.config 等文件）
  const csprojTags = 'Description|Compile|None|PackageReference|ProjectReference|EmbeddedResource|Content|Folder|Service|OutputPath|TargetFramework|LangVersion|Nullable|ImplicitUsings|GenerateDocumentationFile|NoWarn|TreatWarningsAsErrors|AssemblyName|RootNamespace|AssemblyVersion|FileVersion|NeutralLanguage|SignAssembly|AssemblyOriginatorKeyFile|DelaySign|DebugType|Optimize|DefineConstants|ErrorReport|WarningLevel|PlatformTarget|Prefer32Bit|AllowUnsafeBlocks|CheckForOverflowUnderflow|DocumentationFile|NativeDllNameSourceLines|ItemGroup|PropertyGroup|Target|Exec|When|Otherwise|Choose|Import|Using|GenerateNativeDllName';
  result = result.replace(new RegExp(`<(${csprojTags})\\b[^>]*\\/?>`, 'gi'), '&lt;$1&gt;');
  result = result.replace(new RegExp(`<\\/(${csprojTags})>`, 'gi'), '&lt;/$1&gt;');

  // 4. 修复代码块中缺少 mermaid 语言标记
  result = result.replace(/```\s*\n([\s\S]*?)```/g, (match, content: string) => {
    const trimmed = content.trim();
    const isMermaid = mermaidKeys.some(k => trimmed.startsWith(k) || trimmed.includes('\n' + k));
    if (isMermaid) return '```mermaid\n' + trimmed + '\n```';
    return match;
  });

  return result;
}

const Markdown: React.FC<MarkdownProps> = ({ content }) => {
  const processed = useMemo(() => preprocessContent(content), [content]);
  const MarkdownComponents: React.ComponentProps<typeof ReactMarkdown>['components'] = {
    pre({ children }: { children?: React.ReactNode }) {
      // 使用 div 包装而非 Fragment，避免 block 元素直接嵌套在 <p> 中导致 hydration 错误
      return <div>{children}</div>;
    },
    p({ children, ...props }: { children?: React.ReactNode }) {
      // V4 修复：段落可能存在块级子元素（代码块/div 等），使用 div 避免 hydration 错误
      return <div className="mb-3 text-sm leading-relaxed text-[var(--foreground)]" {...props}>{children}</div>;
    },
    h1({ children, ...props }: { children?: React.ReactNode }) {
      return <h1 className="text-xl font-bold mt-8 mb-4 text-[var(--foreground)]" {...props}>{children}</h1>;
    },
    h2({ children, ...props }: { children?: React.ReactNode }) {
      if (children && typeof children === 'string') {
        const text = children.toString();
        if (text.includes('Thought') || text.includes('Action') || text.includes('Observation') || text.includes('Answer')) {
          return (
            <h2
              className={`text-base font-bold mt-5 mb-3 p-2 rounded ${
                text.includes('Thought') ? 'bg-blue-50 text-blue-700 border border-blue-200' :
                text.includes('Action') ? 'bg-green-50 text-green-700 border border-green-200' :
                text.includes('Observation') ? 'bg-amber-50 text-amber-700 border border-amber-200' :
                text.includes('Answer') ? 'bg-purple-50 text-purple-700 border border-purple-200' :
                'text-[var(--foreground)]'
              }`}
              {...props}
            >
              {children}
            </h2>
          );
        }
      }
      return <h2 className="text-lg font-bold mt-6 mb-3 text-[var(--foreground)]" {...props}>{children}</h2>;
    },
    h3({ children, ...props }: { children?: React.ReactNode }) {
      return <h3 className="text-base font-semibold mt-5 mb-2 text-[var(--foreground)]" {...props}>{children}</h3>;
    },
    h4({ children, ...props }: { children?: React.ReactNode }) {
      return <h4 className="text-sm font-semibold mt-4 mb-2 text-[var(--foreground)]" {...props}>{children}</h4>;
    },
    ul({ children, ...props }: { children?: React.ReactNode }) {
      return <ul className="list-disc pl-6 mb-4 text-sm text-[var(--foreground)] space-y-2" {...props}>{children}</ul>;
    },
    ol({ children, ...props }: { children?: React.ReactNode }) {
      return <ol className="list-decimal pl-6 mb-4 text-sm text-[var(--foreground)] space-y-2" {...props}>{children}</ol>;
    },
    li({ children, ...props }: { children?: React.ReactNode }) {
      return <li className="mb-2 text-sm leading-relaxed text-[var(--foreground)]" {...props}>{children}</li>;
    },
    a({ children, href, ...props }: { children?: React.ReactNode; href?: string }) {
      return (
        <a href={href} className="text-[var(--accent-primary)] hover:underline font-medium" target="_blank" rel="noopener noreferrer" {...props}>
          {children}
        </a>
      );
    },
    blockquote({ children, ...props }: { children?: React.ReactNode }) {
      return (
        <blockquote className="border-l-4 border-[var(--accent-primary)]/30 pl-4 py-1 text-[var(--muted)] italic my-4 text-sm" {...props}>
          {children}
        </blockquote>
      );
    },
    table({ children, ...props }: { children?: React.ReactNode }) {
      // V4 修复：表格外不应包裹在 p 标签中，独立 div 渲染避免 hydration 错误
      return (
        <div className="overflow-x-auto my-6 rounded-lg border border-[var(--border-color)] table-wrapper">
          <table className="min-w-full text-sm border-collapse" {...props}>{children}</table>
        </div>
      );
    },
    thead({ children, ...props }: { children?: React.ReactNode }) {
      return <thead className="bg-[var(--background)]" {...props}>{children}</thead>;
    },
    tbody({ children, ...props }: { children?: React.ReactNode }) {
      return <tbody className="divide-y divide-[var(--border-color)]" {...props}>{children}</tbody>;
    },
    tr({ children, ...props }: { children?: React.ReactNode }) {
      return <tr className="hover:bg-[var(--background)]/50" {...props}>{children}</tr>;
    },
    th({ children, ...props }: { children?: React.ReactNode }) {
      return <th className="px-4 py-3 text-left font-medium text-[var(--foreground)] border-b border-[var(--border-color)] align-top" {...props}>{children}</th>;
    },
    td({ children, ...props }: { children?: React.ReactNode }) {
      return <td className="px-4 py-3 border-t border-[var(--border-color)] align-top text-[var(--foreground)]" {...props}>{children}</td>;
    },
    code(props: {
      inline?: boolean;
      className?: string | string[];
      children?: React.ReactNode;
       
      [key: string]: any;
    }) {
      const { inline, className, children, node, ...otherProps } = props;
      const classNameValue = Array.isArray(className) ? className.join(' ') : className;
      const match = /language-([^\s]+)/.exec(classNameValue || '');
      const codeContent = children ? String(children).replace(/\n$/, '') : '';
      const nodeLanguage = typeof node?.lang === 'string' ? node.lang : undefined;
      const language = match?.[1] ?? nodeLanguage;
      const normalizedLanguage = language ? language.toLowerCase() : undefined;

      if (inline === false && normalizedLanguage === 'mermaid') {
        return (
          <div className="my-8 rounded-lg overflow-hidden border border-[var(--border-color)]">
            <Mermaid chart={codeContent} className="w-full max-w-full" zoomingEnabled={true} />
          </div>
        );
      }

      if (inline === false) {
        const displayLanguage = normalizedLanguage ?? 'code';
        return (
          <div className="my-6 rounded-lg overflow-hidden text-sm border border-[var(--border-color)]">
            <div className="bg-[#1e1e1e] text-[#ccc] px-4 py-2 text-xs flex justify-between items-center font-mono">
              <span>{displayLanguage}</span>
              <button
                onClick={() => { navigator.clipboard.writeText(codeContent); }}
                className="text-[#888] hover:text-white transition-colors"
                title="Copy code"
              >
                <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                </svg>
              </button>
            </div>
            <SyntaxHighlighter
              language={normalizedLanguage}
              style={tomorrow}
              className="!text-sm"
              customStyle={{ margin: 0, borderRadius: '0 0 0.5rem 0.5rem', padding: '1rem' }}
              showLineNumbers={Boolean(normalizedLanguage)}
              wrapLines={true}
              wrapLongLines={true}
              {...otherProps}
            >
              {codeContent}
            </SyntaxHighlighter>
          </div>
        );
      }

      return (
        <code className="font-mono bg-[var(--background)] px-1.5 py-0.5 rounded text-[var(--highlight)] text-sm border border-[var(--border-color)]" {...otherProps}>
          {children}
        </code>
      );
    },
  };

  return (
    <div className="prose max-w-none">
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkMath]}
        rehypePlugins={[rehypeKatex]}
        components={MarkdownComponents}
      >
        {processed}
      </ReactMarkdown>
    </div>
  );
};

export default Markdown;
