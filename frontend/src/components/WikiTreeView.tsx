'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { FaChevronRight, FaChevronDown } from 'react-icons/fa';

interface WikiPage {
  id: string;
  title: string;
  content: string;
  filePaths: string[];
  importance: 'high' | 'medium' | 'low';
  relatedPages: string[];
  parentId?: string;
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

const WikiTreeView: React.FC<WikiTreeViewProps> = ({
  wikiStructure,
  currentPageId,
  onPageSelect,
}) => {
  const [expandedSections, setExpandedSections] = useState<Set<string>>(
    new Set(wikiStructure.rootSections)
  );

  // 当选中页面变化时，自动展开包含该页面的所有父章节
  useEffect(() => {
    if (!currentPageId || !wikiStructure.sections?.length) return;

    const findParentSections = (pageId: string): string[] => {
      const parents: string[] = [];
      for (const section of wikiStructure.sections) {
        if (section.pages.includes(pageId)) {
          parents.push(section.id);
          // 检查该 section 是否被其他 section 的 subsections 引用
          for (const parent of wikiStructure.sections) {
            if (parent.subsections?.includes(section.id)) {
              parents.push(...findParentSections(parent.id));
            }
          }
          break;
        }
        if (section.subsections?.length) {
          const found = findInSubsections(pageId, section.subsections);
          if (found.length > 0) {
            parents.push(section.id, ...found);
            break;
          }
        }
      }
      return parents;
    };

    const findInSubsections = (pageId: string, subsectionIds: string[]): string[] => {
      for (const subId of subsectionIds) {
        const sub = wikiStructure.sections.find(s => s.id === subId);
        if (!sub) continue;
        if (sub.pages.includes(pageId)) return [sub.id];
        if (sub.subsections?.length) {
          const deeper = findInSubsections(pageId, sub.subsections);
          if (deeper.length > 0) return [sub.id, ...deeper];
        }
      }
      return [];
    };

    const parents = findParentSections(currentPageId);
    if (parents.length > 0) {
      setExpandedSections(prev => {
        const next = new Set(prev);
        parents.forEach(p => next.add(p));
        return next;
      });
    }
  }, [currentPageId, wikiStructure.sections]);

  const toggleSection = useCallback((sectionId: string, event: React.MouseEvent) => {
    event.stopPropagation();
    setExpandedSections(prev => {
      const newSet = new Set(prev);
      if (newSet.has(sectionId)) {
        newSet.delete(sectionId);
      } else {
        newSet.add(sectionId);
      }
      return newSet;
    });
  }, []);

  const renderSection = useCallback((sectionId: string, level = 0) => {
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
              <FaChevronDown className="text-xs transition-transform duration-200" />
            ) : (
              <FaChevronRight className="text-xs transition-transform duration-200" />
            )}
          </span>
          <span className="truncate">{section.title}</span>
        </button>

        <div
          className={`overflow-hidden transition-all duration-200 ease-in-out ${
            isExpanded ? 'max-h-[2000px] opacity-100' : 'max-h-0 opacity-0'
          }`}
        >
          <div
            className={`mt-0.5 space-y-0.5 ${
              level >= 0 ? 'pl-2 border-l border-[var(--border-color)]/30' : ''
            }`}
            style={{ marginLeft: `${level * 4 + 4}px` }}
          >
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
                    ></div>
                    <span className="truncate">{page.title}</span>
                  </div>
                </button>
              );
            })}

            {section.subsections?.map(subsectionId =>
              renderSection(subsectionId, level + 1)
            )}
          </div>
        </div>
      </div>
    );
  }, [wikiStructure, currentPageId, expandedSections, toggleSection, onPageSelect]);

  if (!wikiStructure.sections || wikiStructure.sections.length === 0 || !wikiStructure.rootSections || wikiStructure.rootSections.length === 0) {
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
                  ></div>
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
      {wikiStructure.rootSections.map(sectionId => {
        const section = wikiStructure.sections.find(s => s.id === sectionId);
        if (!section) return null;
        return renderSection(sectionId);
      })}
    </div>
  );
};

export default WikiTreeView;
