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

const statusColors: Record<string, string> = {
  pending: "bg-yellow-100 text-yellow-700",
  running: "bg-blue-100 text-blue-700",
  completed: "bg-green-100 text-green-700",
  failed: "bg-red-100 text-red-700",
  cancelled: "bg-gray-100 text-gray-700",
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

  const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
  const authHeader = { Authorization: `Bearer ${localStorage.getItem("heimdall_token")}` };

  useEffect(() => {
    const params = new URLSearchParams({ limit: "100" });
    if (filter) params.set("status", filter);
    fetch(`${baseUrl}/admin/tasks?${params}`, { headers: authHeader })
      .then((r) => r.json())
      .then((d) => setTasks(d.tasks || []))
      .catch(() => {});
  }, [filter]);

  async function handleCancel(id: string) {
    await fetch(`${baseUrl}/admin/tasks/${id}/cancel`, { method: "POST", headers: authHeader });
    window.location.reload();
  }

  async function handleRetry(id: string) {
    await fetch(`${baseUrl}/admin/tasks/${id}/retry`, { method: "POST", headers: authHeader });
    window.location.reload();
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-gray-900 dark:text-white">任务监控</h2>
        <select
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white"
        >
          <option value="">全部</option>
          <option value="pending">等待中</option>
          <option value="running">运行中</option>
          <option value="completed">已完成</option>
          <option value="failed">失败</option>
          <option value="cancelled">已取消</option>
        </select>
      </div>

      <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th className="px-4 py-2 text-left">ID</th>
              <th className="px-4 py-2 text-left">类型</th>
              <th className="px-4 py-2 text-left">状态</th>
              <th className="px-4 py-2 text-left">进度</th>
              <th className="px-4 py-2 text-left">Token</th>
              <th className="px-4 py-2 text-left">创建时间</th>
              <th className="px-4 py-2 text-right">操作</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
            {tasks.map((t) => (
              <tr key={t.id} className="bg-white dark:bg-gray-800">
                <td className="px-4 py-2 font-mono text-xs">{t.id.slice(0, 8)}</td>
                <td className="px-4 py-2">{typeLabels[t.task_type] || t.task_type}</td>
                <td className="px-4 py-2">
                  <span className={`rounded-full px-2 py-0.5 text-xs ${statusColors[t.status] || ""}`}>
                    {t.status}
                  </span>
                </td>
                <td className="px-4 py-2">{t.progress_percent}%</td>
                <td className="px-4 py-2">{t.total_prompt_tokens + t.total_completion_tokens}</td>
                <td className="px-4 py-2 text-xs text-gray-500">{new Date(t.created_at).toLocaleString()}</td>
                <td className="px-4 py-2 text-right">
                  {t.status === "running" && (
                    <button onClick={() => handleCancel(t.id)} className="text-xs text-orange-600 hover:underline">取消</button>
                  )}
                  {t.status === "failed" && (
                    <button onClick={() => handleRetry(t.id)} className="text-xs text-blue-600 hover:underline">重试</button>
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
