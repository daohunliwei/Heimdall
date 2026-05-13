'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { FaWikipediaW, FaGithub, FaGitlab, FaBitbucket, FaArrowRight } from 'react-icons/fa';
import ThemeToggle from '@/components/theme-toggle';
import Mermaid from '../components/Mermaid';
import ConfigurationModal from '@/components/ConfigurationModal';
import ProcessedProjects from '@/components/ProcessedProjects';
import { extractUrlPath, extractUrlDomain } from '@/utils/urlDecoder';
import { useProcessedProjects } from '@/hooks/useProcessedProjects';

import { useLanguage } from '@/contexts/LanguageContext';

const DEMO_FLOW_CHART = `graph TD
  A[Code Repository] --> B[Heimdall]
  B --> C[Architecture Diagrams]
  B --> D[Component Relationships]
  B --> E[Data Flow]
  B --> F[Process Workflows]

  style A fill:#f9d3a9,stroke:#d86c1f
  style B fill:#d4a9f9,stroke:#6c1fd8
  style C fill:#a9f9d3,stroke:#1fd86c
  style D fill:#a9d3f9,stroke:#1f6cd8
  style E fill:#f9a9d3,stroke:#d81f6c
  style F fill:#d3f9a9,stroke:#6cd81f`;

const DEMO_SEQUENCE_CHART = `sequenceDiagram
  participant User
  participant Heimdall
  participant GitHub

  User->>Heimdall: Enter repository URL
  Heimdall->>GitHub: Request repository data
  GitHub-->>Heimdall: Return repository data
  Heimdall->>Heimdall: Process and analyze code
  Heimdall-->>User: Display wiki with diagrams

  Note over User,GitHub: Heimdall supports sequence diagrams for visualizing interactions`;

const DEFAULT_REPOSITORY_INPUT = 'https://github.com/AsyncFuncAI/heimdall-open';
const REPO_CONFIG_CACHE_KEY = 'heimdallRepoConfigCache';

const loadRepoConfigCache = (): Record<string, unknown> => {
  try {
    const cacheValue = localStorage.getItem(REPO_CONFIG_CACHE_KEY);
    if (!cacheValue) return {};
    const parsedCache = JSON.parse(cacheValue);
    if (parsedCache && typeof parsedCache === 'object') {
      return parsedCache as Record<string, unknown>;
    }
  } catch (error) {
    console.error('Error loading config from localStorage:', error);
  }
  return {};
};

const persistRepoConfigCache = (configs: Record<string, unknown>): void => {
  try {
    localStorage.setItem(REPO_CONFIG_CACHE_KEY, JSON.stringify(configs));
  } catch (error) {
    console.error('Error saving config to localStorage:', error);
  }
};

