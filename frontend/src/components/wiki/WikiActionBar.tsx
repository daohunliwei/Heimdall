'use client';

import React, { useState } from 'react';
import { FaComments, FaTimes } from 'react-icons/fa';
import Ask from '@/components/Ask';

/**
 * WikiActionBar 浮动操作栏组件（V4 占位）。
 * 当前使用显式 props，后续可迁移至 RepositoryContext。
 */
interface WikiActionBarProps {
  repositoryId: string;
  provider?: string;
  model?: string;
  isCustomModel?: boolean;
  customModel?: string;
  language?: string;
  repositoryVersionId?: string;
  wikiVersionId?: string;
}

export default function WikiActionBar({
  repositoryId,
  provider = '',
  model = '',
  isCustomModel = false,
  customModel = '',
  language = 'zh',
  repositoryVersionId,
  wikiVersionId,
}: WikiActionBarProps) {
  const [isAskModalOpen, setIsAskModalOpen] = useState(false);

  React.useEffect(() => {
    const handleEsc = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsAskModalOpen(false);
    };
    if (isAskModalOpen) {
      window.addEventListener('keydown', handleEsc);
      return () => window.removeEventListener('keydown', handleEsc);
    }
  }, [isAskModalOpen]);

  return (
    <>
      <button
        onClick={() => setIsAskModalOpen(true)}
        className="fixed bottom-6 right-6 w-14 h-14 rounded-full bg-[var(--accent-primary)] text-white shadow-lg flex items-center justify-center hover:shadow-xl transition-all z-50 hover:scale-105"
        aria-label="向仓库提问"
      >
        <FaComments className="text-xl" />
      </button>

      {isAskModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setIsAskModalOpen(false)} />
          <div className="relative bg-[var(--card-bg)] rounded-xl shadow-2xl w-full max-w-2xl max-h-[80vh] flex flex-col border border-[var(--border-color)]">
            <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--border-color)]">
              <h3 className="font-semibold text-sm text-[var(--foreground)]">向仓库提问</h3>
              <button onClick={() => setIsAskModalOpen(false)} className="btn-ghost p-1.5 rounded-lg">
                <FaTimes />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-4">
              <Ask
                repositoryId={repositoryId}
                provider={provider}
                model={model}
                isCustomModel={isCustomModel}
                customModel={customModel}
                language={language}
                repositoryVersionId={repositoryVersionId}
                wikiVersionId={wikiVersionId}
                onRef={() => {}}
              />
            </div>
          </div>
        </div>
      )}
    </>
  );
}
