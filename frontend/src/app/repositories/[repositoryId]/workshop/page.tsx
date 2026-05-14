'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { FaArrowLeft, FaSync, FaDownload } from 'react-icons/fa';
import ThemeToggle from '@/components/theme-toggle';
import Markdown from '@/components/Markdown';
import { useLanguage } from '@/contexts/LanguageContext';
import { buildTaskRequestBody } from '@/utils/taskRequest';

interface WorkshopTaskResponse {
  content: string;
}

export default function WorkshopPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const repositoryId = params.repositoryId as string;
  const providerParam = searchParams.get('provider') || '';
  const modelParam = searchParams.get('model') || '';
  const isCustomModelParam = searchParams.get('is_custom_model') === 'true';
  const customModelParam = searchParams.get('custom_model') || '';
  const language = searchParams.get('language') || 'zh';
  const { messages } = useLanguage();

  const [repo, setRepo] = useState<string>('');
  const [isLoading, setIsLoading] = useState(false);
  const [loadingMessage, setLoadingMessage] = useState<string | undefined>(
    messages.loading?.initializing || '正在初始化训练营任务...'
  );
  const [error, setError] = useState<string | null>(null);
  const [workshopContent, setWorkshopContent] = useState<string>('');
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const generateWorkshopContent = useCallback(async () => {
    if (isLoading) return;
    setIsLoading(true); setError(null); setWorkshopContent('');
    setLoadingMessage(messages.loading?.generatingWorkshop || '正在调用后端生成训练营内容...');
    try {
      const requestBody = buildTaskRequestBody({
        token: null, provider: providerParam, model: modelParam,
        isCustomModel: isCustomModelParam, customModel: customModelParam, language,
      }, { comprehensive: true });

      const bodyWithRepoId = { ...requestBody, repository_id: repositoryId };
      const response = await fetch('/api/tasks/workshop', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(bodyWithRepoId),
      });
      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({ error: '生成 Workshop 失败' }));
        throw new Error(errorBody.error || `生成 Workshop 失败：${response.status}`);
      }
      const data = await response.json() as WorkshopTaskResponse;
      setWorkshopContent(data.content || '');
    } catch (err) {
      console.error('Error generating workshop content:', err);
      setError(err instanceof Error ? err.message : '生成 Workshop 失败');
    } finally { setIsLoading(false); setLoadingMessage(undefined); }
  }, [providerParam, modelParam, isCustomModelParam, customModelParam, language, isLoading, messages.loading, repositoryId]);

  const exportWorkshop = useCallback(async () => {
    if (!workshopContent) { setExportError('暂无可导出的训练营内容'); return; }
    try {
      setIsExporting(true); setExportError(null);
      const blob = new Blob([workshopContent], { type: 'text/markdown' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a'); a.href = url; a.download = `${repo}_workshop.md`;
      document.body.appendChild(a); a.click();
      window.URL.revokeObjectURL(url); document.body.removeChild(a);
    } catch (err) {
      console.error('Error exporting workshop:', err);
      setExportError(err instanceof Error ? err.message : '导出训练营内容失败');
    } finally { setIsExporting(false); }
  }, [workshopContent, repo]);

  useEffect(() => {
    const loadRepo = async () => {
      try {
        const resp = await fetch(`/api/repositories/${repositoryId}`);
        if (resp.ok) {
          const detail = await resp.json();
          setRepo(detail.repo_name || detail.display_name || repositoryId);
        }
      } catch { setRepo(repositoryId); }
    };
    loadRepo();
  }, [repositoryId]);

  const contentGeneratedRef = useRef(false);
  useEffect(() => {
    if (!contentGeneratedRef.current) { contentGeneratedRef.current = true; generateWorkshopContent(); }
  }, [generateWorkshopContent]);

  return (
    <div className="min-h-screen flex flex-col bg-[var(--background)]">
      <header className="sticky top-0 z-10 bg-[var(--card-bg)] border-b border-[var(--border-color)] shadow-sm">
        <div className="container mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <Link href={`/repositories/${repositoryId}`} className="flex items-center text-[var(--foreground)] hover:text-[var(--accent-primary)] transition-colors">
              <FaArrowLeft className="mr-2" />
              <span>{messages.workshop?.backToWiki || 'Back to Wiki'}</span>
            </Link>
            <h1 className="text-xl font-bold text-[var(--accent-primary)]">
              {messages.workshop?.title || 'Workshop'}: {repo}
            </h1>
          </div>
          <div className="flex items-center space-x-3">
            <button onClick={generateWorkshopContent} disabled={isLoading}
              className={`p-2 rounded-md ${isLoading ? 'bg-[var(--button-disabled-bg)] text-[var(--button-disabled-text)]' : 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20'} transition-colors`}>
              <FaSync className={`${isLoading ? 'animate-spin' : ''}`} />
            </button>
            <button onClick={exportWorkshop} disabled={!workshopContent || isExporting}
              className={`p-2 rounded-md ${!workshopContent || isExporting ? 'bg-[var(--button-disabled-bg)] text-[var(--button-disabled-text)]' : 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] hover:bg-[var(--accent-primary)]/20'} transition-colors`}>
              <FaDownload />
            </button>
            <ThemeToggle />
          </div>
        </div>
      </header>
      <main className="flex-1 container mx-auto px-4 py-6">
        {isLoading && !workshopContent ? (
          <div className="flex flex-col items-center justify-center p-8">
            <div className="w-12 h-12 border-4 border-[var(--accent-primary)]/30 border-t-[var(--accent-primary)] rounded-full animate-spin mb-4"></div>
            <p className="text-[var(--foreground)]">{loadingMessage}</p>
          </div>
        ) : error ? (
          <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-md p-4 mb-6">
            <h3 className="text-red-800 dark:text-red-400 font-medium mb-2">{messages.common?.error || 'Error'}</h3>
            <p className="text-red-700 dark:text-red-300">{error}</p>
          </div>
        ) : (
          <div className="bg-[var(--card-bg)] border border-[var(--border-color)] rounded-lg shadow-sm p-6">
            {exportError && (
              <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-md p-3 mb-4">
                <p className="text-red-700 dark:text-red-300 text-sm">{exportError}</p>
              </div>
            )}
            <Markdown content={workshopContent} />
          </div>
        )}
      </main>
    </div>
  );
}
