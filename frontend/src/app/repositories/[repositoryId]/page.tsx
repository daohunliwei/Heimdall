'use client';

import Ask from '@/components/Ask';
import Markdown from '@/components/Markdown';
import ModelSelectionModal from '@/components/ModelSelectionModal';
import RefreshPanel, { RefreshOptions } from '@/components/RefreshPanel';
import ThemeToggle from '@/components/theme-toggle';
import VersionSwitcher from '@/components/VersionSwitcher';
import WikiTreeView from '@/components/WikiTreeView';
import { useLanguage } from '@/contexts/LanguageContext';
import { RepoInfo } from '@/types/repoinfo';
import { readJsonSafely } from '@/utils/response';
import { buildTaskRequestBody } from '@/utils/taskRequest';
import Link from 'next/link';
import { useParams, useSearchParams } from 'next/navigation';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { FaBitbucket, FaBookOpen, FaComments, FaDownload, FaExclamationTriangle, FaFileExport, FaFolder, FaGithub, FaGitlab, FaHome, FaSync, FaTimes } from 'react-icons/fa';

interface WikiSection {
  id: string;
  title: string;
  pages: string[];
  subsections?: string[];
}

interface WikiPage {
  id: string;
  title: string;
  description: string;
  content: string;
  filePaths: string[];
  importance: 'high' | 'medium' | 'low';
  relatedPages: string[];
  parentId?: string;
  isSection?: boolean;
  children?: string[];
}

interface WikiStructure {
  id: string;
  title: string;
  description: string;
  pages: WikiPage[];
  sections: WikiSection[];
  rootSections: string[];
}

interface ServerWikiCacheData {
  repository_id?: string;
  repo_url?: string;
  repo?: RepoInfo;
  provider?: string;
  model?: string;
  language?: string;
  wikiStructure?: WikiStructure;
  generatedPages?: Record<string, WikiPage>;
}

interface RepositoryDetail {
  repository_id: string;
  display_name: string;
  owner: string;
  repo_name: string;
  provider_type: string;
  repo_type: string;
  repo_url: string;
  default_branch: string;
  default_language: string;
  is_archived: boolean;
}

interface TaskErrorResponse {
  error?: string;
  details?: string;
  request_id?: string;
}

interface WikiTaskDebugInfo {
  request_id?: string;
  repository_path?: string;
  file_count?: number;
  structure_page_count?: number;
  generated_page_count?: number;
  fallback_used?: boolean;
  structure_response_preview?: string;
  warnings?: string[];
}

function getRepositoryIcon(repoType: string) {
  if (repoType === 'github') return FaGithub;
  if (repoType === 'gitlab') return FaGitlab;
  return FaBitbucket;
}

