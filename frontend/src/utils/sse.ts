import { readResponseTextSafely } from '@/utils/response';

export async function streamSseText(
  input: RequestInfo | URL,
  init: RequestInit,
  onData: (chunk: string) => void
): Promise<void> {
  const response = await fetch(input, init);
  const contentType = response.headers.get('content-type') ?? '';

  if (!response.ok) {
    const errorText = await readResponseTextSafely(response);
    throw new Error(extractErrorMessage(response, errorText));
  }

  if (!contentType.includes('text/event-stream')) {
    const responseText = await readResponseTextSafely(response);
    if (!responseText.trim()) {
      throw new Error('服务端返回了空响应，请检查模型配置后重试。');
    }

    onData(responseText);
    return;
  }

  const reader = response.body?.getReader();
  if (!reader) {
    throw new Error('无法读取响应流');
  }

  const decoder = new TextDecoder();
  let buffer = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    buffer = processSseBuffer(buffer, onData);
  }

  buffer += decoder.decode();
  processSseBuffer(buffer, onData, true);
}

function processSseBuffer(
  buffer: string,
  onData: (chunk: string) => void,
  flush = false
): string {
  const parts = buffer.split('\n\n');
  const remainder = flush ? '' : (parts.pop() ?? '');

  for (const part of parts) {
    const lines = part
      .split('\n')
      .map((line) => line.trimEnd())
      .filter(Boolean);

    const dataLines = lines
      .filter((line) => line.startsWith('data:'))
      .map((line) => line.slice(5).trimStart());

    if (dataLines.length === 0) {
      continue;
    }

    const data = dataLines.join('\n');
    if (data === '[DONE]') {
      continue;
    }

    onData(data);
  }

  return remainder;
}

function extractErrorMessage(response: Response, errorText: string): string {
  const trimmed = errorText.trim();
  if (!trimmed) {
    return `请求失败：${response.status} ${response.statusText}`.trim();
  }

  try {
    const parsed = JSON.parse(trimmed) as { error?: string; message?: string };
    if (typeof parsed.error === 'string' && parsed.error.trim()) {
      return parsed.error;
    }

    if (typeof parsed.message === 'string' && parsed.message.trim()) {
      return parsed.message;
    }
  } catch {
    // Ignore JSON parsing failures and fall back to raw text.
  }

  return trimmed;
}
