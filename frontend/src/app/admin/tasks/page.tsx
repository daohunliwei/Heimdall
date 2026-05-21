"use client";

import React, { useEffect, useState, useCallback } from "react";

interface TaskInfo {
  id: string;
  task_type: string;
  status: string;
  progress_percent: number;
  progress_message: string | null;
  input_tokens: number;
  output_tokens: number;
  cache_hit_tokens: number;
  estimated_cost: number;
  total_calls: number;
  created_at: string;
  started_at: string | null;
  completed_at: string | null;
  error_message: string | null;
}

interface CallDetail {
  stage: string;
  provider: string;
  model: string;
  inputTokens: number;
  outputTokens: number;
  cacheHitTokens: number;
  latencyMs: number;
  success: boolean;
  errorType: string | null;
  createdAt: string;
}

const statusBadge: Record<string, string> = {
  pending: "tag tag-default",
  running: "bg-[var(--accent-primary)]/10 text-[var(--accent-primary)] border border-[var(--accent-primary)]/20 inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-medium",
  completed: "bg-[var(--success)]/10 text-[var(--success)] border border-[var(--success)]/20 inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-medium",
  failed: "bg-[var(--highlight)]/10 text-[var(--highlight)] border border-[var(--highlight)]/20 inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-medium",
  cancelled: "tag tag-default",
};

const statusLabels: Record<string, string> = {
  pending: "等待中", running: "运行中", completed: "已完成", failed: "失败", cancelled: "已取消",
};

const typeLabels: Record<string, string> = {
  wiki: "Wiki 生成", ask: "Ask 问答", slides: "幻灯片", workshop: "工作坊",
};

const fmtTokens = (n: number | null | undefined) =>
  n == null || n === 0 ? "—" : n >= 1000000 ? `${(n / 1000000).toFixed(1)}M` : n >= 1000 ? `${(n / 1000).toFixed(0)}K` : `${n}`;

const fmtCost = (n: number | null | undefined) =>
  n == null || n === 0 ? "—" : `¥${Number(n).toFixed(4)}`;

