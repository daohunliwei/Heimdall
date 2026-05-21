"use client";

import { useEffect, useState } from "react";

/** 提示词模板 DTO——匹配后端 camelCase 序列化 */
interface PromptTemplateDto {
  id: string;
  slug: string;
  name: string;
  category: string;
  templateContent: string;
  isSystem: boolean;
  isActive: boolean;
  version: number;
  createdAt: string;
  updatedAt: string;
}

const categoryLabels: Record<string, string> = {
  wiki_structure: "Wiki 结构",
  wiki_page: "Wiki 页面",
  ask: "问答",
  slides: "演示文稿",
  workshop: "训练营",
  system: "系统级",
};

export default function PromptsPage() {
  const [prompts, setPrompts] = useState<PromptTemplateDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({
    slug: "",
    name: "",
    category: "wiki_structure",
    contentTemplate: "",
  });

  async function loadPrompts() {
    setIsLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/admin/prompt-templates");
      if (res.ok) {
        setPrompts(await res.json());
      } else {
        setError("加载提示词模板失败");
      }
    } catch {
      setError("网络错误");
    } finally {
      setIsLoading(false);
    }
  }

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { loadPrompts(); }, []);

  async function handleCreate() {
    try {
      const res = await fetch("/api/admin/prompt-templates", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(form),
      });
      if (res.ok) {
        setShowForm(false);
        setForm({ slug: "", name: "", category: "wiki_structure", contentTemplate: "" });
        loadPrompts();
      }
    } catch { /* ignore */ }
  }

  async function handleDelete(id: string) {
    try {
      await fetch(`/api/admin/prompt-templates/${id}`, { method: "DELETE" });
      loadPrompts();
    } catch { /* ignore */ }
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-bold text-[var(--foreground)]">提示词管理</h2>
        <button onClick={() => setShowForm(true)} className="btn-primary text-sm">新建模板</button>
      </div>

      {error && (
        <div className="mb-4 p-3 rounded bg-[var(--highlight-light)] border border-[var(--highlight)]/20 text-sm text-[var(--foreground)]">
          {error}
        </div>
      )}

      {showForm && (
        <div className="mb-4 card p-4 space-y-2">
          <input
            placeholder="Slug（唯一标识，如 wiki-structure-planning）"
            value={form.slug}
            onChange={(e) => setForm({ ...form, slug: e.target.value })}
            className="input w-full text-sm"
          />
          <input
            placeholder="模板名称"
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            className="input w-full text-sm"
          />
          <select
            value={form.category}
            onChange={(e) => setForm({ ...form, category: e.target.value })}
            className="input w-full text-sm"
          >
            {Object.entries(categoryLabels).map(([key, label]) => (
              <option key={key} value={key}>{label}</option>
            ))}
          </select>
          <textarea
            placeholder="模板内容（支持 {{variable}} 变量插值）"
            value={form.contentTemplate}
            onChange={(e) => setForm({ ...form, contentTemplate: e.target.value })}
            rows={6}
            className="input w-full text-sm"
          />
          <div className="flex gap-2">
            <button onClick={handleCreate} className="btn-primary text-sm">保存</button>
            <button onClick={() => setShowForm(false)} className="btn-secondary text-sm">取消</button>
          </div>
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-[var(--muted)]">加载中...</p>
      ) : (
        <div className="space-y-3">
          {prompts.map((p) => (
            <div key={p.id} className="card p-4">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-medium text-[var(--foreground)]">{p.name}</span>
                  <code className="text-xs text-[var(--muted)]">{p.slug}</code>
                  <span className="tag tag-default">{categoryLabels[p.category] || p.category}</span>
                  {p.isSystem && <span className="tag tag-primary">系统内置</span>}
                  <span className="text-xs text-[var(--muted)]">v{p.version}</span>
                </div>
                <button
                  onClick={() => handleDelete(p.id)}
                  disabled={p.isSystem}
                  className={`text-xs ${p.isSystem ? 'text-[var(--muted)] cursor-not-allowed' : 'text-[var(--highlight)] hover:underline'}`}
                >
                  {p.isSystem ? '不可删除' : '删除'}
                </button>
              </div>
              <pre className="mt-2 max-h-40 overflow-y-auto whitespace-pre-wrap rounded bg-[var(--background)] p-2 text-xs text-[var(--muted)]">
                {(p.templateContent || '').slice(0, 500)}
              </pre>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
