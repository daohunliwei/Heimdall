import RepoInfo from '@/types/repoinfo';
import getRepoUrl from '@/utils/getRepoUrl';

export interface TaskRequestOptions {
  repoInfo: RepoInfo;
  token?: string | null;
  provider?: string;
  model?: string;
  isCustomModel?: boolean;
  customModel?: string;
  language?: string;
  excludedDirs?: string;
  excludedFiles?: string;
  includedDirs?: string;
  includedFiles?: string;
}

export function buildTaskRequestBody(
  options: TaskRequestOptions,
  extra: Record<string, unknown> = {},
): Record<string, unknown> {
  const {
    repoInfo,
    token,
    provider,
    model,
    isCustomModel = false,
    customModel,
    language = 'zh',
    excludedDirs,
    excludedFiles,
    includedDirs,
    includedFiles,
  } = options;

  const body: Record<string, unknown> = {
    repo_url: getRepoUrl(repoInfo),
    owner: repoInfo.owner,
    repo: repoInfo.repo,
    type: repoInfo.type,
    language,
    ...extra,
  };

  if (token) {
    body.token = token;
  }

  if (provider) {
    body.provider = provider;
  }

  if (isCustomModel) {
    if (customModel) {
      body.custom_model = customModel;
    }
  } else if (model) {
    body.model = model;
  }

  if (excludedDirs) {
    body.excluded_dirs = excludedDirs;
  }

  if (excludedFiles) {
    body.excluded_files = excludedFiles;
  }

  if (includedDirs) {
    body.included_dirs = includedDirs;
  }

  if (includedFiles) {
    body.included_files = includedFiles;
  }

  return body;
}
