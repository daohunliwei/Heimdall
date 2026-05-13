"use client";

import { useEffect, useState } from "react";

interface TokenSummary {
  prompt_tokens: number;
  completion_tokens: number;
  total_tokens: number;
  call_count: number;
  total_cost: number;
}

interface LlmCallLog {
  id: string;
  step_order: number;
  call_type: string;
  provider: string | null;
  model: string | null;
  prompt_tokens: number;
  completion_tokens: number;
  latency_ms: number;
  is_error: boolean;
}

interface TaskLlmCallSummaryProps {
  taskId: string;
}

const callTypeLabels: Record<string, string> = {
  structure_generation: "结构规划",
  page_generation: "页面生成",
  rag_query: "RAG 检索",
  deep_research: "深度研究",
  slide_generation: "幻灯片生成",
  workshop_generation: "工作坊生成",
};

export default function TaskLlmCallSummary({ taskId }: TaskLlmCallSummaryProps) {
  const [summary, setSummary] = useState<TokenSummary | null>(null);
  const [logs, setLogs] = useState<LlmCallLog[]>([]);
  const [expanded, setExpanded] = useState(false);

  useEffect(() => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
    fetch(`${baseUrl}/tasks/${taskId}/token-summary`)
      .then((r) => r.json())
      .then(setSummary)
      .catch(() => {});
    fetch(`${baseUrl}/tasks/${taskId}/llm-calls`)
      .then((r) => r.json())
      .then(setLogs)
      .catch(() => {});
  }, [taskId]);

  if (!summary) return null;

  return (
    <div className="mt-6 rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-700 dark:bg-gray-800/50">
      <h3 className="text-sm font-semibold text-gray-700 dark:text-gray-200">生成统计</h3>
      <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-gray-600 dark:text-gray-400">
        <span>Prompt Token</span><span className="text-right">{summary.prompt_tokens.toLocaleString()}</span>
        <span>Completion Token</span><span className="text-right">{summary.completion_tokens.toLocaleString()}</span>
        <span>总 Token</span><span className="text-right font-medium">{summary.total_tokens.toLocaleString()}</span>
        <span>LLM 调用次数</span><span className="text-right">{summary.call_count} 次</span>
        <span>估算成本</span><span className="text-right">${summary.total_cost.toFixed(2)}</span>
      </div>
      {logs.length > 0 && (
        <button
          onClick={() => setExpanded(!expanded)}
          className="mt-3 text-xs text-blue-600 hover:underline"
        >
          {expanded ? "收起明细" : "展开调用明细"}
        </button>
      )}
      {expanded && logs.length > 0 && (
        <div className="mt-2 max-h-60 overflow-y-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="text-left text-gray-500">
                <th className="py-1">步骤</th>
                <th>类型</th>
                <th>Tokens</th>
                <th>耗时</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((log) => (
                <tr key={log.step_order} className="border-t border-gray-200 dark:border-gray-700">
                  <td className="py-1">{log.step_order}</td>
                  <td>{callTypeLabels[log.call_type] || log.call_type}</td>
                  <td>{(log.prompt_tokens + log.completion_tokens).toLocaleString()}</td>
                  <td>{log.latency_ms}ms</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
