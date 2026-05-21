'use client';

import React, { useState } from 'react';
import { FaSync, FaCog, FaCodeBranch } from 'react-icons/fa';
import UserSelector from '@/components/UserSelector';

/**
 * 刷新面板属性。
 */
interface RefreshPanelProps {
  /** 仓库唯一标识。 */
  repositoryId: string;
  /** 默认分支。 */
  defaultBranch?: string;
  /** 默认刷新策略。 */
  defaultRefreshStrategy?: 'current' | 'latest';
  /** 默认是否强制刷新。 */
  defaultForceRefresh?: boolean;
  /** 默认生成档位。 */
  defaultGenerationProfile?: 'concise' | 'comprehensive';
  /** 默认 Provider。 */
  defaultProvider?: string;
  /** 默认模型。 */
  defaultModel?: string;
  /** 提交刷新选项时的回调。 */
  onRefresh: (options: RefreshOptions) => void;
  /** 外层加载状态。 */
  isLoading?: boolean;
  /** 额外样式类名。 */
  className?: string;
}

/**
 * 刷新选项。
 */
export interface RefreshOptions {
  /** 目标分支。 */
  branch: string;
  /** 刷新策略。 */
  refreshStrategy: 'current' | 'latest';
  /** 是否强制重新生成。 */
  forceRefresh: boolean;
  /** 生成档位。 */
  generationProfile: 'concise' | 'comprehensive';
  /** 使用的模型提供方。 */
  provider: string;
  /** 使用的模型名称。 */
  model: string;
}