export default function RepositoryWikiPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const repositoryId = params.repositoryId as string;
  const providerParam = searchParams.get('provider') || '';
  const modelParam = searchParams.get('model') || '';
  const isCustomModelParam = searchParams.get('is_custom_model') === 'true';
  const customModelParam = searchParams.get('custom_model') || '';
  const language = searchParams.get('language') || 'zh';
  const isComprehensiveParam = searchParams.get('comprehensive') !== 'false';
  const { messages } = useLanguage();

  const [repoDetail, setRepoDetail] = useState<RepositoryDetail | null>(null);
  const [effectiveRepoInfo, setEffectiveRepoInfo] = useState<RepoInfo>({
    owner: '', repo: '', type: 'github', token: null, localPath: null, repoUrl: null,
  });
  const [selectedProviderState, setSelectedProviderState] = useState(providerParam);
  const [selectedModelState, setSelectedModelState] = useState(modelParam);
  const [isCustomSelectedModelState, setIsCustomSelectedModelState] = useState(isCustomModelParam);
  const [customSelectedModelState, setCustomSelectedModelState] = useState(customModelParam);
  const [isComprehensiveView, setIsComprehensiveView] = useState(isComprehensiveParam);
  const [isLoading, setIsLoading] = useState(true);
  const [loadingMessage, setLoadingMessage] = useState<string | undefined>(
    messages.loading?.initializing || '正在初始化 Wiki 任务...'
  );
  const [error, setError] = useState<string | null>(null);
  const [errorDetails, setErrorDetails] = useState<string | null>(null);
  const [wikiDebug, setWikiDebug] = useState<WikiTaskDebugInfo | null>(null);
  const [wikiStructure, setWikiStructure] = useState<WikiStructure>();
  const [generatedPages, setGeneratedPages] = useState<Record<string, WikiPage>>({});
  const [currentPageId, setCurrentPageId] = useState<string>();
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [isAskModalOpen, setIsAskModalOpen] = useState(false);
  const [isModelSelectionModalOpen, setIsModelSelectionModalOpen] = useState(false);
  const [currentWikiVersionId, setCurrentWikiVersionId] = useState<string | undefined>();
  const askComponentRef = useRef<{ clearConversation: () => void } | null>(null);
  const initialLoadKeyRef = useRef('');

  // 加载仓库详情
  const loadRepoDetail = useCallback(async () => {
    try {
      const resp = await fetch(`/api/repositories/${repositoryId}`);
      if (resp.ok) {
        const detail = await resp.json() as RepositoryDetail;
        setRepoDetail(detail);
        setEffectiveRepoInfo({
          owner: detail.owner,
          repo: detail.repo_name,
          type: detail.repo_type,
          token: null,
          localPath: null,
          repoUrl: detail.repo_url,
        });
      }
    } catch (e) {
      console.error('加载仓库详情失败', e);
    }
  }, [repositoryId]);

  const applyWikiPayload = useCallback((payload: {
    repo?: RepoInfo; provider?: string; model?: string;
    wikiStructure: WikiStructure; generatedPages: Record<string, WikiPage>;
    debug?: WikiTaskDebugInfo;
  }) => {
    if (payload.repo) setEffectiveRepoInfo(payload.repo);
    setWikiStructure(payload.wikiStructure);
    setGeneratedPages(payload.generatedPages || {});
    setWikiDebug(payload.debug || null);
    setCurrentPageId((previousPageId) => {
      if (previousPageId && payload.generatedPages?.[previousPageId]) return previousPageId;
      return payload.wikiStructure.pages[0]?.id;
    });
    if (payload.provider) setSelectedProviderState(payload.provider);
    if (payload.model) setSelectedModelState(payload.model);
  }, []);

  const loadWikiFromCache = useCallback(async (): Promise<boolean> => {
    setLoadingMessage(messages.loading?.fetchingCache || '正在读取缓存 Wiki...');
    const cacheResp = await fetch(`/api/repositories/${repositoryId}/wiki?language=${language}`);
    if (!cacheResp.ok) return false;
    const cachedData = await readJsonSafely<ServerWikiCacheData>(cacheResp);
    if (!cachedData?.wikiStructure) return false;
    applyWikiPayload({
      repo: cachedData.repo, provider: cachedData.provider, model: cachedData.model,
      wikiStructure: cachedData.wikiStructure, generatedPages: cachedData.generatedPages || {},
    });
    return true;
  }, [applyWikiPayload, repositoryId, language, messages.loading]);

  const generateWikiTask = useCallback(async (options?: { forceRefresh?: boolean }) => {
    const forceRefresh = options?.forceRefresh ?? false;
    setIsLoading(true);
    setError(null);
    setErrorDetails(null);
    setExportError(null);
    setLoadingMessage(messages.loading?.initializing || '正在调用后端生成 Wiki...');

    try {
      const requestBody = buildTaskRequestBody({
        token: null,
        provider: selectedProviderState, model: selectedModelState,
        isCustomModel: isCustomSelectedModelState, customModel: customSelectedModelState,
        language,
      }, { comprehensive: isComprehensiveView, force_refresh: forceRefresh });

      const bodyWithRepoId = { ...requestBody, repository_id: repositoryId };

      const response = await fetch('/api/tasks/wiki', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(bodyWithRepoId),
      });

      if (!response.ok) {
        const errorBody = await readJsonSafely<TaskErrorResponse>(response);
        const detailText = [
          errorBody?.details,
          errorBody?.request_id ? `RequestId: ${errorBody.request_id}` : null,
        ].filter(Boolean).join('\n');
        if (detailText) setErrorDetails(detailText);
        throw new Error(errorBody?.error || `Wiki 生成失败：${response.status}`);
      }

      const data = await response.json() as { task_id: string; status: string; message: string };
      const taskId = data.task_id;

      if (data.status === 'completed') {
        setLoadingMessage(messages.loading?.fetchingCache || '正在加载已完成 Wiki...');
        const loaded = await loadWikiFromCache();
        if (loaded) { setIsLoading(false); setLoadingMessage(undefined); return; }
      }

      setLoadingMessage(data.message || '任务已接收，后台处理中...');
      let pollCount = 0;
      const maxPolls = 360;
      while (pollCount < maxPolls) {
        await new Promise(resolve => setTimeout(resolve, 5000));
        pollCount++;

        const statusResp = await fetch(`/api/tasks/${taskId}/status`);
        if (!statusResp.ok) continue;

        const statusData = await statusResp.json() as {
          id: string; status: string; progress_percent: number;
          progress_message?: string; error_message?: string;
        };

        if (statusData.progress_message) {
          setLoadingMessage(statusData.progress_message);
        }

        if (statusData.status === 'completed') {
          setLoadingMessage(messages.loading?.fetchingCache || '正在加载 Wiki 数据...');
          const loaded = await loadWikiFromCache();
          if (loaded) { setIsLoading(false); setLoadingMessage(undefined); return; }
          throw new Error('Wiki 生成完成，但缓存加载失败');
        }

        if (statusData.status === 'failed') {
          throw new Error(statusData.error_message || 'Wiki 生成失败');
        }

        if (statusData.status === 'cancelled') {
          throw new Error('任务已取消');
        }
      }

      throw new Error('任务超时：Wiki 生成超过 30 分钟');
    } catch (err) {
      console.error('Error generating wiki:', err);
      setError(err instanceof Error ? err.message : 'Wiki 生成失败');
    } finally {
      setIsLoading(false);
      setLoadingMessage(undefined);
    }
  }, [applyWikiPayload, loadWikiFromCache, customSelectedModelState, effectiveRepoInfo,
    isComprehensiveView, isCustomSelectedModelState, language, messages.loading,
    selectedModelState, selectedProviderState, repositoryId]);

  const loadInitialData = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      await loadRepoDetail();
      const loadedFromCache = await loadWikiFromCache();
      if (loadedFromCache) { setIsLoading(false); setLoadingMessage(undefined); return; }
      await generateWikiTask();
    } catch (err) {
      console.error('Error loading wiki data:', err);
      setError(err instanceof Error ? err.message : '加载 Wiki 失败');
      setIsLoading(false);
      setLoadingMessage(undefined);
    }
  }, [generateWikiTask, loadWikiFromCache, loadRepoDetail]);

  const exportWiki = useCallback(async (format: 'markdown' | 'json') => {
    if (!wikiStructure || Object.keys(generatedPages).length === 0) {
      setExportError('暂无可导出的 Wiki 内容');
      return;
    }
    try {
      setIsExporting(true);
      setExportError(null);
      const pagesToExport = wikiStructure.pages.map((page) => ({
        ...page, content: generatedPages[page.id]?.content || '',
      }));
      const response = await fetch('/export/wiki', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          repository_id: repositoryId,
          pages: pagesToExport, format,
        }),
      });
      if (!response.ok) {
        const errorText = await response.text().catch(() => '未知错误');
        throw new Error(`导出失败：${response.status} - ${errorText}`);
      }
      const contentDisposition = response.headers.get('Content-Disposition');
      let filename = `${effectiveRepoInfo.repo}_wiki.${format === 'markdown' ? 'md' : 'json'}`;
      const filenameMatch = contentDisposition?.match(/filename=(.+)/);
      if (filenameMatch?.[1]) filename = filenameMatch[1].replace(/"/g, '');
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url; anchor.download = filename;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      window.URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Error exporting wiki:', err);
      setExportError(err instanceof Error ? err.message : '导出 Wiki 失败');
    } finally { setIsExporting(false); }
  }, [effectiveRepoInfo, generatedPages, wikiStructure]);

  const confirmRefresh = useCallback(async () => {
    setIsLoading(true); setError(null); setErrorDetails(null);
    setLoadingMessage(messages.loading?.clearingCache || '正在清理缓存并重新生成 Wiki...');
    try {
      await fetch(`/api/repositories/${repositoryId}/wiki?language=${language}`, { method: 'DELETE' });
      await generateWikiTask({ forceRefresh: true });
    } catch (err) {
      console.error('Error refreshing wiki:', err);
      setIsLoading(false); setLoadingMessage(undefined);
      setError(err instanceof Error ? err.message : '刷新 Wiki 失败');
    }
  }, [repositoryId, language, generateWikiTask, messages.loading]);

  const handleVersionChange = useCallback(async (wikiVersionId: string, _repositoryVersionId: string) => {
    setCurrentWikiVersionId(wikiVersionId);
    // 按版本加载页面内容
    try {
      const resp = await fetch(`/api/repositories/${repositoryId}/wiki/pages?wikiVersionId=${wikiVersionId}&language=${language}`);
      if (resp.ok) {
        const pages = await resp.json() as WikiPage[];
        const pageMap: Record<string, WikiPage> = {};
        pages.forEach((p: WikiPage) => { pageMap[p.id] = p; });
        setGeneratedPages(pageMap);
      }
    } catch (e) {
      console.error('加载版本页面失败', e);
    }
  }, [repositoryId, language]);

  const handleRefreshWithOptions = useCallback(async (options: RefreshOptions) => {
    setIsLoading(true);
    setError(null);
    setErrorDetails(null);
    setLoadingMessage('正在刷新 Wiki...');

    try {
      const body = {
        branch: options.branch,
        refresh_strategy: options.refreshStrategy,
        force_refresh: options.forceRefresh,
        generation_profile: options.generationProfile,
        provider: options.provider,
        model: options.model,
        language,
      };

      const resp = await fetch(`/api/repositories/${repositoryId}/wiki/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });

      if (resp.ok) {
        const result = await resp.json() as { result_type: string; message: string };
        if (result.result_type === 'no_change') {
          setIsLoading(false);
          setLoadingMessage(undefined);
          return;
        }
      }

      // 执行完整刷新
      await generateWikiTask({ forceRefresh: options.forceRefresh });
    } catch (err) {
      console.error('刷新失败', err);
      setIsLoading(false);
      setLoadingMessage(undefined);
      setError(err instanceof Error ? err.message : '刷新失败');
    }
  }, [repositoryId, language, generateWikiTask]);

  const handlePageSelect = useCallback((pageId: string) => { setCurrentPageId(pageId); }, []);

  useEffect(() => {
    const wikiContent = document.getElementById('wiki-content');
    wikiContent?.scrollTo({ top: 0, behavior: 'smooth' });
  }, [currentPageId]);

  useEffect(() => {
    const handleEsc = (event: KeyboardEvent) => { if (event.key === 'Escape') setIsAskModalOpen(false); };
    if (isAskModalOpen) { window.addEventListener('keydown', handleEsc); return () => window.removeEventListener('keydown', handleEsc); }
  }, [isAskModalOpen]);

  useEffect(() => {
    const loadKey = [repositoryId, language, isComprehensiveParam ? 'comprehensive' : 'concise'].join('|');
    if (initialLoadKeyRef.current === loadKey) return;
    initialLoadKeyRef.current = loadKey;
    loadInitialData();
  }, [isComprehensiveParam, language, loadInitialData, repositoryId]);

  const currentPage = currentPageId ? generatedPages[currentPageId] : undefined;
  const displayName = repoDetail?.display_name || effectiveRepoInfo.owner ? `${effectiveRepoInfo.owner}/${effectiveRepoInfo.repo}` : '...';
  const RepositoryIcon = getRepositoryIcon(effectiveRepoInfo.type);

  return (
    <div className="h-screen flex flex-col bg-[var(--background)]">
      {/* Top nav */}
      <header className="h-12 flex items-center justify-between px-4 border-b border-[var(--border-color)] bg-[var(--background)]/80 backdrop-blur-md flex-shrink-0">
        <Link href="/" className="flex items-center gap-1.5 text-sm text-[var(--muted)] hover:text-[var(--foreground)] transition-colors">
          <FaHome className="text-xs" /> {messages.repoPage?.home || 'Home'}
        </Link>
        <ThemeToggle />
      </header>

      <main className="flex-1 min-h-0 flex flex-col">
        {isLoading ? (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <div className="flex items-center justify-center gap-1.5 mb-4">
                <div className="w-2.5 h-2.5 rounded-full bg-[var(--accent-primary)] animate-bounce" />
                <div className="w-2.5 h-2.5 rounded-full bg-[var(--accent-primary)] animate-bounce" style={{ animationDelay: '0.1s' }} />
                <div className="w-2.5 h-2.5 rounded-full bg-[var(--accent-primary)] animate-bounce" style={{ animationDelay: '0.2s' }} />
              </div>
              <p className="text-sm text-[var(--foreground)] font-medium">{loadingMessage || messages.common?.loading || 'Loading...'}</p>
              <p className="text-xs text-[var(--muted)] mt-2">关闭当前页面后，后端仍会继续生成</p>
            </div>
          </div>
        ) : wikiStructure ? (
          <div className="flex-1 min-h-0 flex flex-col lg:flex-row">
            {/* Sidebar */}
            <aside className="w-full lg:w-72 xl:w-80 flex-shrink-0 border-b lg:border-b-0 lg:border-r border-[var(--border-color)] bg-[var(--card-bg)] flex flex-col min-h-0">
              <div className="p-4 border-b border-[var(--border-color)]">
                <h3 className="font-semibold text-[var(--foreground)] text-sm truncate">{wikiStructure.title}</h3>
                <p className="text-xs text-[var(--muted)] mt-1 line-clamp-2">{wikiStructure.description}</p>
              </div>

              <div className="p-4 border-b border-[var(--border-color)] space-y-2">
                <div className="flex items-center gap-2 text-xs text-[var(--muted)]">
                  {effectiveRepoInfo.type === 'local' ? (
                    <>
                      <FaFolder className="flex-shrink-0" />
                      <span className="truncate">{effectiveRepoInfo.localPath}</span>
                    </>
                  ) : (
                    <>
                      <RepositoryIcon className="flex-shrink-0" />
                      <a href={effectiveRepoInfo.repoUrl ?? ''} target="_blank" rel="noopener noreferrer"
                        className="text-[var(--accent-primary)] hover:underline truncate">
                        {displayName}
                      </a>
                    </>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <span className="tag tag-primary">
                    {isComprehensiveView ? (messages.form?.comprehensive || 'Comprehensive') : (messages.form?.concise || 'Concise')}
                  </span>
                  {wikiDebug && (
                    <span className="tag tag-default">{wikiDebug.file_count ?? 0} files</span>
                  )}
                </div>
              </div>

              <div className="p-4 border-b border-[var(--border-color)] space-y-2">
                <VersionSwitcher
                  repositoryId={repositoryId}
                  currentWikiVersionId={currentWikiVersionId}
                  onVersionChange={handleVersionChange}
                />
                <RefreshPanel
                  repositoryId={repositoryId}
                  defaultBranch={repoDetail?.default_branch || 'main'}
                  onRefresh={handleRefreshWithOptions}
                  isLoading={isLoading}
                />
                <button onClick={() => setIsModelSelectionModalOpen(true)} disabled={isLoading}
                  className="btn-secondary w-full text-xs justify-center">
                  <FaSync className={isLoading ? 'animate-spin' : ''} />
                  {messages.repoPage?.refreshWiki || 'Refresh Wiki'}
                </button>

                {Object.keys(generatedPages).length > 0 && (
                  <div className="flex gap-1.5">
                    <button onClick={() => exportWiki('markdown')} disabled={isExporting}
                      className="btn-primary flex-1 text-xs justify-center py-1.5">
                      <FaDownload className="text-[10px]" /> Markdown
                    </button>
                    <button onClick={() => exportWiki('json')} disabled={isExporting}
                      className="btn-secondary flex-1 text-xs justify-center py-1.5">
                      <FaFileExport className="text-[10px]" /> JSON
                    </button>
                  </div>
                )}
                {exportError && <p className="text-xs text-[var(--highlight)]">{exportError}</p>}
              </div>

              <div className="flex-1 min-h-0 overflow-y-auto p-4">
                <h4 className="text-xs font-semibold text-[var(--muted)] uppercase tracking-wider mb-3">
                  {messages.repoPage?.pages || 'Pages'}
                </h4>
                <WikiTreeView
                  wikiStructure={wikiStructure}
                  currentPageId={currentPageId}
                  onPageSelect={handlePageSelect}
                  messages={messages.repoPage}
                />
              </div>
            </aside>

            {/* Content area */}
            <div id="wiki-content" className="flex-1 min-h-0 overflow-y-auto">
              {error && (
                <div className="m-4 p-4 rounded-lg bg-[var(--highlight-light)] border border-[var(--highlight)]/20">
                  <div className="flex items-center gap-2 text-[var(--highlight)] mb-2">
                    <FaExclamationTriangle className="flex-shrink-0" />
                    <span className="font-semibold text-sm">{messages.repoPage?.errorTitle || 'Error'}</span>
                  </div>
                  <p className="text-sm text-[var(--foreground)] mb-3">{error}</p>
                  {errorDetails && (
                    <pre className="text-xs whitespace-pre-wrap break-words bg-[var(--background)]/70 border border-[var(--border-color)] rounded-md p-3 mb-3 overflow-x-auto">{errorDetails}</pre>
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
                        {messages.repoPage?.relatedPages || 'Related Pages:'}
                      </h4>
                      <div className="flex flex-wrap gap-2">
                        {currentPage.relatedPages?.map((relatedId) => {
                          const relatedPage = wikiStructure.pages.find((page) => page.id === relatedId);
                          return relatedPage ? (
                            <button key={relatedId}
                              className="tag tag-primary cursor-pointer hover:bg-[var(--accent-primary)]/15 transition-colors"
                              onClick={() => handlePageSelect(relatedId)}>
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
                    <p className="text-sm">{messages.repoPage?.selectPagePrompt || 'Select a page from the navigation to view its content'}</p>
                  </div>
                </div>
              )}
            </div>
          </div>
        ) : error ? (
          <div className="flex-1 flex items-center justify-center p-8">
            <div className="max-w-lg w-full p-6 rounded-lg bg-[var(--highlight-light)] border border-[var(--highlight)]/20">
              <div className="flex items-center gap-2 text-[var(--highlight)] mb-3">
                <FaExclamationTriangle />
                <span className="font-semibold">{messages.repoPage?.errorTitle || 'Error'}</span>
              </div>
              <p className="text-sm text-[var(--foreground)] mb-4">{error}</p>
              {errorDetails && (
                <pre className="text-xs whitespace-pre-wrap break-words bg-[var(--background)]/70 border border-[var(--border-color)] rounded-md p-3 mb-4 overflow-x-auto">{errorDetails}</pre>
              )}
              <Link href="/" className="btn-primary text-sm inline-flex">
                <FaHome /> {messages.repoPage?.backToHome || 'Back to Home'}
              </Link>
            </div>
          </div>
        ) : null}
      </main>

      {/* Floating AI chat button */}
      {!isLoading && wikiStructure && (
        <button
          onClick={() => setIsAskModalOpen(true)}
          className="fixed bottom-6 right-6 w-13 h-13 rounded-full gradient-accent text-white shadow-lg flex items-center justify-center hover:shadow-xl transition-all z-50 hover:scale-105"
          aria-label={messages.ask?.title || 'Ask about this repository'}
        >
          <FaComments className="text-xl" />
        </button>
      )}

      {/* AI Chat modal */}
      {isAskModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setIsAskModalOpen(false)} />
          <div className="relative bg-[var(--card-bg)] rounded-xl shadow-2xl w-full max-w-2xl max-h-[80vh] flex flex-col border border-[var(--border-color)]">
            <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--border-color)]">
              <h3 className="font-semibold text-sm text-[var(--foreground)]">{messages.ask?.title || 'Ask about this repository'}</h3>
              <button onClick={() => setIsAskModalOpen(false)}
                className="btn-ghost p-1.5 rounded-lg">
                <FaTimes />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-4">
              <Ask
                repositoryId={repositoryId}
                provider={selectedProviderState} model={selectedModelState}
                isCustomModel={isCustomSelectedModelState} customModel={customSelectedModelState}
                language={language}
                onRef={(ref) => { askComponentRef.current = ref; }}
              />
            </div>
          </div>
        </div>
      )}

      {/* Model Selection Modal */}
      <ModelSelectionModal
        isOpen={isModelSelectionModalOpen}
        onClose={() => setIsModelSelectionModalOpen(false)}
        provider={selectedProviderState} setProvider={setSelectedProviderState}
        model={selectedModelState} setModel={setSelectedModelState}
        isCustomModel={isCustomSelectedModelState} setIsCustomModel={setIsCustomSelectedModelState}
        customModel={customSelectedModelState} setCustomModel={setCustomSelectedModelState}
        isComprehensiveView={isComprehensiveView} setIsComprehensiveView={setIsComprehensiveView}
        showFileFilters={false}
        excludedDirs={''} setExcludedDirs={() => {}}
        excludedFiles={''} setExcludedFiles={() => {}}
        includedDirs={''} setIncludedDirs={() => {}}
        includedFiles={''} setIncludedFiles={() => {}}
        onApply={confirmRefresh}
        showWikiType={true}
        showTokenInput={false}
        repositoryType={effectiveRepoInfo.type as 'github' | 'gitlab' | 'bitbucket'}
        authRequired={false} authCode={''} setAuthCode={() => {}}
        isAuthLoading={false}
      />
    </div>
  );
}
