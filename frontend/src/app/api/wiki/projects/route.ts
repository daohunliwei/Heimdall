import { NextResponse } from 'next/server';

interface ApiProcessedProject {
  id: string;
  owner: string;
  repo: string;
  name: string;
  repo_type: string;
  submittedAt: number;
  language: string;
}

interface DeleteProjectCachePayload {
  owner: string;
  repo: string;
  repo_type: string;
  language: string;
}

function isDeleteProjectCachePayload(obj: unknown): obj is DeleteProjectCachePayload {
  return (
    obj != null &&
    typeof obj === 'object' &&
    'owner' in obj && typeof (obj as Record<string, unknown>).owner === 'string' && ((obj as Record<string, unknown>).owner as string).trim() !== '' &&
    'repo' in obj && typeof (obj as Record<string, unknown>).repo === 'string' && ((obj as Record<string, unknown>).repo as string).trim() !== '' &&
    'repo_type' in obj && typeof (obj as Record<string, unknown>).repo_type === 'string' && ((obj as Record<string, unknown>).repo_type as string).trim() !== '' &&
    'language' in obj && typeof (obj as Record<string, unknown>).language === 'string' && ((obj as Record<string, unknown>).language as string).trim() !== ''
  );
}

const TARGET_SERVER_BASE_URL = process.env.SERVER_BASE_URL || 'http://localhost:8001';
const PROJECTS_API_ENDPOINT = `${TARGET_SERVER_BASE_URL}/api/processed_projects`;
const CACHE_API_ENDPOINT = `${TARGET_SERVER_BASE_URL}/api/wiki_cache`;

export async function GET() {
  try {
    const response = await fetch(PROJECTS_API_ENDPOINT, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      cache: 'no-store',
    });

    if (!response.ok) {
      let errorBody = { error: `获取项目列表失败：${response.statusText}` };
      try {
        errorBody = await response.json();
      } catch {
      }
      return NextResponse.json(errorBody, { status: response.status });
    }

    const projects: ApiProcessedProject[] = await response.json();
    return NextResponse.json(projects);
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : '未知错误';
    return NextResponse.json({ error: `无法连接后端服务：${message}` }, { status: 503 });
  }
}

export async function DELETE(request: Request) {
  try {
    const body: unknown = await request.json();
    if (!isDeleteProjectCachePayload(body)) {
      return NextResponse.json({ error: '请求体不合法。' }, { status: 400 });
    }

    const { owner, repo, repo_type, language } = body;
    const params = new URLSearchParams({ owner, repo, repo_type, language });
    const response = await fetch(`${CACHE_API_ENDPOINT}?${params}`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
    });

    if (!response.ok) {
      let errorBody = { error: response.statusText };
      try {
        errorBody = await response.json();
      } catch {
      }
      return NextResponse.json(errorBody, { status: response.status });
    }

    return NextResponse.json({ message: '项目删除成功' });
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : '未知错误';
    return NextResponse.json({ error: `删除项目失败：${message}` }, { status: 500 });
  }
}
