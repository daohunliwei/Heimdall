'use client';

import { RepositoryVersionSummary, WikiVersionSummary } from '@/types/wiki';
import React, { useEffect, useMemo, useState } from 'react';
import { FaCodeBranch, FaHistory, FaCheckCircle, FaClock } from 'react-icons/fa';

/**
 * 版本切换器属性。
 */
interface VersionSwitcherProps {
  /** 当前页面已选中的 Wiki 版本 ID。 */
  currentWikiVersionId?: string;
  /** 当前页面已选中的仓库快照版本 ID。 */
  currentRepositoryVersionId?: string;
  /** 当前仓库下可切换的 Wiki 版本列表。 */
  wikiVersions: WikiVersionSummary[];
  /** 当前仓库下可切换的仓库快照列表。 */
  repositoryVersions: RepositoryVersionSummary[];
  /** 版本数据是否仍在加载中。 */
  isLoading?: boolean;
  /** 版本切换回调。 */
  onVersionChange: (wikiVersionId: string, repositoryVersionId: string) => void;
  /** 额外样式类名。 */
  className?: string;
}

export default function VersionSwitcher({
  currentWikiVersionId,
  currentRepositoryVersionId,
  wikiVersions,
  repositoryVersions,
  isLoading = false,
  onVersionChange,
  className = '',
}: VersionSwitcherProps) {
  const [selectedWikiVersion, setSelectedWikiVersion] = useState<string>(currentWikiVersionId || '');
  const [isExpanded, setIsExpanded] = useState(false);

  useEffect(() => {
    setSelectedWikiVersion(currentWikiVersionId || '');
  }, [currentWikiVersionId]);

  /**
   * 当前已选中的 Wiki 版本。
   */
  const currentWikiVersion = useMemo(
    () => wikiVersions.find((version) => version.wiki_version_id === selectedWikiVersion),
    [selectedWikiVersion, wikiVersions],
  );

  /**
   * 处理 Wiki 版本切换。
   */
  const handleWikiVersionSelect = (versionId: string) => {
    setSelectedWikiVersion(versionId);
    const wikiVersion = wikiVersions.find((version) => version.wiki_version_id === versionId);
    if (wikiVersion?.repository_version_id) {
      onVersionChange(versionId, wikiVersion.repository_version_id);
    }
    setIsExpanded(false);
  };

  /**
   * 将时间格式化为中文日期。
   */
  const formatDate = (dateStr: string) => {
    try { return new Date(dateStr).toLocaleDateString('zh-CN'); } catch { return dateStr; }
  };

  /**
   * 截断提交 SHA，避免在下拉中占用过多宽度。
   */
  const truncateSha = (sha: string) => sha.length > 8 ? sha.substring(0, 8) : sha;

  /**
   * 渲染版本状态标签。
   */
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
              {wikiVersions.map((version) => (
                <button
                  key={version.wiki_version_id}
                  onClick={() => handleWikiVersionSelect(version.wiki_version_id)}
                  className={`w-full text-left px-3 py-2 rounded-md text-xs transition-colors ${
                    selectedWikiVersion === version.wiki_version_id
                      ? 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)]'
                      : 'hover:bg-[var(--background)] text-[var(--foreground)]'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-medium">v{version.version_no}</span>
                    {getStatusBadge(version.status)}
                  </div>
                  <div className="text-[var(--muted)] mt-0.5">
                    {formatDate(version.created_at)} · {version.page_count ?? 0} 页
                  </div>
                </button>
              ))}
            </div>
          )}

          {/* 仓库快照版本 */}
          <h4 className="text-xs font-semibold text-[var(--muted)] uppercase tracking-wider mb-2">
            <FaCodeBranch className="inline mr-1" />仓库快照
          </h4>
          {repositoryVersions.length === 0 ? (
            <p className="text-xs text-[var(--muted)] py-2">暂无快照</p>
          ) : (
            <div className="space-y-1">
              {repositoryVersions.slice(0, 5).map((version) => (
                <button
                  type="button"
                  key={version.repository_version_id}
                  onClick={() => {
                    onVersionChange('', version.repository_version_id);
                    setIsExpanded(false);
                  }}
                  className={`w-full text-left px-3 py-1.5 text-xs rounded-md cursor-pointer transition-colors ${
                    currentRepositoryVersionId === version.repository_version_id
                      ? 'bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] ring-1 ring-[var(--accent-primary)]/30'
                      : 'text-[var(--foreground)] hover:bg-[var(--card-bg)]'
                  }`}
                >
                  <div className="flex items-center gap-1.5">
                    <FaCodeBranch className="text-[var(--muted)]" />
                    <span>{version.branch_name}</span>
                    {version.is_latest_on_branch && <FaCheckCircle className="text-green-500" title="最新" />}
                  </div>
                  <div className="text-[var(--muted)] mt-0.5">
                    {truncateSha(version.commit_sha)} · {formatDate(version.commit_time)}
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {isLoading && <span className="text-xs text-[var(--muted)] ml-2">加载中...</span>}
    </div>
  );
}
