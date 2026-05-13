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

  const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
  const authHeader = { Authorization: `Bearer ${localStorage.getItem("heimdall_token")}` };

  async function loadRepos() {
    const res = await fetch(`${baseUrl}/admin/repositories`, { headers: authHeader });
    if (res.ok) setRepos(await res.json());
  }

  useEffect(() => { loadRepos(); }, []);

  async function handleDelete(id: string) {
    if (!confirm("确定删除此仓库及其缓存吗？")) return;
    await fetch(`${baseUrl}/admin/repositories/${id}`, { method: "DELETE", headers: authHeader });
    loadRepos();
  }

  async function handleRegenerate(id: string) {
    await fetch(`${baseUrl}/admin/repositories/${id}/regenerate`, { method: "POST", headers: authHeader });
  }

  return (
    <div>
      <h2 className="mb-4 text-xl font-bold text-gray-900 dark:text-white">仓库管理</h2>
      <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th className="px-4 py-2 text-left">仓库</th>
              <th className="px-4 py-2 text-left">类型</th>
              <th className="px-4 py-2 text-left">分支</th>
              <th className="px-4 py-2 text-left">创建时间</th>
              <th className="px-4 py-2 text-right">操作</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
            {repos.map((r) => (
              <tr key={r.id} className="bg-white dark:bg-gray-800">
                <td className="px-4 py-2">{r.owner}/{r.repo_name}</td>
                <td className="px-4 py-2">
                  <span className="rounded bg-gray-100 px-1.5 py-0.5 text-xs dark:bg-gray-700">{r.repo_type}</span>
                </td>
                <td className="px-4 py-2">{r.default_branch}</td>
                <td className="px-4 py-2 text-xs text-gray-500">{new Date(r.created_at).toLocaleString()}</td>
                <td className="px-4 py-2 text-right">
                  <button onClick={() => handleRegenerate(r.id)} className="mr-2 text-xs text-blue-600 hover:underline">重新生成</button>
                  <button onClick={() => handleDelete(r.id)} className="text-xs text-red-600 hover:underline">删除</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
