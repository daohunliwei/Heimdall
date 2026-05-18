'use client';

import React, { createContext, useCallback, useContext, useMemo, useReducer } from 'react';
import type {
  RepositoryDetailResponse,
  RepositoryVersionSummary,
  WikiVersionSummary,
  WikiVersionPagePayload,
} from '@/types/api';

// ── 状态定义 ──

interface RepositoryState {
  repositoryId: string;
  repoDetail: RepositoryDetailResponse | null;
  wikiVersions: WikiVersionSummary[];
  repositoryVersions: RepositoryVersionSummary[];
  currentWikiVersionId: string | undefined;
  currentRepositoryVersionId: string | undefined;
  currentPageId: string | undefined;
  generatedPages: Record<string, WikiVersionPagePayload>;
  isLoading: boolean;
  loadingMessage: string | undefined;
  error?: string | null;
  errorDetails?: string | null;
  activeTaskId: string | null;
  isComprehensiveView: boolean;
  selectedProviderState: string;
  selectedModelState: string;
  isCustomSelectedModelState: boolean;
  customSelectedModelState: string;
  language: string;
}

const initialState: Omit<RepositoryState, 'repositoryId'> = {
  repoDetail: null,
  wikiVersions: [],
  repositoryVersions: [],
  currentWikiVersionId: undefined,
  currentRepositoryVersionId: undefined,
  currentPageId: undefined,
  generatedPages: {},
  isLoading: true,
  loadingMessage: undefined,
  error: null,
  errorDetails: null,
  activeTaskId: null,
  isComprehensiveView: true,
  selectedProviderState: 'ollama',
  selectedModelState: 'gemma4:e2b',
  isCustomSelectedModelState: false,
  customSelectedModelState: '',
  language: 'zh',
};

// ── Action 类型 ──

type RepositoryAction =
  | { type: 'SET_REPO_DETAIL'; payload: RepositoryDetailResponse }
  | { type: 'SET_VERSIONS'; wikiVersions: WikiVersionSummary[]; repositoryVersions: RepositoryVersionSummary[] }
  | { type: 'SET_CURRENT_VERSIONS'; wikiVersionId?: string; repositoryVersionId?: string }
  | { type: 'SET_PAGES'; pages: WikiVersionPagePayload[] }
  | { type: 'SET_CURRENT_PAGE_ID'; pageId: string }
  | { type: 'SET_LOADING'; isLoading: boolean; message?: string }
  | { type: 'SET_ERROR'; error: string; details?: string }
  | { type: 'CLEAR_ERROR' }
  | { type: 'SET_ACTIVE_TASK'; taskId: string | null }
  | { type: 'SET_COMPREHENSIVE_VIEW'; isComprehensive: boolean };

// ── Reducer ──

function repositoryReducer(state: RepositoryState, action: RepositoryAction): RepositoryState {
  switch (action.type) {
    case 'SET_REPO_DETAIL':
      return { ...state, repoDetail: action.payload, error: null };
    case 'SET_VERSIONS':
      return { ...state, wikiVersions: action.wikiVersions, repositoryVersions: action.repositoryVersions };
    case 'SET_CURRENT_VERSIONS':
      return {
        ...state,
        currentWikiVersionId: action.wikiVersionId,
        currentRepositoryVersionId: action.repositoryVersionId,
      };
    case 'SET_PAGES': {
      const pageMap = Object.fromEntries(action.pages.map((p) => [p.id, p]));
      return { ...state, generatedPages: pageMap };
    }
    case 'SET_CURRENT_PAGE_ID':
      return { ...state, currentPageId: action.pageId };
    case 'SET_LOADING':
      return { ...state, isLoading: action.isLoading, loadingMessage: action.message, error: null };
    case 'SET_ERROR':
      return { ...state, error: action.error, errorDetails: action.details ?? null, isLoading: false, loadingMessage: undefined };
    case 'CLEAR_ERROR':
      return { ...state, error: null, errorDetails: null };
    case 'SET_ACTIVE_TASK':
      return { ...state, activeTaskId: action.taskId };
    case 'SET_COMPREHENSIVE_VIEW':
      return { ...state, isComprehensiveView: action.isComprehensive };
    default:
      return state;
  }
}

// ── Context ──

interface RepositoryContextValue {
  state: RepositoryState;
  dispatch: React.Dispatch<RepositoryAction>;
  /** 当前选中的 Wiki 版本摘要 */
  currentWikiVersion: WikiVersionSummary | undefined;
  /** 当前选中的仓库快照摘要 */
  currentRepositoryVersion: RepositoryVersionSummary | undefined;
  /** 是否已绑定完整版本上下文（可供 Ask/Slides/Workshop 继承） */
  hasArtifactVersionContext: boolean;
  /** 展示名称 */
  displayName: string;
}

const RepositoryContext = createContext<RepositoryContextValue | null>(null);

// ── Provider ──

export function RepositoryProvider({
  repositoryId,
  children,
}: {
  repositoryId: string;
  children: React.ReactNode;
}) {
  const [state, dispatch] = useReducer(repositoryReducer, {
    ...initialState,
    repositoryId,
  });

  const currentWikiVersion = useMemo(
    () => state.wikiVersions.find((v) => v.wiki_version_id === state.currentWikiVersionId),
    [state.wikiVersions, state.currentWikiVersionId],
  );

  const currentRepositoryVersion = useMemo(
    () => state.repositoryVersions.find((v) => v.repository_version_id === state.currentRepositoryVersionId),
    [state.repositoryVersions, state.currentRepositoryVersionId],
  );

  const hasArtifactVersionContext = useMemo(
    () => Boolean(state.currentRepositoryVersionId && state.currentWikiVersionId),
    [state.currentRepositoryVersionId, state.currentWikiVersionId],
  );

  const displayName = useMemo(() => {
    if (state.repoDetail?.display_name) return state.repoDetail.display_name;
    if (state.repoDetail) return `${state.repoDetail.owner}/${state.repoDetail.repo_name}`;
    return '...';
  }, [state.repoDetail]);

  const value = useMemo<RepositoryContextValue>(
    () => ({
      state,
      dispatch,
      currentWikiVersion,
      currentRepositoryVersion,
      hasArtifactVersionContext,
      displayName,
    }),
    [state, currentWikiVersion, currentRepositoryVersion, hasArtifactVersionContext, displayName],
  );

  return (
    <RepositoryContext.Provider value={value}>
      {children}
    </RepositoryContext.Provider>
  );
}

// ── Hook ──

export function useRepository(): RepositoryContextValue {
  const ctx = useContext(RepositoryContext);
  if (!ctx) {
    throw new Error('useRepository must be used within RepositoryProvider');
  }

  return ctx;
}

export default RepositoryContext;
