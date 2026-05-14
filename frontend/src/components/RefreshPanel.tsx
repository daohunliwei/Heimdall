'use client';

import React, { useState } from 'react';
import { FaSync, FaCog, FaCodeBranch } from 'react-icons/fa';

interface RefreshPanelProps {
  repositoryId: string;
  defaultBranch?: string;
  onRefresh: (options: RefreshOptions) => void;
  isLoading?: boolean;
  className?: string;
}

export interface RefreshOptions {
  branch: string;
  refreshStrategy: 'current' | 'latest';
  forceRefresh: boolean;
  generationProfile: 'concise' | 'comprehensive';
  provider: string;
  model: string;
}

export default function RefreshPanel({
  repositoryId,
  defaultBranch = 'main',
  onRefresh,
  isLoading = false,
  className = '',
}: RefreshPanelProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [branch, setBranch] = useState(defaultBranch);
  const [refreshStrategy, setRefreshStrategy] = useState<'current' | 'latest'>('latest');
  const [forceRefresh, setForceRefresh] = useState(false);
  const [generationProfile, setGenerationProfile] = useState<'concise' | 'comprehensive'>('comprehensive');
  const [provider, setProvider] = useState('ollama');
  const [model, setModel] = useState('gemma4:e2b');

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
    <div className={`refresh-panel ${className}`}>
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
                <label className="text-xs text-[var(--muted)] mb-1 block">刷新策略</label>
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
                <label className="text-xs text-[var(--muted)] mb-1 block">生成档位</label>
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
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="text-xs text-[var(--muted)] mb-1 block">Provider</label>
                  <select
                    value={provider}
                    onChange={(e) => setProvider(e.target.value)}
                    className="input w-full text-xs"
                  >
                    <option value="ollama">Ollama</option>
                    <option value="openai">OpenAI</option>
                    <option value="google">Google</option>
                  </select>
                </div>
                <div>
                  <label className="text-xs text-[var(--muted)] mb-1 block">Model</label>
                  <select
                    value={model}
                    onChange={(e) => setModel(e.target.value)}
                    className="input w-full text-xs"
                  >
                    <option value="gemma4:e2b">gemma4:e2b</option>
                    <option value="qwen3">qwen3</option>
                    <option value="llama4">llama4</option>
                  </select>
                </div>
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
