"use client";

import { useEffect, useState } from "react";

interface UserInfo {
  id: string;
  username: string;
  email: string | null;
  role: string;
}

export default function UsersPage() {
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ username: "", password: "", email: "", role: "Viewer" });

  const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
  const authHeader = { Authorization: `Bearer ${localStorage.getItem("heimdall_token")}` };

  async function loadUsers() {
    const res = await fetch(`${baseUrl}/admin/users`, { headers: authHeader });
    if (res.ok) setUsers(await res.json());
  }

  useEffect(() => { loadUsers(); }, []);

  async function handleCreate() {
    await fetch(`${baseUrl}/admin/users`, {
      method: "POST",
      headers: { ...authHeader, "Content-Type": "application/json" },
      body: JSON.stringify(form),
    });
    setShowForm(false);
    setForm({ username: "", password: "", email: "", role: "Viewer" });
    loadUsers();
  }

  async function handleDelete(id: string) {
    if (!confirm("确定要删除此用户吗？")) return;
    await fetch(`${baseUrl}/admin/users/${id}`, { method: "DELETE", headers: authHeader });
    loadUsers();
  }

  async function handleToggleActive(id: string) {
    await fetch(`${baseUrl}/admin/users/${id}/activate`, { method: "PUT", headers: authHeader });
    loadUsers();
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-gray-900 dark:text-white">用户管理</h2>
        <button onClick={() => setShowForm(true)} className="rounded bg-blue-600 px-3 py-1.5 text-sm text-white hover:bg-blue-700">新建用户</button>
      </div>

      {showForm && (
        <div className="mb-4 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
          <input placeholder="用户名" value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} className="mb-2 w-full rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white" />
          <input type="password" placeholder="密码" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} className="mb-2 w-full rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white" />
          <input placeholder="邮箱" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} className="mb-2 w-full rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white" />
          <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} className="mb-2 w-full rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white">
            <option value="Viewer">Viewer</option>
            <option value="Editor">Editor</option>
            <option value="Admin">Admin</option>
          </select>
          <div className="flex gap-2">
            <button onClick={handleCreate} className="rounded bg-green-600 px-3 py-1 text-sm text-white hover:bg-green-700">保存</button>
            <button onClick={() => setShowForm(false)} className="rounded bg-gray-300 px-3 py-1 text-sm dark:bg-gray-600 dark:text-white">取消</button>
          </div>
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th className="px-4 py-2 text-left">用户名</th>
              <th className="px-4 py-2 text-left">邮箱</th>
              <th className="px-4 py-2 text-left">角色</th>
              <th className="px-4 py-2 text-right">操作</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
            {users.map((u) => (
              <tr key={u.id} className="bg-white dark:bg-gray-800">
                <td className="px-4 py-2">{u.username}</td>
                <td className="px-4 py-2 text-gray-500">{u.email || "-"}</td>
                <td className="px-4 py-2">
                  <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs text-blue-700 dark:bg-blue-900/20 dark:text-blue-400">{u.role}</span>
                </td>
                <td className="px-4 py-2 text-right">
                  <button onClick={() => handleToggleActive(u.id)} className="mr-2 text-xs text-orange-600 hover:underline">切换状态</button>
                  <button onClick={() => handleDelete(u.id)} className="text-xs text-red-600 hover:underline">删除</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
