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

  async function loadPrompts() {
    const res = await fetch("/api/admin/prompts");
    if (res.ok) setPrompts(await res.json());
  }

  useEffect(() => { loadPrompts(); }, []);

  async function handleCreate() {
    await fetch("/api/admin/prompts", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(form),
    });
    setShowForm(false);
    loadPrompts();
  }

  async function handleDelete(id: string) {
    await fetch(`/api/admin/prompts/${id}`, { method: "DELETE" });
    loadPrompts();
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-[var(--foreground)]">提示词管理</h2>
        <button onClick={() => setShowForm(true)} className="btn-primary text-sm">新建模板</button>
      </div>

      {showForm && (
        <div className="mb-4 card p-4">
          <input placeholder="模板名称" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="input mb-2" />
          <select value={form.layer} onChange={(e) => setForm({ ...form, layer: e.target.value })} className="input mb-2">
            <option value="system">系统级 (System)</option>
            <option value="workflow">工作流级 (Workflow)</option>
            <option value="task">任务级 (Task)</option>
          </select>
          <textarea placeholder="模板内容" value={form.template_content} onChange={(e) => setForm({ ...form, template_content: e.target.value })} rows={4} className="input mb-2" />
          <div className="flex gap-2">
            <button onClick={handleCreate} className="btn-primary text-sm">保存</button>
            <button onClick={() => setShowForm(false)} className="btn-secondary text-sm">取消</button>
          </div>
        </div>
      )}

      <div className="space-y-3">
        {prompts.map((p) => (
          <div key={p.id} className="card p-4">
            <div className="flex items-center justify-between">
              <div>
                <span className="font-medium text-[var(--foreground)]">{p.name}</span>
                <span className="ml-2 tag tag-default">{layerLabels[p.layer] || p.layer}</span>
                <span className="ml-1 tag tag-primary">{p.scope_type}</span>
              </div>
              <button onClick={() => handleDelete(p.id)} className="text-xs text-[var(--highlight)] hover:underline">删除</button>
            </div>
            <pre className="mt-2 max-h-40 overflow-y-auto whitespace-pre-wrap rounded bg-[var(--background)] p-2 text-xs text-[var(--muted)]">{p.template_content.slice(0, 500)}</pre>
          </div>
        ))}
      </div>
    </div>
  );
}
