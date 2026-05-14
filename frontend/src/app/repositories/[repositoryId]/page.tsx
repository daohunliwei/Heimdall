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
import {
  buildWikiViewFromVersionPages,
  RepositoryVersionSummary,
  WikiPage,
  WikiStructure,
  WikiVersionPagePayload,
  WikiVersionSummary,
} from '@/types/wiki';
import { readJsonSafely } from '@/utils/response';
import { buildTaskRequestBody } from '@/utils/taskRequest';
import Link from 'next/link';
import { useParams, useSearchParams } from 'next/navigation';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { FaBitbucket, FaBookOpen, FaComments, FaDownload, FaExclamationTriangle, FaFileExport, FaFolder, FaGithub, FaGitlab, FaHome, FaSync, FaTimes } from 'react-icons/fa';

/**
 * 仓库详情响应。
 */
interface RepositoryDetail {
  /** 仓库 ID。 */
  repository_id: string;
  /** 展示名称。 */
  display_name: string;
  /** 所有者。 */
  owner: string;
  /** 仓库名。 */
  repo_name: string;
  /** Provider 类型。 */
  provider_type: string;
  /** 仓库类型。 */
  repo_type: string;
  /** 仓库地址。 */
  repo_url: string;
  /** 默认分支。 */
  default_branch: string;
  /** 默认语言。 */
  default_language: string;
  /** 是否归档。 */
  is_archived: boolean;
}

/**
 * 错误响应结构。
 */
interface TaskErrorResponse {
  /** 错误消息。 */
  error?: string;
  /** 详细错误。 */
  details?: string;
  /** 请求 ID。 */
  request_id?: string;
}

/**
 * 刷新接口统一响应结构。
 */
interface WikiRefreshResponse {
  /** 关联的仓库快照版本 ID。 */
  repositoryVersionId?: string;
  /** 关联的 Wiki 版本 ID。 */
  wikiVersionId?: string;
  /** 刷新结果类型。 */
  resultType: string;
  /** 仓库变化状态。 */
  changeStatus?: string;
  /** 任务 ID。 */
  taskId?: string;
  /** 接口返回消息。 */
  message?: string;
}

/**
 * 任务创建响应。
 */
interface WikiTaskExecutionResponse {
  /** 任务 ID。 */
  task_id: string;
  /** 任务状态。 */
  status: string;
  /** 后端返回消息。 */
  message: string;
}

/**
 * 任务状态响应。
 */
interface WikiTaskStatusResponse {
  /** 任务 ID。 */
  id: string;
  /** 任务状态。 */
  status: string;
  /** 进度百分比。 */
  progress_percent: number;
  /** 进度说明。 */
  progress_message?: string;
  /** 错误信息。 */
  error_message?: string;
}

/**
 * 任务执行时使用的模型参数。
 */
interface WikiTaskExecutionOptions {
  /** 使用的 Provider。 */
  provider: string;
  /** 使用的模型。 */
  model: string;
  /** 是否使用自定义模型。 */
  isCustomModel: boolean;
  /** 自定义模型名。 */
  customModel: string;
}

/**
 * 任务完成后希望优先命中的版本信息。
 */
interface TaskCompletionTarget {
  /** 预期的 Wiki 版本 ID。 */
  wikiVersionId?: string;
  /** 预期的仓库快照版本 ID。 */
  repositoryVersionId?: string;
}

/**
 * 读取多个候选值中的第一个非空字符串。
 */
function pickStringValue(...values: unknown[]): string | undefined {
  return values.find((value) => typeof value === 'string' && value.trim().length > 0) as string | undefined;
}

/**
 * 统一解析 `/wiki/refresh` 返回字段。
 * 当前后端仍存在 camelCase / snake_case / PascalCase 混用的过渡期，
 * 因此前端在阶段 0 先做兼容，避免再次退回旧链路。
 */
function normalizeRefreshResponse(payload: Record<string, unknown>): WikiRefreshResponse {
  return {
    repositoryVersionId: pickStringValue(
      payload.repositoryVersionId,
      payload.repository_version_id,
      payload.RepositoryVersionId,
    ),
    wikiVersionId: pickStringValue(
      payload.wikiVersionId,
      payload.wiki_version_id,
      payload.WikiVersionId,
    ),
    resultType: pickStringValue(
      payload.resultType,
      payload.result_type,
      payload.ResultType,
    ) ?? 'queued',
    changeStatus: pickStringValue(
      payload.changeStatus,
      payload.change_status,
      payload.ChangeStatus,
    ),
    taskId: pickStringValue(
      payload.taskId,
      payload.task_id,
      payload.TaskId,
    ),
    message: pickStringValue(payload.message, payload.Message),
  };
}

