"use client";

import { useEffect, useState } from "react";

interface TaskInfo {
  id: string;
  task_type: string;
  status: string;
  progress_percent: number;
  progress_message: string | null;
  total_prompt_tokens: number;
  total_completion_tokens: number;
  error_message: string | null;
  created_at: string;
  started_at: string | null;
  completed_at: string | null;
}

const statusBadge: Record<string, string> = {
  pending: "tag tag-default",
  running: "tag-primary",
  completed: "bg-[var(--success)]/10 text-[var(--success)] border border-[var(--success)]/20 inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-medium",
  failed: "bg-[var(--highlight)]/10 text-[var(--highlight)] border border-[var(--highlight)]/20 inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-medium",
  cancelled: "tag tag-default",
};

const typeLabels: Record<string, string> = {
  wiki: "Wiki 生成",
  ask: "Ask 问答",
  slides: "幻灯片",
  workshop: "工作坊",
};

export default function TasksPage() {
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [filter, setFilter] = useState("");

  useEffect(() => {
    const params = new URLSearchParams({ limit: "100" });
    if (filter) params.set("status", filter);
    fetch(`/api/admin/tasks?${params}`)
      .then((r) => r.json())
      .then((d) => setTasks(d.tasks || []))
      .catch(() => {});
  }, [filter]);

  async function handleCancel(id: string) {
    await fetch(`/api/admin/tasks/${id}/cancel`, { method: "POST" });
    window.location.reload();
  }

  async function handleRetry(id: string) {
    await fetch(`/api/admin/tasks/${id}/retry`, { method: "POST" });
    window.location.reload();
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-[var(--foreground)]">任务监控</h2>
        <select
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="input text-sm w-auto"
        >
          <option value="">全部</option>
          <option value="pending">等待中</option>
          <option value="running">运行中</option>
          <option value="completed">已完成</option>
          <option value="failed">失败</option>
          <option value="cancelled">已取消</option>
        </select>
      </div>

      <div className="overflow-x-auto rounded-lg border border-[var(--border-color)]">
        <table className="w-full text-sm">
          <thead className="bg-[var(--background)]">
            <tr>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">ID</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">类型</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">状态</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">进度</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">Token</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">创建时间</th>
              <th className="px-4 py-2 text-right text-[var(--foreground)]">操作</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border-color)]">
            {tasks.map((t) => (
              <tr key={t.id} className="bg-[var(--card-bg)]">
                <td className="px-4 py-2 font-mono text-xs text-[var(--foreground)]">{t.id.slice(0, 8)}</td>
                <td className="px-4 py-2 text-[var(--foreground)]">{typeLabels[t.task_type] || t.task_type}</td>
                <td className="px-4 py-2">
                  <span className={statusBadge[t.status] || "tag tag-default"}>
                    {t.status}
                  </span>
                </td>
                <td className="px-4 py-2 text-[var(--foreground)]">{t.progress_percent}%</td>
                <td className="px-4 py-2 text-[var(--foreground)]">{t.total_prompt_tokens + t.total_completion_tokens}</td>
                <td className="px-4 py-2 text-xs text-[var(--muted)]">{new Date(t.created_at).toLocaleString()}</td>
                <td className="px-4 py-2 text-right">
                  {t.status === "running" && (
                    <button onClick={() => handleCancel(t.id)} className="text-xs text-[var(--warning)] hover:underline">取消</button>
                  )}
                  {t.status === "failed" && (
                    <button onClick={() => handleRetry(t.id)} className="text-xs text-[var(--accent-primary)] hover:underline">重试</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
