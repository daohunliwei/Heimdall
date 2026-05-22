'use client';

import React, { useState, useCallback, useMemo } from 'react';
import { FaChevronRight, FaChevronDown } from 'react-icons/fa';

interface WikiPage {
  id: string;
  title: string;
  content: string;
  filePaths: string[];
  importance: 'high' | 'medium' | 'low';
  relatedPages: string[];
  parentId?: string;
  depth?: number;
  isSection?: boolean;
  children?: string[];
}

interface WikiSection {
  id: string;
  title: string;
  pages: string[];
  subsections?: string[];
}

interface WikiStructure {
  id: string;
  title: string;
  description: string;
  pages: WikiPage[];
  sections: WikiSection[];
  rootSections: string[];
}

interface WikiTreeViewProps {
  wikiStructure: WikiStructure;
  currentPageId: string | undefined;
  onPageSelect: (pageId: string) => void;
  messages?: {
    pages?: string;
    [key: string]: string | undefined;
  };
}

interface TreeNode {
  page: WikiPage;
  children: TreeNode[];
}

const WikiTreeView: React.FC<WikiTreeViewProps> = ({
  wikiStructure,
  currentPageId,
  onPageSelect,
}) => {
  // 构建 parentId 树形结构
  const pageTree = useMemo(() => {
    const pageMap = new Map<string, WikiPage>();
    wikiStructure.pages.forEach(p => pageMap.set(p.id, p));

    const childrenMap = new Map<string | null, WikiPage[]>();
    wikiStructure.pages.forEach(page => {
      const parentKey = page.parentId || null;
      if (!childrenMap.has(parentKey)) childrenMap.set(parentKey, []);
      childrenMap.get(parentKey)!.push(page);
    });

    const buildTree = (parentId: string | null): TreeNode[] => {
      const children = childrenMap.get(parentId) || [];
      return children.map(page => ({
        page,
        children: buildTree(page.id),
      }));
    };

    return buildTree(null);
  }, [wikiStructure.pages]);

  // 是否使用 parentId 树形结构（至少存在一个有 parentId 的页面）
  const hasTreeStructure = useMemo(
    () => wikiStructure.pages.some(p => !!p.parentId),
    [wikiStructure.pages]
  );

  // 获取当前页面的祖先 ID 链
  const getAncestorIds = useCallback(
    (pageId: string): string[] => {
      const ancestors: string[] = [];
      let current = wikiStructure.pages.find(p => p.id === pageId);
      while (current?.parentId) {
        ancestors.push(current.parentId);
        current = wikiStructure.pages.find(p => p.id === current!.parentId);
      }
      return ancestors;
    },
    [wikiStructure.pages]
  );

  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(() => {
    const initial = new Set<string>();
    // 自动展开前 2 层节点
    const expandLevel = (nodes: TreeNode[], depth: number) => {
      if (depth > 2) return;
      for (const node of nodes) {
        initial.add(node.page.id);
        expandLevel(node.children, depth + 1);
      }
    };
    expandLevel(pageTree, 1);
    // 展开当前页面的祖先路径
    if (currentPageId) {
      getAncestorIds(currentPageId).forEach(id => initial.add(id));
    }
    return initial;
  });

  // 当前页面变化时自动展开祖先路径
  const [prevPageId, setPrevPageId] = useState(currentPageId);
  if (currentPageId && currentPageId !== prevPageId) {
    setPrevPageId(currentPageId);
    const ancestors = getAncestorIds(currentPageId);
    if (ancestors.length > 0) {
      const hasAll = ancestors.every(a => expandedNodes.has(a));
      if (!hasAll) {
        setExpandedNodes(prev => {
          const next = new Set(prev);
          ancestors.forEach(a => next.add(a));
          return next;
        });
      }
    }
  }
  if (!currentPageId && prevPageId) {
    setPrevPageId(undefined);
  }

  const toggleNode = useCallback((nodeId: string) => {
    setExpandedNodes(prev => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  }, []);

  // 渲染单个页面节点
  const renderPageNode = (page: WikiPage, level: number, hasChildren: boolean) => {
    const isSelected = currentPageId === page.id;
    const isExpanded = expandedNodes.has(page.id);
    const indent = Math.min(level * 16, 64);

    return (
      <div key={page.id} className="mb-0.5">
        <button
          className={`flex items-center w-full text-left px-2 py-1.5 rounded-md text-sm transition-colors ${
            isSelected
              ? 'bg-[var(--accent-primary)]/20 text-[var(--accent-primary)] border border-[var(--accent-primary)]/30 ring-1 ring-[var(--accent-primary)]/20'
              : 'text-[var(--foreground)] hover:bg-[var(--background)] border border-transparent'
          }`}
          style={{ paddingLeft: `${indent + 8}px` }}
          onClick={() => {
            if (hasChildren) {
              toggleNode(page.id);
            }
            onPageSelect(page.id);
          }}
        >
          {hasChildren ? (
            <span className="w-4 flex-shrink-0 flex items-center justify-center mr-1">
              {isExpanded ? (
                <FaChevronDown className="text-xs" />
              ) : (
                <FaChevronRight className="text-xs" />
              )}
            </span>
          ) : (
            <span className="w-4 flex-shrink-0 mr-1" />
          )}
          <div
            className={`w-2 h-2 rounded-full mr-2 flex-shrink-0 ${
              page.importance === 'high'
                ? 'bg-[#2563eb]'
                : page.importance === 'medium'
                ? 'bg-[#f59e0b]'
                : 'bg-[#a3a3a3]'
            }`}
          />
          <span className="truncate">{page.title}</span>
        </button>

        {hasChildren && (
          <div
            className={`overflow-hidden transition-all duration-200 ease-in-out ${
              isExpanded ? 'max-h-[5000px] opacity-100' : 'max-h-0 opacity-0'
            }`}
          >
            {isExpanded &&
              pageTree
                .flatMap(n => flattenTree(n))
                .filter(n => n.page.parentId === page.id)
                .map(n =>
                  renderPageNode(n.page, level + 1, n.children.length > 0)
                )}
          </div>
        )}
      </div>
    );
  };

  // 渲染树节点（递归）
  const renderTreeNode = (node: TreeNode, level: number): React.ReactNode => {
    const hasChildren = node.children.length > 0;
    const isExpanded = expandedNodes.has(node.page.id);

    return (
      <div key={node.page.id} className="mb-0.5">
        <button
          className={`flex items-center w-full text-left px-2 py-1.5 rounded-md text-sm transition-colors ${
            currentPageId === node.page.id
              ? 'bg-[var(--accent-primary)]/20 text-[var(--accent-primary)] border border-[var(--accent-primary)]/30 ring-1 ring-[var(--accent-primary)]/20'
              : 'text-[var(--foreground)] hover:bg-[var(--background)] border border-transparent'
          }`}
          style={{ paddingLeft: `${level * 16 + 8}px` }}
          onClick={() => {
            if (hasChildren) toggleNode(node.page.id);
            onPageSelect(node.page.id);
          }}
        >
          {hasChildren ? (
            <span className="w-4 flex-shrink-0 flex items-center justify-center mr-1">
              {isExpanded ? (
                <FaChevronDown className="text-xs" />
              ) : (
                <FaChevronRight className="text-xs" />
              )}
            </span>
          ) : (
            <span className="w-4 flex-shrink-0 mr-1" />
          )}
          <div
            className={`w-2 h-2 rounded-full mr-2 flex-shrink-0 ${
              node.page.importance === 'high'
                ? 'bg-[#2563eb]'
                : node.page.importance === 'medium'
                ? 'bg-[#f59e0b]'
                : 'bg-[#a3a3a3]'
            }`}
          />
          <span className="truncate">{node.page.title}</span>
        </button>

        {hasChildren && (
          <div
            className={`overflow-hidden transition-all duration-200 ease-in-out ${
              isExpanded ? 'max-h-[5000px] opacity-100' : 'max-h-0 opacity-0'
            }`}
          >
            {isExpanded &&
              node.children.map(child => renderTreeNode(child, level + 1))}
          </div>
        )}
      </div>
    );
  };

  // 渲染基于 parentId 的树形结构
  const renderTreeView = () => (
    <div className="space-y-0.5">
      {pageTree.map(node => renderTreeNode(node, 0))}
    </div>
  );

  // 兼容旧的 section 平铺结构
  const renderSectionBasedView = () => {
    const [expandedSections, setExpandedSectionsState] = useState<Set<string>>(() => {
      const initial = new Set<string>();
      const expandLevel = (sectionIds: string[], depth: number) => {
        if (depth > 2) return;
        for (const id of sectionIds) {
          initial.add(id);
          const section = wikiStructure.sections.find(s => s.id === id);
          if (section?.subsections?.length) {
            expandLevel(section.subsections, depth + 1);
          }
        }
      };
      expandLevel(wikiStructure.rootSections || [], 0);
      return initial;
    });

    const toggleSection = useCallback((sectionId: string, event: React.MouseEvent) => {
      event.stopPropagation();
      setExpandedSectionsState(prev => {
        const newSet = new Set(prev);
        if (newSet.has(sectionId)) newSet.delete(sectionId);
        else newSet.add(sectionId);
        return newSet;
      });
    }, []);

    const renderSection = (sectionId: string, level = 0): React.ReactNode => {
      const section = wikiStructure.sections.find(s => s.id === sectionId);
      if (!section) return null;

      const isExpanded = expandedSections.has(sectionId);

      return (
        <div key={sectionId} className="mb-0.5">
          <button
            className={`flex items-center w-full text-left px-2 py-1.5 rounded-md text-sm font-medium text-[var(--foreground)] hover:bg-[var(--background)]/70 transition-colors ${
              level === 0 ? 'bg-[var(--background)]/50' : ''
            }`}
            onClick={(e) => toggleSection(sectionId, e)}
          >
            <span className="w-4 flex-shrink-0 flex items-center justify-center mr-1">
              {isExpanded ? (
                <FaChevronDown className="text-xs" />
              ) : (
                <FaChevronRight className="text-xs" />
              )}
            </span>
            <span className="truncate">{section.title}</span>
          </button>

          <div
            className={`overflow-hidden transition-all duration-200 ease-in-out ${
              isExpanded ? 'max-h-[5000px] opacity-100' : 'max-h-0 opacity-0'
            }`}
          >
            <div className="mt-0.5 space-y-0.5 pl-2 border-l border-[var(--border-color)]/30"
              style={{ marginLeft: `${Math.min(level * 4 + 4, 20)}px` }}>
              {section.pages.map(pageId => {
                const page = wikiStructure.pages.find(p => p.id === pageId);
                if (!page) return null;
                const isSelected = currentPageId === pageId;
                return (
                  <button
                    key={pageId}
                    className={`w-full text-left px-3 py-1.5 rounded-md text-sm transition-colors ${
                      isSelected
                        ? 'bg-[var(--accent-primary)]/20 text-[var(--accent-primary)] border border-[var(--accent-primary)]/30 ring-1 ring-[var(--accent-primary)]/20'
                        : 'text-[var(--foreground)] hover:bg-[var(--background)] border border-transparent'
                    }`}
                    onClick={() => onPageSelect(pageId)}
                  >
                    <div className="flex items-center">
                      <div
                        className={`w-2 h-2 rounded-full mr-2 flex-shrink-0 ${
                          page.importance === 'high'
                            ? 'bg-[#2563eb]'
                            : page.importance === 'medium'
                            ? 'bg-[#f59e0b]'
                            : 'bg-[#a3a3a3]'
                        }`}
                      />
                      <span className="truncate">{page.title}</span>
                    </div>
                  </button>
                );
              })}
              {section.subsections?.map(subId => renderSection(subId, level + 1))}
            </div>
          </div>
        </div>
      );
    };

    if (!wikiStructure.sections?.length || !wikiStructure.rootSections?.length) {
      return (
        <ul className="space-y-1">
          {wikiStructure.pages.map(page => {
            const isSelected = currentPageId === page.id;
            return (
              <li key={page.id}>
                <button
                  className={`w-full text-left px-3 py-2 rounded-md text-sm transition-colors ${
                    isSelected
                      ? 'bg-[var(--accent-primary)]/20 text-[var(--accent-primary)] border border-[var(--accent-primary)]/30 ring-1 ring-[var(--accent-primary)]/20'
                      : 'text-[var(--foreground)] hover:bg-[var(--background)] border border-transparent'
                  }`}
                  onClick={() => onPageSelect(page.id)}
                >
                  <div className="flex items-center">
                    <div
                      className={`w-2 h-2 rounded-full mr-2 flex-shrink-0 ${
                        page.importance === 'high'
                          ? 'bg-[#2563eb]'
                          : page.importance === 'medium'
                          ? 'bg-[#f59e0b]'
                          : 'bg-[#a3a3a3]'
                      }`}
                    />
                    <span className="truncate">{page.title}</span>
                  </div>
                </button>
              </li>
            );
          })}
        </ul>
      );
    }

    return (
      <div className="space-y-0.5">
        {wikiStructure.rootSections.map(sectionId => renderSection(sectionId))}
      </div>
    );
  };

  // 根据是否存在 parentId 层级选择渲染模式
  if (hasTreeStructure) {
    return renderTreeView();
  }

  // 保留旧的 section-based 渲染（兼容旧数据）
  return <SectionBasedView
    wikiStructure={wikiStructure}
    currentPageId={currentPageId}
    onPageSelect={onPageSelect}
  />;
};

// Section-based 视图组件（向后兼容）
const SectionBasedView: React.FC<{
  wikiStructure: WikiStructure;
  currentPageId: string | undefined;
  onPageSelect: (pageId: string) => void;
}> = ({ wikiStructure, currentPageId, onPageSelect }) => {
  const [expandedSections, setExpandedSections] = useState<Set<string>>(() => {
    const initial = new Set<string>();
    const expandLevel = (sectionIds: string[], depth: number) => {
      if (depth > 2) return;
      for (const id of sectionIds) {
        initial.add(id);
        const section = wikiStructure.sections.find(s => s.id === id);
        if (section?.subsections?.length) expandLevel(section.subsections, depth + 1);
      }
    };
    expandLevel(wikiStructure.rootSections || [], 0);
    return initial;
  });

  const toggleSection = useCallback((sectionId: string, event: React.MouseEvent) => {
    event.stopPropagation();
    setExpandedSections(prev => {
      const newSet = new Set(prev);
      if (newSet.has(sectionId)) newSet.delete(sectionId);
      else newSet.add(sectionId);
      return newSet;
    });
  }, []);

  const renderSection = (sectionId: string, level = 0): React.ReactNode => {
    const section = wikiStructure.sections.find(s => s.id === sectionId);
    if (!section) return null;
    const isExpanded = expandedSections.has(sectionId);

    return (
      <div key={sectionId} className="mb-0.5">
        <button
          className={`flex items-center w-full text-left px-2 py-1.5 rounded-md text-sm font-medium text-[var(--foreground)] hover:bg-[var(--background)]/70 transition-colors ${
            level === 0 ? 'bg-[var(--background)]/50' : ''
          }`}
          onClick={(e) => toggleSection(sectionId, e)}
        >
          <span className="w-4 flex-shrink-0 flex items-center justify-center mr-1">
            {isExpanded ? <FaChevronDown className="text-xs" /> : <FaChevronRight className="text-xs" />}
          </span>
          <span className="truncate">{section.title}</span>
        </button>

        <div className={`overflow-hidden transition-all duration-200 ease-in-out ${
          isExpanded ? 'max-h-[5000px] opacity-100' : 'max-h-0 opacity-0'
        }`}>
          <div className="mt-0.5 space-y-0.5 pl-2 border-l border-[var(--border-color)]/30"
            style={{ marginLeft: `${Math.min(level * 4 + 4, 20)}px` }}>
            {section.pages.map(pageId => {
              const page = wikiStructure.pages.find(p => p.id === pageId);
              if (!page) return null;
              const isSelected = currentPageId === pageId;
              return (
                <button
                  key={pageId}
                  className={`w-full text-left px-3 py-1.5 rounded-md text-sm transition-colors ${
                    isSelected
                      ? 'bg-[var(--accent-primary)]/20 text-[var(--accent-primary)] border border-[var(--accent-primary)]/30 ring-1 ring-[var(--accent-primary)]/20'
                      : 'text-[var(--foreground)] hover:bg-[var(--background)] border border-transparent'
                  }`}
                  onClick={() => onPageSelect(pageId)}
                >
                  <div className="flex items-center">
                    <div className={`w-2 h-2 rounded-full mr-2 flex-shrink-0 ${
                      page.importance === 'high' ? 'bg-[#2563eb]' : page.importance === 'medium' ? 'bg-[#f59e0b]' : 'bg-[#a3a3a3]'
                    }`} />
                    <span className="truncate">{page.title}</span>
                  </div>
                </button>
              );
            })}
            {section.subsections?.map(subId => renderSection(subId, level + 1))}
          </div>
        </div>
      </div>
    );
  };

  if (!wikiStructure.sections?.length || !wikiStructure.rootSections?.length) {
    return (
      <ul className="space-y-1">
        {wikiStructure.pages.map(page => {
          const isSelected = currentPageId === page.id;
          return (
            <li key={page.id}>
              <button
                className={`w-full text-left px-3 py-2 rounded-md text-sm transition-colors ${
                  isSelected
                    ? 'bg-[var(--accent-primary)]/20 text-[var(--accent-primary)] border border-[var(--accent-primary)]/30 ring-1 ring-[var(--accent-primary)]/20'
                    : 'text-[var(--foreground)] hover:bg-[var(--background)] border border-transparent'
                }`}
                onClick={() => onPageSelect(page.id)}
              >
                <div className="flex items-center">
                  <div className={`w-2 h-2 rounded-full mr-2 flex-shrink-0 ${
                    page.importance === 'high' ? 'bg-[#2563eb]' : page.importance === 'medium' ? 'bg-[#f59e0b]' : 'bg-[#a3a3a3]'
                  }`} />
                  <span className="truncate">{page.title}</span>
                </div>
              </button>
            </li>
          );
        })}
      </ul>
    );
  }

  return (
    <div className="space-y-0.5">
      {wikiStructure.rootSections.map(sectionId => renderSection(sectionId))}
    </div>
  );
};

function flattenTree(node: TreeNode): TreeNode[] {
  const result: TreeNode[] = [node];
  for (const child of node.children) {
    result.push(...flattenTree(child));
  }
  return result;
}

export default WikiTreeView;
