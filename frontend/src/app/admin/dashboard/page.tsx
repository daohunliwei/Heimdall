"use client";

import { useEffect, useState } from "react";

interface DashboardData {
  total_tasks: number;
  completed_tasks: number;
  failed_tasks: number;
  active_users: number;
  total_repositories: number;
  total_wikis: number;
  success_rate: number;
  total_tokens_used: number;
}

export default function DashboardPage() {
  const [data, setData] = useState<DashboardData | null>(null);

  useEffect(() => {
    fetch("/api/admin/dashboard")
      .then((r) => r.json())
      .then(setData)
      .catch(() => {});
  }, []);

  if (!data) {
    return <div className="text-sm text-[var(--muted)]">加载中...</div>;
  }

  const cards = [
    { label: "任务总数", value: data.total_tasks },
    { label: "已完成", value: data.completed_tasks },
    { label: "失败任务", value: data.failed_tasks },
    { label: "成功率", value: `${data.success_rate.toFixed(1)}%` },
    { label: "活跃用户", value: data.active_users },
    { label: "仓库数量", value: data.total_repositories },
    { label: "Wiki 数量", value: data.total_wikis },
    { label: "Token 用量", value: data.total_tokens_used.toLocaleString() },
  ];

  return (
    <div>
      <h2 className="mb-4 text-xl font-bold text-[var(--foreground)]">仪表盘</h2>
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {cards.map((card) => (
          <div
            key={card.label}
            className="card p-4"
          >
            <div className="text-xs text-[var(--muted)]">{card.label}</div>
            <div className="mt-1 text-2xl font-semibold text-[var(--foreground)]">
              {card.value}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
