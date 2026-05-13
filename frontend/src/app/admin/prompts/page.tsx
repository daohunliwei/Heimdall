"use client";

import { useEffect, useState } from "react";

interface PromptTemplate {
  id: string;
  name: string;
  layer: string;
  scope_type: string;
  scope_value: string | null;
  template_content: string;
  is_active: boolean;
}

const layerLabels: Record<string, string> = {
  system: "系统级",
  workflow: "工作流级",
  task: "任务级",
};

export default function PromptsPage() {
  const [prompts, setPrompts] = useState<PromptTemplate[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({
    name: "", layer: "system", scope_type: "global", scope_value: "", template_content: "",
  });

  const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
  const authHeader = { Authorization: `Bearer ${localStorage.getItem("heimdall_token")}` };

  async function loadPrompts() {
    const res = await fetch(`${baseUrl}/admin/prompts`, { headers: authHeader });
    if (res.ok) setPrompts(await res.json());
  }

  useEffect(() => { loadPrompts(); }, []);

  async function handleCreate() {
    await fetch(`${baseUrl}/admin/prompts`, {
      method: "POST",
      headers: { ...authHeader, "Content-Type": "application/json" },
      body: JSON.stringify(form),
    });
    setShowForm(false);
    loadPrompts();
  }

  async function handleDelete(id: string) {
    await fetch(`${baseUrl}/admin/prompts/${id}`, { method: "DELETE", headers: authHeader });
    loadPrompts();
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-gray-900 dark:text-white">提示词管理</h2>
        <button onClick={() => setShowForm(true)} className="rounded bg-blue-600 px-3 py-1.5 text-sm text-white hover:bg-blue-700">新建模板</button>
      </div>

      {showForm && (
        <div className="mb-4 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
          <input placeholder="模板名称" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="mb-2 w-full rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white" />
          <select value={form.layer} onChange={(e) => setForm({ ...form, layer: e.target.value })} className="mb-2 w-full rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white">
            <option value="system">系统级 (System)</option>
            <option value="workflow">工作流级 (Workflow)</option>
            <option value="task">任务级 (Task)</option>
          </select>
          <textarea placeholder="模板内容" value={form.template_content} onChange={(e) => setForm({ ...form, template_content: e.target.value })} rows={4} className="mb-2 w-full rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white" />
          <div className="flex gap-2">
            <button onClick={handleCreate} className="rounded bg-green-600 px-3 py-1 text-sm text-white hover:bg-green-700">保存</button>
            <button onClick={() => setShowForm(false)} className="rounded bg-gray-300 px-3 py-1 text-sm dark:bg-gray-600 dark:text-white">取消</button>
          </div>
        </div>
      )}

      <div className="space-y-3">
        {prompts.map((p) => (
          <div key={p.id} className="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
            <div className="flex items-center justify-between">
              <div>
                <span className="font-medium text-gray-900 dark:text-white">{p.name}</span>
                <span className="ml-2 rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-300">{layerLabels[p.layer] || p.layer}</span>
                <span className="ml-1 rounded bg-blue-100 px-1.5 py-0.5 text-xs text-blue-600 dark:bg-blue-900/20 dark:text-blue-400">{p.scope_type}</span>
              </div>
              <button onClick={() => handleDelete(p.id)} className="text-xs text-red-600 hover:underline">删除</button>
            </div>
            <pre className="mt-2 max-h-40 overflow-y-auto whitespace-pre-wrap rounded bg-gray-50 p-2 text-xs text-gray-600 dark:bg-gray-900 dark:text-gray-400">{p.template_content.slice(0, 500)}</pre>
          </div>
        ))}
      </div>
    </div>
  );
}