export default function Home() {
  const router = useRouter();
  const { language, setLanguage, messages, supportedLanguages } = useLanguage();
  const { projects, isLoading: projectsLoading } = useProcessedProjects();

  const t = (key: string, params: Record<string, string | number> = {}): string => {
    const keys = key.split('.');
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let value: any = messages;
    for (const k of keys) {
      if (value && typeof value === 'object' && k in value) {
        value = value[k];
      } else {
        return key;
      }
    }
    if (typeof value === 'string') {
      return Object.entries(params).reduce((acc: string, [paramKey, paramValue]) => {
        return acc.replace(`{${paramKey}}`, String(paramValue));
      }, value);
    }
    return key;
  };

  const [repositoryInput, setRepositoryInput] = useState(DEFAULT_REPOSITORY_INPUT);

  const loadConfigFromCache = useCallback((repoUrl: string) => {
    if (!repoUrl) return;
    try {
      const configs = loadRepoConfigCache() as Record<string, Record<string, unknown>>;
      const config = configs[repoUrl.trim()];
      if (config) {
        setSelectedLanguage((config.selectedLanguage as string) || language);
        setIsComprehensiveView(config.isComprehensiveView === undefined ? true : Boolean(config.isComprehensiveView));
        setProvider((config.provider as string) || '');
        setModel((config.model as string) || '');
        setIsCustomModel(Boolean(config.isCustomModel));
        setCustomModel((config.customModel as string) || '');
        setSelectedPlatform(((config.selectedPlatform as 'github' | 'gitlab' | 'bitbucket') || 'github'));
        setExcludedDirs((config.excludedDirs as string) || '');
        setExcludedFiles((config.excludedFiles as string) || '');
        setIncludedDirs((config.includedDirs as string) || '');
        setIncludedFiles((config.includedFiles as string) || '');
      }
    } catch (error) {
      console.error('Error loading config from localStorage:', error);
    }
  }, [language]);

  const handleRepositoryInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newRepoUrl = e.target.value;
    setRepositoryInput(newRepoUrl);
    if (newRepoUrl.trim() !== "") {
      loadConfigFromCache(newRepoUrl);
    }
  };

  useEffect(() => {
    if (repositoryInput) {
      loadConfigFromCache(repositoryInput);
    }
  }, [repositoryInput, loadConfigFromCache]);

  const [provider, setProvider] = useState<string>('');
  const [model, setModel] = useState<string>('');
  const [isCustomModel, setIsCustomModel] = useState<boolean>(false);
  const [customModel, setCustomModel] = useState<string>('');
  const [isComprehensiveView, setIsComprehensiveView] = useState<boolean>(true);
  const [excludedDirs, setExcludedDirs] = useState('');
  const [excludedFiles, setExcludedFiles] = useState('');
  const [includedDirs, setIncludedDirs] = useState('');
  const [includedFiles, setIncludedFiles] = useState('');
  const [selectedPlatform, setSelectedPlatform] = useState<'github' | 'gitlab' | 'bitbucket'>('github');
  const [accessToken, setAccessToken] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [selectedLanguage, setSelectedLanguage] = useState<string>(language);
  const [authRequired, setAuthRequired] = useState<boolean>(false);
  const [authCode, setAuthCode] = useState<string>('');
  const [isAuthLoading, setIsAuthLoading] = useState<boolean>(true);
  const [isConfigModalOpen, setIsConfigModalOpen] = useState(false);

  useEffect(() => {
    setLanguage(selectedLanguage);
  }, [selectedLanguage, setLanguage]);

  useEffect(() => {
    const fetchAuthStatus = async () => {
      try {
        setIsAuthLoading(true);
        const response = await fetch('/api/auth/status');
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
        const data = await response.json();
        setAuthRequired(data.auth_required);
      } catch (err) {
        console.error("Failed to fetch auth status:", err);
        setAuthRequired(true);
      } finally {
        setIsAuthLoading(false);
      }
    };
    fetchAuthStatus();
  }, []);

  const parseRepositoryInput = (input: string): {
    owner: string, repo: string, type: string, fullPath?: string, localPath?: string
  } | null => {
    input = input.trim();
    let owner = '', repo = '', type = 'github', fullPath;
    let localPath: string | undefined;

    const windowsPathRegex = /^[a-zA-Z]:\\(?:[^\\/:*?"<>|\r\n]+\\)*[^\\/:*?"<>|\r\n]*$/;
    const customGitRegex = /^(?:https?:\/\/)?([^\/]+)\/(.+?)\/([^\/]+)(?:\.git)?\/?$/;

    if (windowsPathRegex.test(input)) {
      type = 'local';
      localPath = input;
      repo = input.split('\\').pop() || 'local-repo';
      owner = 'local';
    } else if (input.startsWith('/')) {
      type = 'local';
      localPath = input;
      repo = input.split('/').filter(Boolean).pop() || 'local-repo';
      owner = 'local';
    } else if (customGitRegex.test(input)) {
      const domain = extractUrlDomain(input);
      if (domain?.includes('github.com')) type = 'github';
      else if (domain?.includes('gitlab.com') || domain?.includes('gitlab.')) type = 'gitlab';
      else if (domain?.includes('bitbucket.org') || domain?.includes('bitbucket.')) type = 'bitbucket';
      else type = 'web';
      fullPath = extractUrlPath(input)?.replace(/\.git$/, '');
      const parts = fullPath?.split('/') ?? [];
      if (parts.length >= 2) {
        repo = parts[parts.length - 1] || '';
        owner = parts[parts.length - 2] || '';
      }
    } else {
      console.error('Unsupported URL format:', input);
      return null;
    }

    if (!owner || !repo) return null;
    owner = owner.trim();
    repo = repo.trim();
    if (repo.endsWith('.git')) repo = repo.slice(0, -4);
    return { owner, repo, type, fullPath, localPath };
  };

  const handleFormSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const parsedRepo = parseRepositoryInput(repositoryInput);
    if (!parsedRepo) {
      setError('Invalid repository format. Use "owner/repo", GitHub/GitLab/BitBucket URL, or a local folder path like "/path/to/folder" or "C:\\path\\to\\folder".');
      return;
    }
    setError(null);
    setIsConfigModalOpen(true);
  };

  const validateAuthCode = async () => {
    try {
      if (authRequired) {
        if (!authCode) return false;
        const response = await fetch('/api/auth/validate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ 'code': authCode })
        });
        if (!response.ok) return false;
        const data = await response.json();
        return data.success || false;
      }
    } catch { return false; }
    return true;
  };

  const handleGenerateWiki = async () => {
    const validation = await validateAuthCode();
    if (!validation) {
      setError('Failed to validate the authorization code');
      setIsConfigModalOpen(false);
      return;
    }

    if (isSubmitting) return;

    try {
      const currentRepoUrl = repositoryInput.trim();
      if (currentRepoUrl) {
        const existingConfigs = loadRepoConfigCache() as Record<string, unknown>;
        existingConfigs[currentRepoUrl] = {
          selectedLanguage, isComprehensiveView, provider, model,
          isCustomModel, customModel, selectedPlatform, excludedDirs,
          excludedFiles, includedDirs, includedFiles,
        };
        persistRepoConfigCache(existingConfigs);
      }
    } catch (error) {
      console.error('Error saving config to localStorage:', error);
    }

    setIsSubmitting(true);
    const parsedRepo = parseRepositoryInput(repositoryInput);

    if (!parsedRepo) {
      setError('Invalid repository format.');
      setIsSubmitting(false);
      return;
    }

    const { owner, repo, type, localPath } = parsedRepo;
    const params = new URLSearchParams();
    if (accessToken) params.append('token', accessToken);
    params.append('type', (type === 'local' ? type : selectedPlatform) || 'github');
    if (localPath) {
      params.append('local_path', encodeURIComponent(localPath));
    } else {
      params.append('repo_url', encodeURIComponent(repositoryInput));
    }
    params.append('provider', provider);
    params.append('model', model);
    if (isCustomModel && customModel) params.append('custom_model', customModel);
    if (excludedDirs) params.append('excluded_dirs', excludedDirs);
    if (excludedFiles) params.append('excluded_files', excludedFiles);
    if (includedDirs) params.append('included_dirs', includedDirs);
    if (includedFiles) params.append('included_files', includedFiles);
    params.append('language', selectedLanguage);
    params.append('comprehensive', isComprehensiveView.toString());

    const queryString = params.toString() ? `?${params.toString()}` : '';
    router.push(`/${owner}/${repo}${queryString}`);
  };

  const hasProjects = !projectsLoading && projects.length > 0;

  return (
    <div className="min-h-screen flex flex-col bg-[var(--background)]">
      {/* Navigation */}
      <header className="sticky top-0 z-50 bg-[var(--background)]/80 backdrop-blur-md border-b border-[var(--border-color)]">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
          <Link href="/" className="flex items-center gap-2.5 font-semibold text-lg text-[var(--foreground)] hover:text-[var(--accent-primary)] transition-colors">
            <div className="w-8 h-8 rounded-lg gradient-accent flex items-center justify-center">
              <FaWikipediaW className="text-white text-sm" />
            </div>
            {t('common.appName')}
          </Link>
          <div className="flex items-center gap-3">
            <Link href="/wiki/projects" className="text-sm text-[var(--muted)] hover:text-[var(--foreground)] transition-colors">
              {t('nav.wikiProjects')}
            </Link>
            <ThemeToggle />
          </div>
        </div>
      </header>

      <main className="flex-1">
        {/* Hero Section */}
        <section className="max-w-3xl mx-auto px-4 pt-20 pb-12 text-center">
          <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-[var(--foreground)] mb-4">
            {t('common.appName')}
          </h1>
          <p className="text-lg text-[var(--muted)] mb-8 max-w-xl mx-auto leading-relaxed">
            {t('home.description')}
          </p>

          {/* Search Input */}
          <form onSubmit={handleFormSubmit} className="max-w-xl mx-auto">
            <div className="flex gap-2">
              <div className="relative flex-1">
                <input
                  type="text"
                  value={repositoryInput}
                  onChange={handleRepositoryInputChange}
                  placeholder={t('form.repoPlaceholder')}
                  className="input h-11 pr-4 text-sm"
                />
                {error && (
                  <p className="text-[var(--highlight)] text-xs mt-1.5 text-left">{error}</p>
                )}
              </div>
              <button
                type="submit"
                className="btn-primary h-11 px-5 whitespace-nowrap"
                disabled={isSubmitting}
              >
                {isSubmitting ? t('common.processing') : t('common.generateWiki')}
                {!isSubmitting && <FaArrowRight className="text-xs" />}
              </button>
            </div>
          </form>

          {/* Quick examples */}
          <div className="flex flex-wrap items-center justify-center gap-2 mt-4">
            <span className="text-xs text-[var(--muted-light)]">支持:</span>
            <span className="tag tag-default">
              <FaGithub className="mr-1" size={10} /> GitHub
            </span>
            <span className="tag tag-default">
              <FaGitlab className="mr-1" size={10} /> GitLab
            </span>
            <span className="tag tag-default">
              <FaBitbucket className="mr-1" size={10} /> Bitbucket
            </span>
            <span className="tag tag-default">本地目录</span>
          </div>
        </section>

        {/* Features Grid */}
        {!hasProjects && (
          <section className="max-w-5xl mx-auto px-4 pb-16">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div className="card p-6">
                <div className="w-10 h-10 rounded-lg bg-[var(--accent-secondary)] flex items-center justify-center mb-4">
                  <svg className="w-5 h-5 text-[var(--accent-primary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                </div>
                <h3 className="font-semibold text-[var(--foreground)] mb-2">智能文档生成</h3>
                <p className="text-sm text-[var(--muted)] leading-relaxed">自动分析代码结构，生成包含架构说明、模块关系和 API 文档的结构化 Wiki</p>
              </div>
              <div className="card p-6">
                <div className="w-10 h-10 rounded-lg bg-[var(--accent-secondary)] flex items-center justify-center mb-4">
                  <svg className="w-5 h-5 text-[var(--accent-primary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M4 5a1 1 0 011-1h14a1 1 0 011 1v2a1 1 0 01-1 1H5a1 1 0 01-1-1V5zM4 13a1 1 0 011-1h6a1 1 0 011 1v6a1 1 0 01-1 1H5a1 1 0 01-1-1v-6zM16 13a1 1 0 011-1h2a1 1 0 011 1v6a1 1 0 01-1 1h-2a1 1 0 01-1-1v-6z" />
                  </svg>
                </div>
                <h3 className="font-semibold text-[var(--foreground)] mb-2">自动图示</h3>
                <p className="text-sm text-[var(--muted)] leading-relaxed">内置流程图、时序图、类图等多种可视化方式，直观展示代码逻辑</p>
              </div>
              <div className="card p-6">
                <div className="w-10 h-10 rounded-lg bg-[var(--accent-secondary)] flex items-center justify-center mb-4">
                  <svg className="w-5 h-5 text-[var(--accent-primary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9" />
                  </svg>
                </div>
                <h3 className="font-semibold text-[var(--foreground)] mb-2">多平台支持</h3>
                <p className="text-sm text-[var(--muted)] leading-relaxed">支持 GitHub、GitLab、Bitbucket 及本地仓库，多 AI 模型后端可选</p>
              </div>
            </div>
          </section>
        )}

        {/* Processed Projects or Demo */}
        <section className="max-w-5xl mx-auto px-4 pb-20">
          {hasProjects ? (
            <div>
              <div className="flex items-center gap-3 mb-6">
                <h2 className="text-xl font-semibold text-[var(--foreground)]">{t('projects.recentProjects')}</h2>
                <Link href="/wiki/projects" className="text-sm text-[var(--accent-primary)] hover:underline">
                  {t('nav.wikiProjects')} →
                </Link>
              </div>
              <ProcessedProjects showHeader={false} maxItems={6} messages={messages} />
            </div>
          ) : (
            <div>
              <h2 className="text-xl font-semibold text-[var(--foreground)] mb-6 text-center">{t('home.advancedVisualization')}</h2>
              <p className="text-sm text-[var(--muted)] text-center mb-8">{t('home.diagramDescription')}</p>
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <div className="card p-4">
                  <h4 className="text-sm font-medium text-[var(--foreground)] mb-3">{t('home.flowDiagram')}</h4>
                  <div className="mermaid-container">
                    <Mermaid chart={DEMO_FLOW_CHART} />
                  </div>
                </div>
                <div className="card p-4">
                  <h4 className="text-sm font-medium text-[var(--foreground)] mb-3">{t('home.sequenceDiagram')}</h4>
                  <div className="mermaid-container">
                    <Mermaid chart={DEMO_SEQUENCE_CHART} />
                  </div>
                </div>
              </div>
            </div>
          )}
        </section>
      </main>

      {/* Footer */}
      <footer className="border-t border-[var(--border-color)]">
        <div className="max-w-6xl mx-auto px-4 py-6 flex flex-col sm:flex-row items-center justify-between gap-4">
          <p className="text-xs text-[var(--muted)]">{t('footer.copyright')}</p>
          <div className="flex items-center gap-4">
            <a href="https://github.com/AsyncFuncAI/heimdall-open" target="_blank" rel="noopener noreferrer"
              className="text-[var(--muted)] hover:text-[var(--foreground)] transition-colors">
              <FaGithub className="text-lg" />
            </a>
          </div>
        </div>
      </footer>

      {/* Configuration Modal */}
      <ConfigurationModal
        isOpen={isConfigModalOpen}
        onClose={() => setIsConfigModalOpen(false)}
        repositoryInput={repositoryInput}
        selectedLanguage={selectedLanguage}
        setSelectedLanguage={setSelectedLanguage}
        supportedLanguages={supportedLanguages}
        isComprehensiveView={isComprehensiveView}
        setIsComprehensiveView={setIsComprehensiveView}
        provider={provider}
        setProvider={setProvider}
        model={model}
        setModel={setModel}
        isCustomModel={isCustomModel}
        setIsCustomModel={setIsCustomModel}
        customModel={customModel}
        setCustomModel={setCustomModel}
        selectedPlatform={selectedPlatform}
        setSelectedPlatform={setSelectedPlatform}
        accessToken={accessToken}
        setAccessToken={setAccessToken}
        excludedDirs={excludedDirs}
        setExcludedDirs={setExcludedDirs}
        excludedFiles={excludedFiles}
        setExcludedFiles={setExcludedFiles}
        includedDirs={includedDirs}
        setIncludedDirs={setIncludedDirs}
        includedFiles={includedFiles}
        setIncludedFiles={setIncludedFiles}
        onSubmit={handleGenerateWiki}
        isSubmitting={isSubmitting}
        authRequired={authRequired}
        authCode={authCode}
        setAuthCode={setAuthCode}
        isAuthLoading={isAuthLoading}
      />
    </div>
  );
}
