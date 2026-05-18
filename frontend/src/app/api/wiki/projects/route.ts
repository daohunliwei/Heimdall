import { NextResponse } from 'next/server';

const TARGET_SERVER_BASE_URL = process.env.SERVER_BASE_URL || 'http://localhost:8001';
const PROJECTS_API_ENDPOINT = `${TARGET_SERVER_BASE_URL}/api/processed_projects`;

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

    const projects = await response.json();
    return NextResponse.json(projects);
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : '未知错误';
    return NextResponse.json({ error: `无法连接后端服务：${message}` }, { status: 503 });
  }
}
