import { NextResponse } from 'next/server';

const TARGET_SERVER_BASE_URL = process.env.SERVER_BASE_URL || 'http://localhost:8001';
const ALLOWED_TASKS = new Set(['wiki', 'ask', 'slides', 'workshop']);

export async function POST(request: Request, { params }: { params: Promise<{ task: string }> }) {
  const resolvedParams = await params;
  if (!ALLOWED_TASKS.has(resolvedParams.task)) {
    return NextResponse.json({ error: 'Unsupported task' }, { status: 404 });
  }

  try {
    const targetUrl = `${TARGET_SERVER_BASE_URL}/tasks/${resolvedParams.task}`;
    const startedAt = Date.now();
    console.info('[tasks proxy] forward start', {
      task: resolvedParams.task,
      targetUrl,
    });
    const backendResponse = await fetch(targetUrl, {
      method: 'POST',
      headers: {
        'Content-Type': request.headers.get('content-type') ?? 'application/json',
        'Accept': request.headers.get('accept') ?? 'application/json',
      },
      body: await request.text(),
    });

    const contentType = backendResponse.headers.get('content-type') ?? 'application/json';
    console.info('[tasks proxy] forward done', {
      task: resolvedParams.task,
      status: backendResponse.status,
      elapsedMs: Date.now() - startedAt,
    });
    return new NextResponse(await backendResponse.text(), {
      status: backendResponse.status,
      headers: { 'Content-Type': contentType },
    });
  } catch (error) {
    console.error('[tasks proxy] forward failed', {
      task: resolvedParams.task,
      error: error instanceof Error ? error.message : String(error),
    });
    return NextResponse.json({ error: 'Proxy request failed', details: error instanceof Error ? error.message : String(error) }, { status: 500 });
  }
}

export function OPTIONS() {
  return new NextResponse(null, {
    status: 204,
    headers: {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, Authorization',
    },
  });
}
