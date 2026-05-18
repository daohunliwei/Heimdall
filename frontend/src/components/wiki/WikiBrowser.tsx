'use client';

import Link from 'next/link';
import { FaHome } from 'react-icons/fa';
import ThemeToggle from '@/components/theme-toggle';
import LoadingState from '@/components/ui/LoadingState';

/**
 * WikiBrowser 主容器组件（V4 占位）。
 * 当前阶段保持与现有 page.tsx 结构兼容，后续渐进式迁移。
 */
interface WikiBrowserProps {
  displayName: string;
  loadingMessage?: string;
  activeTaskId: string | null;
  isLoading: boolean;
  hasContent: boolean;
  repositoryId: string;
  children?: React.ReactNode;
}

export default function WikiBrowser({
  displayName: _displayName,
  loadingMessage,
  activeTaskId,
  isLoading,
  hasContent: _hasContent,
  repositoryId: _repositoryId,
  children,
}: WikiBrowserProps) {
  return (
    <div className="h-screen flex flex-col bg-[var(--background)]">
      <header className="h-12 flex items-center justify-between px-4 border-b border-[var(--border-color)] bg-[var(--background)]/80 backdrop-blur-md flex-shrink-0">
        <Link href="/" className="flex items-center gap-1.5 text-sm text-[var(--muted)] hover:text-[var(--foreground)] transition-colors">
          <FaHome className="text-xs" /> 首页
        </Link>
        <ThemeToggle />
      </header>
      <main className="flex-1 min-h-0 flex flex-col">
        {isLoading ? (
          <LoadingState message={loadingMessage || '加载中...'} taskId={activeTaskId ?? undefined} />
        ) : (
          children
        )}
      </main>
    </div>
  );
}
