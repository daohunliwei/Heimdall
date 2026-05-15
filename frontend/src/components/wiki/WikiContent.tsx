'use client';

import React from 'react';
import { FaBookOpen, FaExclamationTriangle } from 'react-icons/fa';
import Markdown from '@/components/Markdown';

/**
 * WikiContent 正文展示组件（V4 占位）。
 * 当前直接从父组件 props 接收内容，后续可迁移至 RepositoryContext。
 */
interface WikiContentProps {
  error?: string | null;
  errorDetails?: string | null;
  currentPage?: {
    id: string;
    title: string;
    content: string;
    relatedPages?: string[];
  } | null;
  wikiStructure?: {
    pages: Array<{ id: string; title: string }>;
  } | null;
  onPageSelect?: (pageId: string) => void;
}

export default function WikiContent({
  error,
  errorDetails,
  currentPage,
  wikiStructure,
  onPageSelect,
}: WikiContentProps) {
  return (
    <div id="wiki-content" className="flex-1 min-h-0 overflow-y-auto">
      {error && (
        <div className="m-4 p-4 rounded-lg bg-[var(--highlight-light)] border border-[var(--highlight)]/20">
          <div className="flex items-center gap-2 text-[var(--highlight)] mb-2">
            <FaExclamationTriangle className="flex-shrink-0" />
            <span className="font-semibold text-sm">出错了</span>
          </div>
          <p className="text-sm text-[var(--foreground)] mb-3">{error}</p>
          {errorDetails && (
            <pre className="text-xs whitespace-pre-wrap break-words bg-[var(--background)]/70 border border-[var(--border-color)] rounded-md p-3 mb-3 overflow-x-auto">
              {errorDetails}
            </pre>
          )}
        </div>
      )}

      {currentPage ? (
        <div className="max-w-3xl mx-auto p-6 lg:p-8">
          <h3 className="text-xl font-bold text-[var(--foreground)] mb-6">{currentPage.title}</h3>
          <div className="prose prose-sm md:prose-base max-w-none">
            <Markdown content={currentPage.content} />
          </div>

          {currentPage.relatedPages && currentPage.relatedPages.length > 0 && (
            <div className="mt-10 pt-6 border-t border-[var(--border-color)]">
              <h4 className="text-xs font-semibold text-[var(--muted)] uppercase tracking-wider mb-3">
                相关页面：
              </h4>
              <div className="flex flex-wrap gap-2">
                {currentPage.relatedPages.map((relatedId: string) => {
                  const relatedPage = wikiStructure?.pages.find((p: { id: string }) => p.id === relatedId);
                  return relatedPage ? (
                    <button
                      key={relatedId}
                      className="tag tag-primary cursor-pointer hover:bg-[var(--accent-primary)]/15 transition-colors"
                      onClick={() => onPageSelect?.(relatedId)}
                    >
                      {relatedPage.title}
                    </button>
                  ) : null;
                })}
              </div>
            </div>
          )}
        </div>
      ) : (
        <div className="flex-1 flex items-center justify-center text-[var(--muted)] min-h-[300px]">
          <div className="text-center">
            <FaBookOpen className="text-3xl mx-auto mb-3 opacity-30" />
            <p className="text-sm">从左侧导航选择页面查看内容</p>
          </div>
        </div>
      )}
    </div>
  );
}