/**
 * 根据仓库类型选择图标。
 */
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
    owner: '',
    repo: '',
    type: 'github',
    token: null,
    localPath: null,
    repoUrl: null,
  });
  const [selectedProviderState, setSelectedProviderState] = useState(providerParam);
  const [selectedModelState, setSelectedModelState] = useState(modelParam);
  const [isCustomSelectedModelState, setIsCustomSelectedModelState] = useState(isCustomModelParam);
  const [customSelectedModelState, setCustomSelectedModelState] = useState(customModelParam);
  const [isComprehensiveView, setIsComprehensiveView] = useState(isComprehensiveParam);
  const [isLoading, setIsLoading] = useState(true);
  const [loadingMessage, setLoadingMessage] = useState<string | undefined>(
    messages.loading?.initializing || '正在初始化 Wiki 页面...'
  );
  const [error, setError] = useState<string | null>(null);
  const [errorDetails, setErrorDetails] = useState<string | null>(null);
  const [wikiStructure, setWikiStructure] = useState<WikiStructure>();
  const [generatedPages, setGeneratedPages] = useState<Record<string, WikiPage>>({});
  const [wikiVersions, setWikiVersions] = useState<WikiVersionSummary[]>([]);
  const [repositoryVersions, setRepositoryVersions] = useState<RepositoryVersionSummary[]>([]);
  const [currentPageId, setCurrentPageId] = useState<string>();
  const [currentWikiVersionId, setCurrentWikiVersionId] = useState<string | undefined>();
  const [currentRepositoryVersionId, setCurrentRepositoryVersionId] = useState<string | undefined>();
  const [activeTaskId, setActiveTaskId] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [isAskModalOpen, setIsAskModalOpen] = useState(false);
  const [isModelSelectionModalOpen, setIsModelSelectionModalOpen] = useState(false);
  const askComponentRef = useRef<{ clearConversation: () => void } | null>(null);
  const initialLoadKeyRef = useRef('');

  /**
   * 当前仓库展示名称。
   */
  const displayName = useMemo(() => {
    if (repoDetail?.display_name) {
      return repoDetail.display_name;
    }

    if (effectiveRepoInfo.owner && effectiveRepoInfo.repo) {
      return `${effectiveRepoInfo.owner}/${effectiveRepoInfo.repo}`;
    }

    return '...';
  }, [effectiveRepoInfo.owner, effectiveRepoInfo.repo, repoDetail?.display_name]);

  /**
   * 当前选中的 Wiki 版本摘要。
   */
  const currentWikiVersion = useMemo(
    () => wikiVersions.find((version) => version.wiki_version_id === currentWikiVersionId),
    [currentWikiVersionId, wikiVersions],
  );

  /**
   * 当前选中的仓库快照摘要。
   */
  const currentRepositoryVersion = useMemo(
    () => repositoryVersions.find((version) => version.repository_version_id === currentRepositoryVersionId),
    [currentRepositoryVersionId, repositoryVersions],
  );

  /**
   * 当前页面是否已经绑定可供 Ask / Slides / Workshop 继承的完整版本上下文。
   */
  const hasArtifactVersionContext = useMemo(
    () => Boolean(currentRepositoryVersionId && currentWikiVersionId),
    [currentRepositoryVersionId, currentWikiVersionId],
  );

  /**
   * 构建下游制品页面需要继承的查询参数。
   * 这里显式透传 RepositoryVersion / WikiVersion，避免下游页面回退到已发布版或最新版本。
   */
  const artifactQueryString = useMemo(() => {
    const params = new URLSearchParams();

    if (selectedProviderState) {
      params.set('provider', selectedProviderState);
    }

    if (isCustomSelectedModelState) {
      params.set('is_custom_model', 'true');
      if (customSelectedModelState) {
        params.set('custom_model', customSelectedModelState);
      }
    } else if (selectedModelState) {
      params.set('model', selectedModelState);
    }

    if (language) {
      params.set('language', language);
    }

    if (currentRepositoryVersionId) {
      params.set('repositoryVersionId', currentRepositoryVersionId);
    }

    if (currentWikiVersionId) {
      params.set('wikiVersionId', currentWikiVersionId);
    }

    return params.toString();
  }, [
    currentRepositoryVersionId,
    currentWikiVersionId,
    customSelectedModelState,
    isCustomSelectedModelState,
    language,
    selectedModelState,
    selectedProviderState,
  ]);

  /**
   * Slides 页面地址。
   */
  const slidesHref = useMemo(
    () => `/repositories/${repositoryId}/slides${artifactQueryString ? `?${artifactQueryString}` : ''}`,
    [artifactQueryString, repositoryId],
  );

  /**
   * Workshop 页面地址。
   */
  const workshopHref = useMemo(
    () => `/repositories/${repositoryId}/workshop${artifactQueryString ? `?${artifactQueryString}` : ''}`,
    [artifactQueryString, repositoryId],
  );

  /**
   * 加载仓库详情，用于页面基础展示与默认分支计算。
   */
  const loadRepoDetail = useCallback(async () => {
    try {
      const response = await fetch(`/api/repositories/${repositoryId}`);
      if (!response.ok) {
        return null;
      }

      const detail = await response.json() as RepositoryDetail;
      setRepoDetail(detail);
      setEffectiveRepoInfo({
        owner: detail.owner,
        repo: detail.repo_name,
        type: detail.repo_type,
        token: null,
        localPath: null,
        repoUrl: detail.repo_url,
      });
      return detail;
    } catch (fetchError) {
      console.error('加载仓库详情失败', fetchError);
      return null;
    }
  }, [repositoryId]);

  /**
   * 将版本页数据应用到当前页面状态。
   */
  const applyWikiViewState = useCallback((
    pages: WikiVersionPagePayload[],
    wikiVersion?: WikiVersionSummary,
    repositoryVersionId?: string,
    displayNameOverride?: string,
  ) => {
    const viewState = buildWikiViewFromVersionPages(pages, {
      displayName: displayNameOverride || displayName,
      wikiVersion,
    });

    setWikiStructure(viewState.wikiStructure);
    setGeneratedPages(viewState.generatedPages);
    setCurrentWikiVersionId(wikiVersion?.wiki_version_id);
    setCurrentRepositoryVersionId(repositoryVersionId ?? wikiVersion?.repository_version_id);
    setCurrentPageId((previousPageId) => {
      if (previousPageId && viewState.generatedPages[previousPageId]) {
        return previousPageId;
      }

      return viewState.wikiStructure.pages[0]?.id;
    });

    if (wikiVersion?.generation_profile) {
      setIsComprehensiveView(wikiVersion.generation_profile !== 'concise');
    }
  }, [displayName]);

  /**
   * 统一加载版本目录列表，确保仓库页与版本切换器共享同一份数据来源。
   */
  const loadVersionCatalog = useCallback(async () => {
    const [wikiResponse, repositoryResponse] = await Promise.all([
      fetch(`/api/repositories/${repositoryId}/wiki/versions?language=${language}`),
      fetch(`/api/repositories/${repositoryId}/versions`),
    ]);

    const wikiVersionList = wikiResponse.ok
      ? await wikiResponse.json() as WikiVersionSummary[]
      : [];
    const repositoryVersionList = repositoryResponse.ok
      ? await repositoryResponse.json() as RepositoryVersionSummary[]
      : [];

    setWikiVersions(wikiVersionList);
    setRepositoryVersions(repositoryVersionList);

    return {
      wikiVersionList,
      repositoryVersionList,
    };
  }, [language, repositoryId]);

  /**
   * 根据当前页面状态优先选择要展示的 Wiki 版本。
   */
  const selectPreferredWikiVersion = useCallback((
    versionList: WikiVersionSummary[],
    requestedWikiVersionId?: string,
    requestedRepositoryVersionId?: string,
  ) => {
    if (requestedWikiVersionId) {
      const exactWikiVersion = versionList.find((version) => version.wiki_version_id === requestedWikiVersionId);
      if (exactWikiVersion) {
        return exactWikiVersion;
      }
    }

    if (requestedRepositoryVersionId) {
      const exactRepositoryVersion = versionList.find((version) => version.repository_version_id === requestedRepositoryVersionId);
      if (exactRepositoryVersion) {
        return exactRepositoryVersion;
      }
    }

    return versionList.find((version) => version.status === 'published') ?? versionList[0];
  }, []);

  /**
   * 加载指定 Wiki 版本的页面内容，并作为正文主数据源。
   */
  const loadWikiVersionContent = useCallback(async (
    wikiVersion: WikiVersionSummary,
    displayNameOverride?: string,
  ) => {
    setLoadingMessage(messages.loading?.fetchingCache || '正在加载 Wiki 版本内容...');
    const response = await fetch(
      `/api/repositories/${repositoryId}/wiki/pages?wikiVersionId=${wikiVersion.wiki_version_id}&language=${language}`,
    );

    if (!response.ok) {
      const errorBody = await readJsonSafely<TaskErrorResponse>(response);
      throw new Error(errorBody?.error || '加载版本页面失败');
    }

    const pages = await response.json() as WikiVersionPagePayload[];
    applyWikiViewState(pages, wikiVersion, wikiVersion.repository_version_id, displayNameOverride);
  }, [applyWikiViewState, language, messages.loading, repositoryId]);

  /**
   * 使用统一任务入口创建 Wiki 生成任务。
   * 该方法只负责“执行任务”，不再承担页面刷新与版本选择职责。
   */
  const executeWikiTask = useCallback(async (
    refreshOptions: RefreshOptions,
    executionOptions: WikiTaskExecutionOptions,
  ) => {
    const requestBody = buildTaskRequestBody({
      token: null,
      provider: executionOptions.provider,
      model: executionOptions.model,
      isCustomModel: executionOptions.isCustomModel,
      customModel: executionOptions.customModel,
      language,
    }, {
      repository_id: repositoryId,
      branch: refreshOptions.branch,
      refresh_strategy: refreshOptions.refreshStrategy,
      force_refresh: refreshOptions.forceRefresh,
      generation_profile: refreshOptions.generationProfile,
      comprehensive: refreshOptions.generationProfile === 'comprehensive',
    });

    const response = await fetch('/api/tasks/wiki', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(requestBody),
    });

    if (!response.ok) {
      const errorBody = await readJsonSafely<TaskErrorResponse>(response);
      const detailText = [
        errorBody?.details,
        errorBody?.request_id ? `RequestId: ${errorBody.request_id}` : null,
      ].filter(Boolean).join('\n');
      if (detailText) {
        setErrorDetails(detailText);
      }

      throw new Error(errorBody?.error || `Wiki 任务提交失败：${response.status}`);
    }

    return await response.json() as WikiTaskExecutionResponse;
  }, [language, repositoryId]);

  /**
   * 轮询任务状态，并在完成后重新对齐版本目录与正文内容。
   */
  const waitForTaskCompletion = useCallback(async (
    taskId: string,
    target: TaskCompletionTarget,
  ) => {
    setActiveTaskId(taskId);
    setLoadingMessage('任务已接收，正在等待后台生成...');

    const maxPolls = 360;
    for (let pollCount = 0; pollCount < maxPolls; pollCount += 1) {
      await new Promise((resolve) => setTimeout(resolve, 5000));
      const response = await fetch(`/api/tasks/${taskId}/status`);
      if (!response.ok) {
        continue;
      }

      const status = await response.json() as WikiTaskStatusResponse;
      if (status.progress_message) {
        setLoadingMessage(status.progress_message);
      }

      if (status.status === 'completed') {
        const { wikiVersionList } = await loadVersionCatalog();
        const preferredVersion = selectPreferredWikiVersion(
          wikiVersionList,
          target.wikiVersionId,
          target.repositoryVersionId,
        );

        if (!preferredVersion) {
          throw new Error('任务已完成，但未发现可用的 Wiki 版本');
        }

        await loadWikiVersionContent(preferredVersion);
        setActiveTaskId(null);
        return;
      }

      if (status.status === 'failed') {
        throw new Error(status.error_message || 'Wiki 生成失败');
      }

      if (status.status === 'cancelled') {
        throw new Error('任务已取消');
      }
    }

    throw new Error('任务超时：Wiki 生成超过 30 分钟');
  }, [loadVersionCatalog, loadWikiVersionContent, selectPreferredWikiVersion]);

  /**
   * 同步刷新面板与仓库页的当前模型/档位状态，
   * 避免页面展示状态与下一次任务提交参数不一致。
   */
  const syncRuntimeSelection = useCallback((options: RefreshOptions) => {
    setSelectedProviderState(options.provider);
    setSelectedModelState(options.model);
    setIsCustomSelectedModelState(false);
    setCustomSelectedModelState('');
    setIsComprehensiveView(options.generationProfile === 'comprehensive');
  }, []);

  /**
   * 基于当前页面状态生成默认刷新参数。
   */
  const buildDefaultRefreshOptions = useCallback((overrides: Partial<RefreshOptions> = {}): RefreshOptions => ({
    branch: overrides.branch ?? repoDetail?.default_branch ?? 'main',
    refreshStrategy: overrides.refreshStrategy ?? 'latest',
    forceRefresh: overrides.forceRefresh ?? false,
    generationProfile: overrides.generationProfile ?? (isComprehensiveView ? 'comprehensive' : 'concise'),
    provider: overrides.provider ?? (selectedProviderState || 'ollama'),
    model: overrides.model ?? (selectedModelState || 'gemma4:e2b'),
  }), [isComprehensiveView, repoDetail?.default_branch, selectedModelState, selectedProviderState]);

  /**
   * 统一刷新主链路。
   * 页面初始化、刷新面板提交、模型设置重新生成都从这里进入，
   * 由此保证“页面态 / 版本态 / 任务态”走同一条状态机。
   */
  const runUnifiedRefreshFlow = useCallback(async (
    refreshOptions: RefreshOptions,
    executionOptions: WikiTaskExecutionOptions,
  ) => {
    setIsLoading(true);
    setError(null);
    setErrorDetails(null);
    setExportError(null);
    setLoadingMessage('正在分析仓库版本...');

    try {
      const refreshResponse = await fetch(`/api/repositories/${repositoryId}/wiki/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          branch: refreshOptions.branch,
          refresh_strategy: refreshOptions.refreshStrategy,
          force_refresh: refreshOptions.forceRefresh,
          generation_profile: refreshOptions.generationProfile,
          provider: executionOptions.provider,
          model: executionOptions.isCustomModel ? executionOptions.customModel : executionOptions.model,
          language,
        }),
      });

      if (!refreshResponse.ok) {
        const errorBody = await readJsonSafely<TaskErrorResponse>(refreshResponse);
        throw new Error(errorBody?.error || '刷新请求失败');
      }

      const refreshPayload = await refreshResponse.json() as Record<string, unknown>;
      const refreshResult = normalizeRefreshResponse(refreshPayload);
      setLoadingMessage(refreshResult.message || '正在处理刷新请求...');

      if (refreshResult.resultType === 'reused' || refreshResult.resultType === 'no_change') {
        const { wikiVersionList } = await loadVersionCatalog();
        const preferredVersion = selectPreferredWikiVersion(
          wikiVersionList,
          refreshResult.wikiVersionId,
          refreshResult.repositoryVersionId,
        );

        if (!preferredVersion) {
          throw new Error(refreshResult.message || '当前仓库暂无可展示的 Wiki 版本');
        }

        await loadWikiVersionContent(preferredVersion);
        return;
      }

      let taskId = refreshResult.taskId;
      if (!taskId) {
        const taskResponse = await executeWikiTask(refreshOptions, executionOptions);
        taskId = taskResponse.task_id;
        setLoadingMessage(taskResponse.message || '任务已创建，正在后台生成...');
      }

      await waitForTaskCompletion(taskId, {
        wikiVersionId: refreshResult.wikiVersionId,
        repositoryVersionId: refreshResult.repositoryVersionId,
      });
    } catch (refreshError) {
      console.error('统一刷新链路执行失败', refreshError);
      setError(refreshError instanceof Error ? refreshError.message : '刷新 Wiki 失败');
      setWikiStructure((previous) => previous);
    } finally {
      setActiveTaskId(null);
      setIsLoading(false);
      setLoadingMessage(undefined);
    }
  }, [executeWikiTask, language, loadVersionCatalog, loadWikiVersionContent, repositoryId, selectPreferredWikiVersion, waitForTaskCompletion]);

  /**
   * 页面首次加载逻辑。
   * 优先加载已有版本；如果没有版本，再进入统一刷新链路。
   */
  const loadInitialData = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    setErrorDetails(null);
    setLoadingMessage(messages.loading?.initializing || '正在初始化仓库页...');

    try {
      const detail = await loadRepoDetail();
      const pageDisplayName = detail?.display_name || (detail ? `${detail.owner}/${detail.repo_name}` : undefined);
      const { wikiVersionList } = await loadVersionCatalog();
      const preferredVersion = selectPreferredWikiVersion(wikiVersionList, currentWikiVersionId);

      if (preferredVersion) {
        await loadWikiVersionContent(preferredVersion, pageDisplayName);
        return;
      }

      const refreshOptions = buildDefaultRefreshOptions();
      const executionOptions: WikiTaskExecutionOptions = {
        provider: selectedProviderState || 'ollama',
        model: selectedModelState || 'gemma4:e2b',
        isCustomModel: isCustomSelectedModelState,
        customModel: customSelectedModelState,
      };

      await runUnifiedRefreshFlow(refreshOptions, executionOptions);
    } catch (loadError) {
      console.error('加载仓库页失败', loadError);
      setError(loadError instanceof Error ? loadError.message : '加载 Wiki 失败');
    } finally {
      setIsLoading(false);
      setLoadingMessage(undefined);
    }
  }, [
    buildDefaultRefreshOptions,
    currentWikiVersionId,
    customSelectedModelState,
    isCustomSelectedModelState,
    loadRepoDetail,
    loadVersionCatalog,
    loadWikiVersionContent,
    messages.loading,
    runUnifiedRefreshFlow,
    selectPreferredWikiVersion,
    selectedModelState,
    selectedProviderState,
  ]);

  /**
   * 导出当前页面已加载的 Wiki 内容。
   */
  const exportWiki = useCallback(async (format: 'markdown' | 'json') => {
    if (!wikiStructure || Object.keys(generatedPages).length === 0) {
      setExportError('暂无可导出的 Wiki 内容');
      return;
    }

    try {
      setIsExporting(true);
      setExportError(null);

      const pagesToExport = wikiStructure.pages.map((page) => ({
        ...page,
        content: generatedPages[page.id]?.content || '',
      }));

      const response = await fetch('/export/wiki', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          repository_id: repositoryId,
          pages: pagesToExport,
          format,
        }),
      });

      if (!response.ok) {
        const errorText = await response.text().catch(() => '未知错误');
        throw new Error(`导出失败：${response.status} - ${errorText}`);
      }

      const contentDisposition = response.headers.get('Content-Disposition');
      let filename = `${effectiveRepoInfo.repo || 'repository'}_wiki.${format === 'markdown' ? 'md' : 'json'}`;
      const filenameMatch = contentDisposition?.match(/filename=(.+)/);
      if (filenameMatch?.[1]) {
        filename = filenameMatch[1].replace(/"/g, '');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = filename;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      window.URL.revokeObjectURL(url);
    } catch (exportWikiError) {
      console.error('导出 Wiki 失败', exportWikiError);
      setExportError(exportWikiError instanceof Error ? exportWikiError.message : '导出 Wiki 失败');
    } finally {
      setIsExporting(false);
    }
  }, [effectiveRepoInfo.repo, generatedPages, repositoryId, wikiStructure]);

  /**
   * 模型弹窗应用后的刷新动作。
   */
  const confirmRefresh = useCallback(async () => {
    const refreshOptions = buildDefaultRefreshOptions({ forceRefresh: true });
    const executionOptions: WikiTaskExecutionOptions = {
      provider: selectedProviderState || 'ollama',
      model: selectedModelState || 'gemma4:e2b',
      isCustomModel: isCustomSelectedModelState,
      customModel: customSelectedModelState,
    };

    await runUnifiedRefreshFlow(refreshOptions, executionOptions);
  }, [
    buildDefaultRefreshOptions,
    customSelectedModelState,
    isCustomSelectedModelState,
    runUnifiedRefreshFlow,
    selectedModelState,
    selectedProviderState,
  ]);

  /**
   * 处理版本切换。
   */
  const handleVersionChange = useCallback(async (wikiVersionId: string, repositoryVersionVersionId: string) => {
    setIsLoading(true);
    setError(null);
    setErrorDetails(null);

    try {
      const currentVersion = wikiVersions.find((version) => version.wiki_version_id === wikiVersionId);
      if (!currentVersion) {
        const { wikiVersionList } = await loadVersionCatalog();
        const fallbackVersion = selectPreferredWikiVersion(wikiVersionList, wikiVersionId, repositoryVersionVersionId);
        if (!fallbackVersion) {
          throw new Error('目标版本不存在');
        }

        await loadWikiVersionContent(fallbackVersion);
        return;
      }

      await loadWikiVersionContent(currentVersion);
    } catch (versionError) {
      console.error('版本切换失败', versionError);
      setError(versionError instanceof Error ? versionError.message : '加载版本页面失败');
    } finally {
      setIsLoading(false);
      setLoadingMessage(undefined);
    }
  }, [loadVersionCatalog, loadWikiVersionContent, selectPreferredWikiVersion, wikiVersions]);

  /**
   * 处理刷新面板提交。
   */
  const handleRefreshWithOptions = useCallback(async (options: RefreshOptions) => {
    syncRuntimeSelection(options);
    await runUnifiedRefreshFlow(options, {
      provider: options.provider,
      model: options.model,
      isCustomModel: false,
      customModel: '',
    });
  }, [runUnifiedRefreshFlow, syncRuntimeSelection]);

  /**
   * 处理页面切换。
   */
  const handlePageSelect = useCallback((pageId: string) => {
    setCurrentPageId(pageId);
  }, []);

  useEffect(() => {
    const wikiContent = document.getElementById('wiki-content');
    wikiContent?.scrollTo({ top: 0, behavior: 'smooth' });
  }, [currentPageId]);

  useEffect(() => {
    const handleEsc = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsAskModalOpen(false);
      }
    };

    if (isAskModalOpen) {
      window.addEventListener('keydown', handleEsc);
      return () => window.removeEventListener('keydown', handleEsc);
    }
  }, [isAskModalOpen]);

  useEffect(() => {
    const loadKey = [repositoryId, language, isComprehensiveParam ? 'comprehensive' : 'concise'].join('|');
    if (initialLoadKeyRef.current === loadKey) {
      return;
    }

    initialLoadKeyRef.current = loadKey;
    loadInitialData();
  }, [isComprehensiveParam, language, loadInitialData, repositoryId]);

  const currentPage = currentPageId ? generatedPages[currentPageId] : undefined;
  const RepositoryIcon = getRepositoryIcon(effectiveRepoInfo.type);

  return (
    <div className="h-screen flex flex-col bg-[var(--background)]">
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
              <p className="text-sm text-[var(--foreground)] font-medium">
                {loadingMessage || messages.common?.loading || 'Loading...'}
              </p>
              {activeTaskId && (
                <p className="text-xs text-[var(--muted)] mt-2">
                  当前任务：{activeTaskId}
                </p>
              )}
              <p className="text-xs text-[var(--muted)] mt-2">关闭当前页面后，后端仍会继续生成</p>
            </div>
          </div>
        ) : wikiStructure ? (
          <div className="flex-1 min-h-0 flex flex-col lg:flex-row">
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
                      <a
                        href={effectiveRepoInfo.repoUrl ?? ''}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-[var(--accent-primary)] hover:underline truncate"
                      >
                        {displayName}
                      </a>
                    </>
                  )}
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="tag tag-primary">
                    {isComprehensiveView ? (messages.form?.comprehensive || 'Comprehensive') : (messages.form?.concise || 'Concise')}
                  </span>
                  {currentWikiVersion && (
                    <span className="tag tag-default">v{currentWikiVersion.version_no}</span>
                  )}
                  {currentRepositoryVersion && (
                    <span className="tag tag-default">
                      {currentRepositoryVersion.commit_sha.slice(0, 8)}
                    </span>
                  )}
                </div>
              </div>

              <div className="p-4 border-b border-[var(--border-color)] space-y-2">
                <VersionSwitcher
                  currentWikiVersionId={currentWikiVersionId}
                  currentRepositoryVersionId={currentRepositoryVersionId}
                  wikiVersions={wikiVersions}
                  repositoryVersions={repositoryVersions}
                  isLoading={isLoading}
                  onVersionChange={handleVersionChange}
                />
                <RefreshPanel
                  repositoryId={repositoryId}
                  defaultBranch={repoDetail?.default_branch || 'main'}
                  defaultGenerationProfile={isComprehensiveView ? 'comprehensive' : 'concise'}
                  defaultProvider={selectedProviderState || 'ollama'}
                  defaultModel={selectedModelState || 'gemma4:e2b'}
                  onRefresh={handleRefreshWithOptions}
                  isLoading={isLoading}
                />
                <button
                  onClick={() => setIsModelSelectionModalOpen(true)}
                  disabled={isLoading}
                  className="btn-secondary w-full text-xs justify-center"
                >
                  <FaSync className={isLoading ? 'animate-spin' : ''} />
                  模型与生成设置
                </button>

                {Object.keys(generatedPages).length > 0 && (
                  <div className="flex gap-1.5">
                    <button
                      onClick={() => exportWiki('markdown')}
                      disabled={isExporting}
                      className="btn-primary flex-1 text-xs justify-center py-1.5"
                    >
                      <FaDownload className="text-[10px]" /> Markdown
                    </button>
                    <button
                      onClick={() => exportWiki('json')}
                      disabled={isExporting}
                      className="btn-secondary flex-1 text-xs justify-center py-1.5"
                    >
                      <FaFileExport className="text-[10px]" /> JSON
                    </button>
                  </div>
                )}
                <div className="grid grid-cols-2 gap-1.5">
                  {hasArtifactVersionContext ? (
                    <Link href={slidesHref} className="btn-secondary text-xs justify-center py-1.5">
                      <FaFileExport className="text-[10px]" /> Slides
                    </Link>
                  ) : (
                    <span className="btn-secondary text-xs justify-center py-1.5 opacity-60 cursor-not-allowed">
                      <FaFileExport className="text-[10px]" /> Slides
                    </span>
                  )}
                  {hasArtifactVersionContext ? (
                    <Link href={workshopHref} className="btn-secondary text-xs justify-center py-1.5">
                      <FaBookOpen className="text-[10px]" /> Workshop
                    </Link>
                  ) : (
                    <span className="btn-secondary text-xs justify-center py-1.5 opacity-60 cursor-not-allowed">
                      <FaBookOpen className="text-[10px]" /> Workshop
                    </span>
                  )}
                </div>
                {!hasArtifactVersionContext && (
                  <p className="text-xs text-[var(--muted)]">
                    当前版本尚未就绪，Ask / Slides / Workshop 将在绑定 RepositoryVersion 与 WikiVersion 后开放。
                  </p>
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
                        {currentPage.relatedPages.map((relatedId) => {
                          const relatedPage = wikiStructure.pages.find((page) => page.id === relatedId);
                          return relatedPage ? (
                            <button
                              key={relatedId}
                              className="tag tag-primary cursor-pointer hover:bg-[var(--accent-primary)]/15 transition-colors"
                              onClick={() => handlePageSelect(relatedId)}
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

      {!isLoading && wikiStructure && (
        <button
          onClick={() => setIsAskModalOpen(true)}
          className="fixed bottom-6 right-6 w-14 h-14 rounded-full gradient-accent text-white shadow-lg flex items-center justify-center hover:shadow-xl transition-all z-50 hover:scale-105"
          aria-label={messages.ask?.title || 'Ask about this repository'}
        >
          <FaComments className="text-xl" />
        </button>
      )}

      {isAskModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setIsAskModalOpen(false)} />
          <div className="relative bg-[var(--card-bg)] rounded-xl shadow-2xl w-full max-w-2xl max-h-[80vh] flex flex-col border border-[var(--border-color)]">
            <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--border-color)]">
              <h3 className="font-semibold text-sm text-[var(--foreground)]">{messages.ask?.title || 'Ask about this repository'}</h3>
              <button onClick={() => setIsAskModalOpen(false)} className="btn-ghost p-1.5 rounded-lg">
                <FaTimes />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-4">
              <Ask
                repositoryId={repositoryId}
                provider={selectedProviderState}
                model={selectedModelState}
                isCustomModel={isCustomSelectedModelState}
                customModel={customSelectedModelState}
                language={language}
                repositoryVersionId={currentRepositoryVersionId}
                wikiVersionId={currentWikiVersionId}
                onRef={(ref) => { askComponentRef.current = ref; }}
              />
            </div>
          </div>
        </div>
      )}

      <ModelSelectionModal
        isOpen={isModelSelectionModalOpen}
        onClose={() => setIsModelSelectionModalOpen(false)}
        provider={selectedProviderState}
        setProvider={setSelectedProviderState}
        model={selectedModelState}
        setModel={setSelectedModelState}
        isCustomModel={isCustomSelectedModelState}
        setIsCustomModel={setIsCustomSelectedModelState}
        customModel={customSelectedModelState}
        setCustomModel={setCustomSelectedModelState}
        isComprehensiveView={isComprehensiveView}
        setIsComprehensiveView={setIsComprehensiveView}
        showFileFilters={false}
        excludedDirs={''}
        setExcludedDirs={() => {}}
        excludedFiles={''}
        setExcludedFiles={() => {}}
        includedDirs={''}
        setIncludedDirs={() => {}}
        includedFiles={''}
        setIncludedFiles={() => {}}
        onApply={confirmRefresh}
        showWikiType={true}
        showTokenInput={false}
        repositoryType={effectiveRepoInfo.type as 'github' | 'gitlab' | 'bitbucket'}
        authRequired={false}
        authCode={''}
        setAuthCode={() => {}}
        isAuthLoading={false}
      />
    </div>
  );
}
