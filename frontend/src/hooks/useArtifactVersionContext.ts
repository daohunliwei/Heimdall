import { useEffect, useMemo, useState } from 'react';
import type { ReadonlyURLSearchParams } from 'next/navigation';
import type { RepositoryVersionSummary, WikiVersionSummary } from '@/types/wiki';

/**
 * 制品页面显式继承的版本上下文。
 * 该结构用于 Slides / Workshop 等下游页面，
 * 保证它们消费的版本与仓库页当前浏览版本保持一致。
 */
export interface ArtifactVersionContext {
  /** 当前仓库页选中的仓库快照版本 ID。 */
  repositoryVersionId?: string;
  /** 当前仓库页选中的 Wiki 版本 ID。 */
  wikiVersionId?: string;
}

/**
 * 制品页面版本上下文校验 Hook 的入参。
 */
interface UseArtifactVersionContextOptions {
  /** 当前仓库 ID。 */
  repositoryId: string;
  /** 当前语言，用于拉取 Wiki 版本目录。 */
  language: string;
  /** 当前页面查询参数。 */
  searchParams: ReadonlyURLSearchParams;
}

/**
 * 制品页面版本上下文校验 Hook 的输出。
 */
interface UseArtifactVersionContextResult {
  /** 解析后的版本上下文。 */
  versionContext: ArtifactVersionContext;
  /** 当前查询参数命中的 Wiki 版本摘要。 */
  wikiVersion?: WikiVersionSummary;
  /** 当前查询参数命中的仓库快照版本摘要。 */
  repositoryVersion?: RepositoryVersionSummary;
  /** 当前版本校验是否仍在执行中。 */
  isValidating: boolean;
  /** 当前版本上下文是否已通过前端校验。 */
  isReady: boolean;
  /** 当前版本上下文的校验失败信息。 */
  validationMessage?: string;
}

/**
 * 读取查询参数中的首个非空字符串。
 */
function pickSearchParamValue(
  searchParams: ReadonlyURLSearchParams,
  ...keys: string[]
): string | undefined {
  for (const key of keys) {
    const value = searchParams.get(key);
    if (value && value.trim().length > 0) {
      return value.trim();
    }
  }

  return undefined;
}

/**
 * 判断字符串是否为合法 GUID。
 * 当前前端使用该方法提前拦截错误链接，避免把无效版本参数提交给后端。
 */
function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

/**
 * 从查询参数中读取版本上下文。
 * 该方法同时兼容 camelCase 与 snake_case，避免不同阶段生成的链接失效。
 */
function parseArtifactVersionContext(searchParams: ReadonlyURLSearchParams): ArtifactVersionContext {
  return {
    repositoryVersionId: pickSearchParamValue(searchParams, 'repositoryVersionId', 'repository_version_id'),
    wikiVersionId: pickSearchParamValue(searchParams, 'wikiVersionId', 'wiki_version_id'),
  };
}

/**
 * 校验 Slides / Workshop 等页面继承的版本上下文。
 * 校验通过后会返回与查询参数精确匹配的版本摘要，供页面展示与任务请求复用。
 */
export function useArtifactVersionContext(
  options: UseArtifactVersionContextOptions,
): UseArtifactVersionContextResult {
  const { repositoryId, language, searchParams } = options;
  const versionContext = useMemo(
    () => parseArtifactVersionContext(searchParams),
    [searchParams],
  );
  const [wikiVersion, setWikiVersion] = useState<WikiVersionSummary | undefined>();
  const [repositoryVersion, setRepositoryVersion] = useState<RepositoryVersionSummary | undefined>();
  const [validationMessage, setValidationMessage] = useState<string | undefined>();
  const [isValidating, setIsValidating] = useState(true);

  useEffect(() => {
    let isCancelled = false;

    const validateVersionContext = async () => {
      setIsValidating(true);
      setWikiVersion(undefined);
      setRepositoryVersion(undefined);
      setValidationMessage(undefined);

      if (!versionContext.repositoryVersionId && !versionContext.wikiVersionId) {
        setValidationMessage('缺少 RepositoryVersion/WikiVersion 参数，请从仓库 Wiki 页面进入当前页面。');
        setIsValidating(false);
        return;
      }

      if (!versionContext.repositoryVersionId || !versionContext.wikiVersionId) {
        setValidationMessage('版本参数不完整，请同时携带 RepositoryVersion 与 WikiVersion 后重试。');
        setIsValidating(false);
        return;
      }

      if (!isGuid(versionContext.repositoryVersionId) || !isGuid(versionContext.wikiVersionId)) {
        setValidationMessage('版本参数格式无效，请从仓库 Wiki 页面重新打开当前页面。');
        setIsValidating(false);
        return;
      }

      try {
        const [wikiResponse, repositoryResponse] = await Promise.all([
          fetch(`/api/repositories/${repositoryId}/wiki/versions?language=${language}`),
          fetch(`/api/repositories/${repositoryId}/versions`),
        ]);

        if (!wikiResponse.ok || !repositoryResponse.ok) {
          throw new Error('读取版本目录失败');
        }

        const [wikiVersionList, repositoryVersionList] = await Promise.all([
          wikiResponse.json() as Promise<WikiVersionSummary[]>,
          repositoryResponse.json() as Promise<RepositoryVersionSummary[]>,
        ]);

        const matchedWikiVersion = wikiVersionList.find(
          (item) => item.wiki_version_id === versionContext.wikiVersionId,
        );
        if (!matchedWikiVersion) {
          throw new Error('指定的 WikiVersion 不存在或已被删除');
        }

        const matchedRepositoryVersion = repositoryVersionList.find(
          (item) => item.repository_version_id === versionContext.repositoryVersionId,
        );
        if (!matchedRepositoryVersion) {
          throw new Error('指定的 RepositoryVersion 不存在或已被删除');
        }

        if (matchedWikiVersion.repository_version_id !== matchedRepositoryVersion.repository_version_id) {
          throw new Error('当前链接中的 WikiVersion 与 RepositoryVersion 不匹配，请返回仓库页重新进入');
        }

        if (isCancelled) {
          return;
        }

        setWikiVersion(matchedWikiVersion);
        setRepositoryVersion(matchedRepositoryVersion);
      } catch (error) {
        if (isCancelled) {
          return;
        }

        setValidationMessage(
          error instanceof Error
            ? error.message
            : '版本上下文校验失败，请返回仓库页重新进入当前页面。',
        );
      } finally {
        if (!isCancelled) {
          setIsValidating(false);
        }
      }
    };

    validateVersionContext();

    return () => {
      isCancelled = true;
    };
  }, [language, repositoryId, versionContext.repositoryVersionId, versionContext.wikiVersionId]);

  return {
    versionContext,
    wikiVersion,
    repositoryVersion,
    isValidating,
    isReady: !isValidating && !validationMessage && Boolean(wikiVersion && repositoryVersion),
    validationMessage,
  };
}
