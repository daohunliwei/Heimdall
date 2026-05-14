"use client";

import { useEffect, useState } from "react";

interface RepoInfo {
  id: string;
  owner: string;
  repo_name: string;
  repo_type: string;
  repo_url: string | null;
  default_branch: string;
  created_at: string;
}

export default function RepositoriesPage() {
  const [repos, setRepos] = useState<RepoInfo[]>([]);

  async function loadRepos() {
    const res = await fetch("/api/admin/repositories");
    if (res.ok) setRepos(await res.json());
  }

  useEffect(() => { loadRepos(); }, []);

  async function handleDelete(id: string) {
    if (!confirm("确定删除此仓库及其缓存吗？")) return;
    await fetch(`/api/admin/repositories/${id}`, { method: "DELETE" });
    loadRepos();
  }

  async function handleRegenerate(id: string) {
    await fetch(`/api/admin/repositories/${id}/regenerate`, { method: "POST" });
  }

  return (
    <div>
      <h2 className="mb-4 text-xl font-bold text-[var(--foreground)]">仓库管理</h2>
      <div className="overflow-x-auto rounded-lg border border-[var(--border-color)]">
        <table className="w-full text-sm">
          <thead className="bg-[var(--background)]">
            <tr>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">仓库</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">类型</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">分支</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">创建时间</th>
              <th className="px-4 py-2 text-right text-[var(--foreground)]">操作</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border-color)]">
            {repos.map((r) => (
              <tr key={r.id} className="bg-[var(--card-bg)]">
                <td className="px-4 py-2 text-[var(--foreground)]">{r.owner}/{r.repo_name}</td>
                <td className="px-4 py-2">
                  <span className="tag tag-default">{r.repo_type}</span>
                </td>
                <td className="px-4 py-2 text-[var(--foreground)]">{r.default_branch}</td>
                <td className="px-4 py-2 text-xs text-[var(--muted)]">{new Date(r.created_at).toLocaleString()}</td>
                <td className="px-4 py-2 text-right">
                  <button onClick={() => handleRegenerate(r.id)} className="mr-2 text-xs text-[var(--accent-primary)] hover:underline">重新生成</button>
                  <button onClick={() => handleDelete(r.id)} className="text-xs text-[var(--highlight)] hover:underline">删除</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
