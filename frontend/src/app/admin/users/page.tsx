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

  async function loadUsers() {
    const res = await fetch("/api/admin/users");
    if (res.ok) setUsers(await res.json());
  }

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { loadUsers(); }, []);

  async function handleCreate() {
    await fetch("/api/admin/users", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(form),
    });
    setShowForm(false);
    setForm({ username: "", password: "", email: "", role: "Viewer" });
    loadUsers();
  }

  async function handleDelete(id: string) {
    if (!confirm("确定要删除此用户吗？")) return;
    await fetch(`/api/admin/users/${id}`, { method: "DELETE" });
    loadUsers();
  }

  async function handleToggleActive(id: string) {
    await fetch(`/api/admin/users/${id}/activate`, { method: "PUT" });
    loadUsers();
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-[var(--foreground)]">用户管理</h2>
        <button onClick={() => setShowForm(true)} className="btn-primary text-sm">新建用户</button>
      </div>

      {showForm && (
        <div className="mb-4 card p-4">
          <input placeholder="用户名" value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} className="input mb-2" />
          <input type="password" placeholder="密码" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} className="input mb-2" />
          <input placeholder="邮箱" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} className="input mb-2" />
          <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} className="input mb-2">
            <option value="Viewer">Viewer</option>
            <option value="Editor">Editor</option>
            <option value="Admin">Admin</option>
          </select>
          <div className="flex gap-2">
            <button onClick={handleCreate} className="btn-primary text-sm">保存</button>
            <button onClick={() => setShowForm(false)} className="btn-secondary text-sm">取消</button>
          </div>
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-[var(--border-color)]">
        <table className="w-full text-sm">
          <thead className="bg-[var(--background)]">
            <tr>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">用户名</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">邮箱</th>
              <th className="px-4 py-2 text-left text-[var(--foreground)]">角色</th>
              <th className="px-4 py-2 text-right text-[var(--foreground)]">操作</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border-color)]">
            {users.map((u) => (
              <tr key={u.id} className="bg-[var(--card-bg)]">
                <td className="px-4 py-2 text-[var(--foreground)]">{u.username}</td>
                <td className="px-4 py-2 text-[var(--muted)]">{u.email || "-"}</td>
                <td className="px-4 py-2">
                  <span className="tag tag-primary">{u.role}</span>
                </td>
                <td className="px-4 py-2 text-right">
                  <button onClick={() => handleToggleActive(u.id)} className="mr-2 text-xs text-[var(--warning)] hover:underline">切换状态</button>
                  <button onClick={() => handleDelete(u.id)} className="text-xs text-[var(--highlight)] hover:underline">删除</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
