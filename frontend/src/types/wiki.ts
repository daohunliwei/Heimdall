/**
 * Wiki 目录节点。
 * 当前前端阶段 0 主要使用扁平页面列表，因此该结构保留为兼容字段，
 * 便于后续继续恢复树状目录与章节编排。
 */
export interface WikiSection {
  /** 章节唯一标识。 */
  id: string;
  /** 章节标题。 */
  title: string;
  /** 当前章节直接包含的页面 ID 列表。 */
  pages: string[];
  /** 当前章节直接包含的子章节 ID 列表。 */
  subsections?: string[];
}

/**
 * Wiki 页面视图模型。
 * 该结构同时用于左侧导航与右侧正文展示。
 */
export interface WikiPage {
  /** 页面唯一标识。 */
  id: string;
  /** 页面标题。 */
  title: string;
  /** 页面简介，当前阶段由前端兜底为空字符串。 */
  description: string;
  /** Markdown 正文内容。 */
  content: string;
  /** 与页面关联的源文件路径列表。 */
  filePaths: string[];
  /** 页面重要程度。 */
  importance: 'high' | 'medium' | 'low';
  /** 关联页面 ID 列表。 */
  relatedPages: string[];
  /** 父页面 ID。 */
  parentId?: string;
  /** 是否作为章节节点展示。 */
  isSection?: boolean;
  /** 子页面 ID 列表。 */
  children?: string[];
}

/**
 * 仓库页使用的 Wiki 结构模型。
 * 当前阶段优先保证“版本一致性”，因此允许 sections/rootSections 为空，
 * 由导航组件自动回退为扁平列表。
 */
export interface WikiStructure {
  /** Wiki 结构唯一标识。 */
  id: string;
  /** Wiki 标题。 */
  title: string;
  /** Wiki 描述。 */
  description: string;
  /** 页面列表。 */
  pages: WikiPage[];
  /** 章节列表。 */
  sections: WikiSection[];
  /** 根章节 ID 列表。 */
  rootSections: string[];
}

/**
 * 仓库快照版本摘要。
 */
export interface RepositoryVersionSummary {
  /** 仓库快照版本 ID。 */
  repository_version_id: string;
  /** 分支名。 */
  branch_name: string;
  /** 提交 SHA。 */
  commit_sha: string;
  /** 提交时间。 */
  commit_time: string;
  /** 提交作者。 */
  commit_author: string;
  /** 提交信息。 */
  commit_message: string;
  /** 是否为当前分支最新提交。 */
  is_latest_on_branch: boolean;
  /** 源状态。 */
  source_status: string;
}

/**
 * Wiki 版本摘要。
 */
export interface WikiVersionSummary {
  /** Wiki 版本 ID。 */
  wiki_version_id: string;
  /** 所属 Wiki 空间 ID。 */
  wiki_space_id?: string;
  /** 关联的仓库快照版本 ID。 */
  repository_version_id: string;
  /** 版本号。 */
  version_no: number;
  /** 生成模式。 */
  generation_mode: string;
  /** 生成档位。 */
  generation_profile?: string;
  /** 版本状态。 */
  status: string;
  /** 页面数量。 */
  page_count: number;
  /** 目录深度。 */
  toc_depth?: number;
  /** 摘要 Markdown。 */
  summary_markdown?: string;
  /** 创建时间。 */
  created_at: string;
  /** 完成时间。 */
  completed_at?: string | null;
}

/**
 * 后端 `/wiki/pages` 接口返回的页面 DTO。
 */
export interface WikiVersionPagePayload {
  /** 页面 ID。 */
  id: string;
  /** 页面标题。 */
  title: string;
  /** Markdown 正文。 */
  content: string;
  /** 页面类型。 */
  page_type?: string;
  /** 重要程度。 */
  importance?: string;
  /** 页面顺序。 */
  page_order?: number;
  /** 源文件路径。 */
  file_paths?: string[];
  /** 导航标题。 */
  nav_title?: string | null;
  /** 父页面 ID。 */
  parent_page_id?: string | null;
  /** 深度。 */
  depth?: number;
  /** Token 数。 */
  token_count?: number;
  /** 页面状态。 */
  status?: string;
  /** 创建时间。 */
  created_at?: string;
}

/**
 * 由版本页 DTO 构建出的前端视图结果。
 */
export interface WikiViewState {
  /** 导航与正文共同使用的结构对象。 */
  wikiStructure: WikiStructure;
  /** 便于按 ID 快速索引页面内容的映射。 */
  generatedPages: Record<string, WikiPage>;
}

/**
 * 生成 Wiki 结构时需要的上下文参数。
 */
export interface BuildWikiViewOptions {
  /** 仓库展示名称。 */
  displayName: string;
  /** 当前 Wiki 版本摘要。 */
  wikiVersion?: WikiVersionSummary;
}

/**
 * 将后端重要级别兜底为前端可识别值。
 */
function normalizeImportance(value?: string): 'high' | 'medium' | 'low' {
  if (value === 'high' || value === 'medium' || value === 'low') {
    return value;
  }

  return 'medium';
}

/**
 * 基于 V2 版本页 DTO 构建仓库页展示状态。
 * 当前阶段优先保证“版本页内容”和“版本切换器”引用同一份版本数据，
 * 因此前端直接以 `/wiki/pages` 结果作为正文来源，而不再读取旧缓存聚合结构。
 */
export function buildWikiViewFromVersionPages(
  pages: WikiVersionPagePayload[],
  options: BuildWikiViewOptions,
): WikiViewState {
  const sortedPages = [...pages].sort((left, right) => {
    const orderDiff = (left.page_order ?? 0) - (right.page_order ?? 0);
    if (orderDiff !== 0) {
      return orderDiff;
    }

    return left.title.localeCompare(right.title, 'zh-CN');
  });

  const childPageIdsByParent = new Map<string, string[]>();
  for (const page of sortedPages) {
    if (!page.parent_page_id) {
      continue;
    }

    const currentChildren = childPageIdsByParent.get(page.parent_page_id) ?? [];
    currentChildren.push(page.id);
    childPageIdsByParent.set(page.parent_page_id, currentChildren);
  }

  const mappedPages: WikiPage[] = sortedPages.map((page) => ({
    id: page.id,
    title: page.nav_title || page.title,
    description: '',
    content: page.content || '',
    filePaths: page.file_paths ?? [],
    importance: normalizeImportance(page.importance),
    relatedPages: [],
    parentId: page.parent_page_id ?? undefined,
    children: childPageIdsByParent.get(page.id) ?? [],
  }));

  const generatedPages = Object.fromEntries(
    mappedPages.map((page) => [page.id, page]),
  );

  const versionLabel = options.wikiVersion
    ? `版本 v${options.wikiVersion.version_no} · ${options.wikiVersion.page_count} 页`
    : '暂无版本信息';

  return {
    wikiStructure: {
      id: options.wikiVersion?.wiki_version_id ?? `${options.displayName}-wiki`,
      title: `${options.displayName} Wiki`,
      description: versionLabel,
      pages: mappedPages,
      sections: [],
      rootSections: [],
    },
    generatedPages,
  };
}