export default function TasksPage() {
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [filter, setFilter] = useState("");
  const [expanded, setExpanded] = useState<string | null>(null);
  const [details, setDetails] = useState<CallDetail[]>([]);
  const [loadingDetails, setLoadingDetails] = useState(false);

  const fetchTasks = useCallback(() => {
    const params = new URLSearchParams({ limit: "50" });
    if (filter) params.set("status", filter);
    fetch(`/api/admin/tasks?${params}`)
      .then((r) => r.json())
      .then((d) => setTasks(d.tasks || []))
      .catch(() => {});
  }, [filter]);

  useEffect(() => { fetchTasks(); }, [fetchTasks]);

  async function fetchDetails(taskId: string) {
    setLoadingDetails(true);
    try {
      const res = await fetch(`/api/admin/tasks/${taskId}/details`);
      const data = await res.json();
      setDetails(Array.isArray(data) ? data : []);
    } catch { setDetails([]); }
    setLoadingDetails(false);
  }

  async function toggleExpand(taskId: string) {
    if (expanded === taskId) { setExpanded(null); setDetails([]); return; }
    setExpanded(taskId);
    await fetchDetails(taskId);
  }

  async function handleCancel(id: string) {
    await fetch(`/api/admin/tasks/${id}/cancel`, { method: "POST" });
    fetchTasks();
  }

  // Stats
  const totalTasks = tasks.length;
  const totalInput = tasks.reduce((s, t) => s + (t.input_tokens || 0), 0);
  const totalOutput = tasks.reduce((s, t) => s + (t.output_tokens || 0), 0);
  const totalCost = tasks.reduce((s, t) => s + (Number(t.estimated_cost) || 0), 0);
  const totalCalls = tasks.reduce((s, t) => s + (t.total_calls || 0), 0);
  const totalCacheHit = tasks.reduce((s, t) => s + (t.cache_hit_tokens || 0), 0);
  const avgCacheRate = totalInput > 0 ? (totalCacheHit / totalInput * 100) : 0;

  return (
    <div>
      <h2 className="mb-4 text-xl font-bold text-[var(--foreground)]">任务监控</h2>

      {/* 统计卡片 */}
      <div className="mb-4 grid grid-cols-2 md:grid-cols-4 gap-3">
        {[
          { label: "总任务数", value: `${totalTasks}`, sub: `${tasks.filter(t => t.status === "running").length} 运行中` },
          { label: "Token 消耗", value: `${fmtTokens(totalInput)}↓ / ${fmtTokens(totalOutput)}↑`, sub: avgCacheRate > 0 ? `缓存命中 ${avgCacheRate.toFixed(1)}%` : "" },
          { label: "累计成本", value: fmtCost(totalCost), sub: `${tasks.filter(t => t.status === "completed").length} 已完成` },
          { label: "LLM 调用", value: `${totalCalls}`, sub: `${tasks.filter(t => t.status === "failed").length} 失败` },
        ].map((card) => (
          <div key={card.label} className="card p-4">
            <p className="text-xs text-[var(--muted)]">{card.label}</p>
            <p className="mt-1 text-xl font-bold text-[var(--foreground)]">{card.value}</p>
            {card.sub && <p className="mt-0.5 text-xs text-[var(--muted)]">{card.sub}</p>}
          </div>
        ))}
      </div>

      {/* 筛选栏 */}
      <div className="mb-4 flex items-center gap-3">
        <select value={filter} onChange={(e) => setFilter(e.target.value)} className="input text-sm w-auto">
          <option value="">全部状态</option>
          <option value="pending">等待中</option>
          <option value="running">运行中</option>
          <option value="completed">已完成</option>
          <option value="failed">失败</option>
          <option value="cancelled">已取消</option>
        </select>
        <button onClick={fetchTasks} className="btn-secondary text-sm">刷新</button>
      </div>

      {/* 任务表格 */}
      <div className="overflow-x-auto rounded-lg border border-[var(--border-color)]">
        <table className="w-full text-sm">
          <thead className="bg-[var(--background)]">
            <tr>
              <th className="px-3 py-2 text-left">ID</th>
              <th className="px-3 py-2 text-left">类型</th>
              <th className="px-3 py-2 text-left">状态</th>
              <th className="px-3 py-2 text-right">进度</th>
              <th className="px-3 py-2 text-right">输入↓</th>
              <th className="px-3 py-2 text-right">输出↑</th>
              <th className="px-3 py-2 text-right">缓存</th>
              <th className="px-3 py-2 text-right">成本</th>
              <th className="px-3 py-2 text-right">调用</th>
              <th className="px-3 py-2 text-left">时间</th>
              <th className="px-3 py-2 text-right">操作</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border-color)]">
            {tasks.map((t) => (
              <React.Fragment key={t.id}>
                <tr className="bg-[var(--card-bg)] hover:bg-[var(--background)] cursor-pointer"
                  onClick={() => toggleExpand(t.id)}>
                  <td className="px-3 py-2 font-mono text-xs" title={t.id}>{t.id.slice(0, 8)}</td>
                  <td className="px-3 py-2">{typeLabels[t.task_type] || t.task_type}</td>
                  <td className="px-3 py-2"><span className={statusBadge[t.status] || "tag tag-default"}>{statusLabels[t.status] || t.status}</span></td>
                  <td className="px-3 py-2 text-right font-mono">{t.progress_percent}%</td>
                  <td className="px-3 py-2 text-right font-mono text-xs">{fmtTokens(t.input_tokens)}</td>
                  <td className="px-3 py-2 text-right font-mono text-xs">{fmtTokens(t.output_tokens)}</td>
                  <td className="px-3 py-2 text-right font-mono text-xs">{t.cache_hit_tokens > 0 ? `${fmtTokens(t.cache_hit_tokens)}` : "—"}</td>
                  <td className="px-3 py-2 text-right font-mono text-xs">{fmtCost(t.estimated_cost)}</td>
                  <td className="px-3 py-2 text-right font-mono text-xs">{t.total_calls || "—"}</td>
                  <td className="px-3 py-2 text-xs text-[var(--muted)]">{new Date(t.created_at).toLocaleString()}</td>
                  <td className="px-3 py-2 text-right space-x-1" onClick={(e) => e.stopPropagation()}>
                    {t.status === "running" && (
                      <button onClick={() => handleCancel(t.id)} className="text-xs text-[var(--warning)] hover:underline">取消</button>
                    )}
                    {t.status === "failed" && (
                      <button onClick={() => { fetch(`/api/admin/tasks/${t.id}/retry`, { method: "POST" }).then(() => fetchTasks()); }} className="text-xs text-[var(--accent-primary)] hover:underline">重试</button>
                    )}
                  </td>
                </tr>
                {expanded === t.id && (
                  <tr key={`${t.id}-details`}>
                    <td colSpan={11} className="p-3 bg-[var(--background)]">
                      <h4 className="mb-2 text-sm font-semibold text-[var(--foreground)]">LLM 调用明细</h4>
                      {loadingDetails ? <p className="text-xs text-[var(--muted)]">加载中...</p>
                       : details.length === 0 ? <p className="text-xs text-[var(--muted)]">暂无调用记录</p>
                       : (
                        <table className="w-full text-xs">
                          <thead>
                            <tr className="text-[var(--muted)]">
                              <th className="px-2 py-1 text-left">阶段</th>
                              <th className="px-2 py-1 text-left">Provider/Model</th>
                              <th className="px-2 py-1 text-right">输入</th>
                              <th className="px-2 py-1 text-right">输出</th>
                              <th className="px-2 py-1 text-right">缓存</th>
                              <th className="px-2 py-1 text-right">延迟</th>
                              <th className="px-2 py-1 text-center">状态</th>
                              <th className="px-2 py-1 text-left">时间</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-[var(--border-color)]">
                            {details.map((d, i) => (
                              <tr key={i}>
                                <td className="px-2 py-1">{d.stage}</td>
                                <td className="px-2 py-1 font-mono text-[var(--muted)]">{d.provider}/{d.model}</td>
                                <td className="px-2 py-1 text-right font-mono">{fmtTokens(d.inputTokens)}</td>
                                <td className="px-2 py-1 text-right font-mono">{fmtTokens(d.outputTokens)}</td>
                                <td className="px-2 py-1 text-right font-mono">{d.cacheHitTokens > 0 ? fmtTokens(d.cacheHitTokens) : "—"}</td>
                                <td className="px-2 py-1 text-right font-mono">{d.latencyMs > 0 ? `${(d.latencyMs / 1000).toFixed(1)}s` : "—"}</td>
                                <td className="px-2 py-1 text-center">{d.success ? "✓" : `✗ ${d.errorType || ""}`}</td>
                                <td className="px-2 py-1 text-[var(--muted)]">{new Date(d.createdAt).toLocaleTimeString()}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      )}
                    </td>
                  </tr>
                )}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
