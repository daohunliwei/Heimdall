'use client';

import React, { useEffect, useState } from 'react';
import { FaCodeBranch, FaHistory, FaCheckCircle, FaClock } from 'react-icons/fa';

interface RepositoryVersion {
  repository_version_id: string;
  branch_name: string;
  commit_sha: string;
  commit_time: string;
  commit_author: string;
  commit_message: string;
  is_latest_on_branch: boolean;
  source_status: string;
}

interface WikiVersionInfo {
  wiki_version_id: string;
  version_no: number;
  generation_mode: string;
  status: string;
  page_count: number;
  created_at: string;
  completed_at: string;
  summary_markdown: string;
}

interface VersionSwitcherProps {
  repositoryId: string;
  currentWikiVersionId?: string;
  onVersionChange: (wikiVersionId: string, repositoryVersionId: string) => void;
  className?: string;
}

export default function VersionSwitcher({
  repositoryId,
  currentWikiVersionId,
  onVersionChange,
  className = '',
}: VersionSwitcherProps) {
  const [wikiVersions, setWikiVersions] = useState<WikiVersionInfo[]>([]);
  const [repoVersions, setRepoVersions] = useState<RepositoryVersion[]>([]);
  const [selectedWikiVersion, setSelectedWikiVersion] = useState<string>(currentWikiVersionId || '');
  const [isLoading, setIsLoading] = useState(true);
  const [isExpanded, setIsExpanded] = useState(false);

  useEffect(() => {
    async function loadVersions() {
      try {
        const [wikiResp, repoResp] = await Promise.all([
          fetch(`/api/repositories/${repositoryId}/wiki/versions`),
          fetch(`/api/repositories/${repositoryId}/versions`),
        ]);

        if (wikiResp.ok) {
          const wikiData = await wikiResp.json() as WikiVersionInfo[];
          setWikiVersions(wikiData);
        }

        if (repoResp.ok) {
          const repoData = await repoResp.json() as RepositoryVersion[];
          setRepoVersions(repoData);
        }
      } catch (e) {
        console.error('加载版本列表失败', e);
      } finally {
        setIsLoading(false);
      }
    }

    loadVersions();
  }, [repositoryId]);

  const currentWikiVersion = wikiVersions.find(v => v.wiki_version_id === selectedWikiVersion);

  const handleWikiVersionSelect = (versionId: string) => {
    setSelectedWikiVersion(versionId);
    const wikiVer = wikiVersions.find(v => v.wiki_version_id === versionId);
    if (wikiVer && repoVersions.length > 0) {
      onVersionChange(versionId, repoVersions[0].repository_version_id);
    }
    setIsExpanded(false);
  };

  const formatDate = (dateStr: string) => {
    try { return new Date(dateStr).toLocaleDateString('zh-CN'); } catch { return dateStr; }
  };

  const truncateSha = (sha: string) => sha.length > 8 ? sha.substring(0, 8) : sha;

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'published': return <span className="tag tag-primary text-xs">已发布</span>;
      case 'ready': return <span className="tag tag-default text-xs">就绪</span>;
      case 'generating': return <span className="tag tag-default text-xs">生成中</span>;
      case 'draft': return <span className="tag tag-default text-xs">草稿</span>;
      case 'failed': return <span className="tag tag-default text-xs">失败</span>;
      default: return <span className="tag tag-default text-xs">{status}</span>;
    }
  };

  return (
    <div className={`version-switcher ${className}`}>
      <div className="flex items-center gap-2">
        <button
          onClick={() => setIsExpanded(!isExpanded)}
          className="flex items-center gap-1.5 text-xs text-[var(--muted)] hover:text-[var(--foreground)] transition-colors"
        >
          <FaHistory className="text-xs" />
          <span>版本</span>
          {currentWikiVersion && (
            <span className="font-medium text-[var(--foreground)]">v{currentWikiVersion.version_no}</span>
          )}
        </button>
        {currentWikiVersion && getStatusBadge(currentWikiVersion.status)}
      </div>

      {isExpanded && (
        <div className="absolute z-50 mt-2 w-80 bg-[var(--card-bg)] border border-[var(--border-color)] rounded-lg shadow-lg p-3 max-h-96 overflow-y-auto">
          {/* Wiki 版本列表 */}
          <h4 className="text-xs font-semibold text-[var(--muted)] uppercase tracking-wider mb-2">
            <FaClock className="inline mr-1" />Wiki 生成版本
          </h4>
          {wikiVersions.length === 0 ? (
            <p className="text-xs text-[var(--muted)] py-2">暂无版本</p>
          ) : (
            <div className="space-y-1 mb-3">
              {wikiVersions.map(v => (
                <button
                  key={v.wiki_version_id}
                  onClick={() => handleWikiVersionSelect(v.wiki_version_id)}
                  className={`w-full text-left px-3 py-2 rounded-md text-xs transition-colors ${
                    selectedWikiVersion === v.wiki_version_id
                      ? 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)]'
                      : 'hover:bg-[var(--background)] text-[var(--foreground)]'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-medium">v{v.version_no}</span>
                    {getStatusBadge(v.status)}
                  </div>
                  <div className="text-[var(--muted)] mt-0.5">
                    {formatDate(v.created_at)} · {v.page_count ?? 0} 页
                  </div>
                </button>
              ))}
            </div>
          )}

          {/* 仓库快照版本 */}
          <h4 className="text-xs font-semibold text-[var(--muted)] uppercase tracking-wider mb-2">
            <FaCodeBranch className="inline mr-1" />仓库快照
          </h4>
          {repoVersions.length === 0 ? (
            <p className="text-xs text-[var(--muted)] py-2">暂无快照</p>
          ) : (
            <div className="space-y-1">
              {repoVersions.slice(0, 5).map(v => (
                <div key={v.repository_version_id} className="px-3 py-1.5 text-xs text-[var(--foreground)]">
                  <div className="flex items-center gap-1.5">
                    <FaCodeBranch className="text-[var(--muted)]" />
                    <span>{v.branch_name}</span>
                    {v.is_latest_on_branch && <FaCheckCircle className="text-green-500" title="最新" />}
                  </div>
                  <div className="text-[var(--muted)] mt-0.5">
                    {truncateSha(v.commit_sha)} · {formatDate(v.commit_time)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {isLoading && <span className="text-xs text-[var(--muted)] ml-2">加载中...</span>}
    </div>
  );
}
