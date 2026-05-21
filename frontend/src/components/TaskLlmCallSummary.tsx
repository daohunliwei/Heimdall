"use client";

import { useEffect, useState, useCallback } from "react";

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

// V7: 新版指标接口（来自 LlmMetricsController）
interface V7MetricsSummary {
  taskId: string;
  totalCalls: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalCacheHitTokens: number;
  cacheHitRate: number;
  averageLatencyMs: number;
  estimatedCost: number;
  stages: { stage: string; calls: number; inputTokens: number; outputTokens: number; avgLatency: number }[];
}

interface TaskLlmCallSummaryProps {
  taskId: string;
  isRunning?: boolean;
}

const callTypeLabels: Record<string, string> = {
  structure_generation: "结构规划",
  page_generation: "页面生成",
  code_understanding: "代码理解",
  rag_query: "RAG 检索",
  deep_research: "深度研究",
  slide_generation: "幻灯片生成",
  workshop_generation: "工作坊生成",
};

export default function TaskLlmCallSummary({ taskId, isRunning = false }: TaskLlmCallSummaryProps) {
  const [summary, setSummary] = useState<TokenSummary | null>(null);
  const [v7Metrics, setV7Metrics] = useState<V7MetricsSummary | null>(null);
  const [logs, setLogs] = useState<LlmCallLog[]>([]);
  const [expanded, setExpanded] = useState(false);

  const fetchMetrics = useCallback(() => {
    // 优先尝试 V7 接口
    fetch(`/api/tasks/${taskId}/metrics`)
      .then((r) => r.ok ? r.json() : null)
      .then((data) => { if (data) setV7Metrics(data); })
      .catch(() => {});

    // 兼容旧接口
    fetch(`/api/tasks/${taskId}/token-summary`)
      .then((r) => r.ok ? r.json() : null)
      .then((data) => { if (data) setSummary(data); })
      .catch(() => {});
    fetch(`/api/tasks/${taskId}/llm-calls`)
      .then((r) => r.ok ? r.json() : null)
      .then((data) => { if (data) setLogs(data); })
      .catch(() => {});
  }, [taskId]);

  useEffect(() => {
    fetchMetrics();
    // V7: 运行中时每 5 秒轮询更新
    if (isRunning) {
      const interval = setInterval(fetchMetrics, 5000);
      return () => clearInterval(interval);
    }
  }, [taskId, isRunning, fetchMetrics]);

  // V7 指标优先展示
  if (v7Metrics) {
    return (
      <div className="card p-4 mt-6">
        <h3 className="text-sm font-semibold text-[var(--foreground)]">
          LLM 调用统计 {isRunning && <span className="ml-2 text-xs text-green-500 animate-pulse">● 实时</span>}
        </h3>
        <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-[var(--muted)]">
          <span>LLM 调用次数</span><span className="text-right">{v7Metrics.totalCalls} 次</span>
          <span>Input Tokens</span><span className="text-right">{v7Metrics.totalInputTokens.toLocaleString()}</span>
          <span>Output Tokens</span><span className="text-right">{v7Metrics.totalOutputTokens.toLocaleString()}</span>
          <span>缓存命中</span><span className="text-right">{v7Metrics.totalCacheHitTokens.toLocaleString()} ({(v7Metrics.cacheHitRate * 100).toFixed(0)}%)</span>
          <span>平均延迟</span><span className="text-right">{v7Metrics.averageLatencyMs.toFixed(0)}ms</span>
          <span>估算成本</span><span className="text-right font-medium">${v7Metrics.estimatedCost.toFixed(4)}</span>
        </div>
        {v7Metrics.stages.length > 0 && (
          <>
            <button
              onClick={() => setExpanded(!expanded)}
              className="mt-3 text-xs text-[var(--accent-primary)] hover:underline"
            >
              {expanded ? "收起阶段明细" : "展开阶段明细"}
            </button>
            {expanded && (
              <div className="mt-2 max-h-48 overflow-y-auto">
                <table className="w-full text-xs">
                  <thead>
                    <tr className="text-left text-[var(--muted)]">
                      <th className="py-1">阶段</th>
                      <th>调用</th>
                      <th>Tokens</th>
                      <th>延迟</th>
                    </tr>
                  </thead>
                  <tbody>
                    {v7Metrics.stages.map((stage) => (
                      <tr key={stage.stage} className="border-t border-[var(--border-color)]">
                        <td className="py-1">{callTypeLabels[stage.stage] || stage.stage}</td>
                        <td>{stage.calls}</td>
                        <td>{(stage.inputTokens + stage.outputTokens).toLocaleString()}</td>
                        <td>{stage.avgLatency.toFixed(0)}ms</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>
    );
  }

  if (!summary) return null;

  return (
    <div className="card p-4 mt-6">
      <h3 className="text-sm font-semibold text-[var(--foreground)]">生成统计</h3>
      <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-[var(--muted)]">
        <span>Prompt Token</span><span className="text-right">{summary.prompt_tokens.toLocaleString()}</span>
        <span>Completion Token</span><span className="text-right">{summary.completion_tokens.toLocaleString()}</span>
        <span>总 Token</span><span className="text-right font-medium">{summary.total_tokens.toLocaleString()}</span>
        <span>LLM 调用次数</span><span className="text-right">{summary.call_count} 次</span>
        <span>估算成本</span><span className="text-right">${summary.total_cost.toFixed(2)}</span>
      </div>
      {logs.length > 0 && (
        <button
          onClick={() => setExpanded(!expanded)}
          className="mt-3 text-xs text-[var(--accent-primary)] hover:underline"
        >
          {expanded ? "收起明细" : "展开调用明细"}
        </button>
      )}
      {expanded && logs.length > 0 && (
        <div className="mt-2 max-h-60 overflow-y-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="text-left text-[var(--muted)]">
                <th className="py-1">步骤</th>
                <th>类型</th>
                <th>Tokens</th>
                <th>耗时</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((log) => (
                <tr key={log.step_order} className="border-t border-[var(--border-color)]">
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
