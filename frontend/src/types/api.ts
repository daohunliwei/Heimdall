/**
 * 后端 API 响应类型统一定义。
 * 所有类型名与后端返回字段保持一致（snake_case 映射）。
 */

// ── 通用 ──

export interface ApiErrorResponse {
  error?: string;
  details?: string;
  request_id?: string;
}

// ── 仓库 ──

export interface RepositoryDetailResponse {
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

export interface RepositoryListItem {
  repository_id: string;
  display_name: string;
  owner: string;
  repo_name: string;
  repo_type: string;
  repo_url: string;
  default_branch: string;
  default_language: string;
  is_archived: boolean;
  created_at: string;
}

// ── 仓库版本 ──

export interface RepositoryVersionSummary {
  repository_version_id: string;
  branch_name: string;
  commit_sha: string;
  commit_time: string;
  commit_author: string;
  commit_message: string;
  is_latest_on_branch: boolean;
  source_status: string;
}

// ── Wiki 版本 ──

export interface WikiVersionSummary {
  wiki_version_id: string;
  wiki_space_id?: string;
  repository_version_id: string;
  version_no: number;
  generation_mode: string;
  generation_profile?: string;
  status: string;
  page_count: number;
  toc_depth?: number;
  summary_markdown?: string;
  created_at: string;
  completed_at?: string | null;
}

// ── Wiki 页面 ──

export interface WikiVersionPagePayload {
  id: string;
  title: string;
  content: string;
  page_type?: string;
  importance?: string;
  page_order?: number;
  file_paths?: string[];
  nav_title?: string | null;
  parent_page_id?: string | null;
  depth?: number;
  token_count?: number;
  status?: string;
  created_at?: string;
}

// ── 刷新/任务 ──

export interface WikiRefreshResponse {
  repository_version_id?: string;
  wiki_version_id?: string;
  result_type: string;
  change_status?: string;
  task_id?: string;
  message?: string;
}

export interface TaskStatusResponse {
  id: string;
  status: string;
  task_type?: string;
  progress_percent: number;
  progress_message?: string;
  error_message?: string;
  current_stage?: string;
  current_stage_status?: string;
  result_wiki_version_id?: string;
  resolved_repository_version_id?: string;
}

export interface TaskListItem {
  id: string;
  task_type: string;
  status: string;
  progress_percent: number;
  progress_message?: string;
  repository_id?: string;
  source_branch?: string;
  created_at: string;
}

// ── Ask/Chat ──

export interface ChatRequest {
  question: string;
  repository_id: string;
  provider?: string;
  model?: string;
  language?: string;
  repository_version_id?: string;
  wiki_version_id?: string;
}

export interface ChatResponse {
  answer: string;
  task_id?: string;
}

// ── Slides ──

export interface SlidesRequest {
  repository_id: string;
  provider?: string;
  model?: string;
  language?: string;
  repository_version_id?: string;
  wiki_version_id?: string;
}

export interface SlidesResponse {
  task_id: string;
  slides?: SlideItem[];
  html?: string;
}

export interface SlideItem {
  title: string;
  content: string;
  notes?: string;
}

// ── Workshop ──

export interface WorkshopRequest {
  repository_id: string;
  provider?: string;
  model?: string;
  language?: string;
  repository_version_id?: string;
  wiki_version_id?: string;
}

export interface WorkshopResponse {
  task_id: string;
  title?: string;
  sections?: WorkshopSection[];
}

export interface WorkshopSection {
  title: string;
  content: string;
  exercises?: string[];
}

// ── 模型配置 ──

export interface ModelConfigResponse {
  default_provider: string;
  default_model: string;
  available_providers: string[];
  provider_models: Record<string, string[]>;
}

// ── 项目列表 ──

export interface ProcessedProject {
  id: string;
  repository_id: string;
  display_name: string;
  owner: string;
  repo_name: string;
  repo_type: string;
  repo_url: string;
  page_count: number;
  last_generated_at?: string;
  status: string;
}