export default function RefreshPanel({
  repositoryId,
  defaultBranch = 'main',
  defaultRefreshStrategy = 'latest',
  defaultForceRefresh = false,
  defaultGenerationProfile = 'comprehensive',
  defaultProvider = 'ollama',
  defaultModel = 'gemma4:e2b',
  onRefresh,
  isLoading = false,
  className = '',
}: RefreshPanelProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [branch, setBranch] = useState(defaultBranch);
  const [refreshStrategy, setRefreshStrategy] = useState<'current' | 'latest'>(defaultRefreshStrategy);
  const [forceRefresh, setForceRefresh] = useState(defaultForceRefresh);
  const [generationProfile, setGenerationProfile] = useState<'concise' | 'comprehensive'>(defaultGenerationProfile);
  const [provider, setProvider] = useState(defaultProvider);
  const [model, setModel] = useState(defaultModel);
  const [isCustomModel, setIsCustomModel] = useState(false);
  const [customModel, setCustomModel] = useState('');

  // 当父组件的默认值变化时，同步到本地状态（仅在面板关闭时）
  if (!isOpen) {
    if (branch !== defaultBranch) setBranch(defaultBranch);
    if (refreshStrategy !== defaultRefreshStrategy) setRefreshStrategy(defaultRefreshStrategy);
    if (forceRefresh !== defaultForceRefresh) setForceRefresh(defaultForceRefresh);
    if (generationProfile !== defaultGenerationProfile) setGenerationProfile(defaultGenerationProfile);
    if (provider !== defaultProvider) setProvider(defaultProvider);
    if (model !== defaultModel) setModel(defaultModel);
  }

  /**
   * 提交刷新表单。
   */
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onRefresh({
      branch,
      refreshStrategy,
      forceRefresh,
      generationProfile,
      provider,
      model,
    });
    setIsOpen(false);
  };

  return (
    <div className={`refresh-panel ${className}`} data-repository-id={repositoryId}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        disabled={isLoading}
        className="btn-secondary w-full text-xs justify-center"
      >
        <FaSync className={isLoading ? 'animate-spin' : ''} />
        刷新 Wiki
      </button>

      {isOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setIsOpen(false)} />
          <div className="relative bg-[var(--card-bg)] rounded-xl shadow-2xl w-full max-w-md border border-[var(--border-color)] p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-semibold text-[var(--foreground)]">
                <FaCog className="inline mr-2" />刷新选项
              </h3>
              <button onClick={() => setIsOpen(false)} className="text-[var(--muted)] hover:text-[var(--foreground)]">
                ✕
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
              {/* 分支选择 */}
              <div>
                <label className="text-xs text-[var(--muted)] mb-1 block">
                  <FaCodeBranch className="inline mr-1" />分支
                </label>
                <input
                  type="text"
                  value={branch}
                  onChange={(e) => setBranch(e.target.value)}
                  className="input w-full text-sm"
                  placeholder="main"
                />
              </div>

              {/* 刷新策略 */}
              <div>
                <label className="text-xs text-[var(--muted)] mb-1 block" title="最新版本：拉取远程仓库最新代码后重新生成 Wiki；当前快照：基于已保存的仓库快照重新生成，不拉取新代码">
                  刷新策略 <span className="text-[var(--muted)]/60 ml-0.5 cursor-help" title="最新版本：拉取远程仓库最新代码后重新生成 Wiki；当前快照：基于已保存的仓库快照重新生成，不拉取新代码">ⓘ</span>
                </label>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => setRefreshStrategy('latest')}
                    className={`flex-1 px-3 py-1.5 rounded-md text-xs border transition-colors ${
                      refreshStrategy === 'latest'
                        ? 'border-[var(--accent-primary)] bg-[var(--accent-primary)]/10 text-[var(--accent-primary)]'
                        : 'border-[var(--border-color)] text-[var(--muted)]'
                    }`}
                  >
                    最新版本
                  </button>
                  <button
                    type="button"
                    onClick={() => setRefreshStrategy('current')}
                    className={`flex-1 px-3 py-1.5 rounded-md text-xs border transition-colors ${
                      refreshStrategy === 'current'
                        ? 'border-[var(--accent-primary)] bg-[var(--accent-primary)]/10 text-[var(--accent-primary)]'
                        : 'border-[var(--border-color)] text-[var(--muted)]'
                    }`}
                  >
                    当前版本
                  </button>
                </div>
              </div>

              {/* 强制刷新 */}
              <div className="flex items-center gap-2">
                <input
                  type="checkbox"
                  id="forceRefresh"
                  checked={forceRefresh}
                  onChange={(e) => setForceRefresh(e.target.checked)}
                  className="rounded"
                />
                <label htmlFor="forceRefresh" className="text-xs text-[var(--foreground)]">
                  强制刷新（即使版本不变也重新生成）
                </label>
              </div>

              {/* 生成档位 */}
              <div>
                <label className="text-xs text-[var(--muted)] mb-1 block" title="完整：生成全面的代码分析、架构图和模块文档；简洁：仅生成核心文件和入口点文档，速度更快">
                  生成档位 <span className="text-[var(--muted)]/60 ml-0.5 cursor-help" title="完整：生成全面的代码分析、架构图和模块文档；简洁：仅生成核心文件和入口点文档，速度更快">ⓘ</span>
                </label>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => setGenerationProfile('comprehensive')}
                    className={`flex-1 px-3 py-1.5 rounded-md text-xs border transition-colors ${
                      generationProfile === 'comprehensive'
                        ? 'border-[var(--accent-primary)] bg-[var(--accent-primary)]/10 text-[var(--accent-primary)]'
                        : 'border-[var(--border-color)] text-[var(--muted)]'
                    }`}
                  >
                    完整
                  </button>
                  <button
                    type="button"
                    onClick={() => setGenerationProfile('concise')}
                    className={`flex-1 px-3 py-1.5 rounded-md text-xs border transition-colors ${
                      generationProfile === 'concise'
                        ? 'border-[var(--accent-primary)] bg-[var(--accent-primary)]/10 text-[var(--accent-primary)]'
                        : 'border-[var(--border-color)] text-[var(--muted)]'
                    }`}
                  >
                    简洁
                  </button>
                </div>
              </div>

              {/* Provider / Model */}
              <div>
                <label className="text-xs text-[var(--muted)] mb-1 block">模型选择</label>
                <UserSelector
                  provider={provider}
                  setProvider={setProvider}
                  model={model}
                  setModel={setModel}
                  isCustomModel={isCustomModel}
                  setIsCustomModel={setIsCustomModel}
                  customModel={customModel}
                  setCustomModel={setCustomModel}
                />
              </div>

              {/* 提交 */}
              <div className="flex gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setIsOpen(false)}
                  className="btn-secondary flex-1 text-xs"
                >
                  取消
                </button>
                <button
                  type="submit"
                  disabled={isLoading}
                  className="btn-primary flex-1 text-xs"
                >
                  {isLoading ? '处理中...' : '开始刷新'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
