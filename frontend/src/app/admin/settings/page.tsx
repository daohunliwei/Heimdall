"use client";

import { useEffect, useState } from "react";

interface Setting {
  id: string;
  key: string;
  value: string;
  description: string | null;
}

export default function SettingsPage() {
  const [settings, setSettings] = useState<Setting[]>([]);
  const [editing, setEditing] = useState<Record<string, string>>({});

  useEffect(() => {
    fetch("/api/admin/settings")
      .then((r) => r.json())
      .then((data) => {
        setSettings(data);
        const map: Record<string, string> = {};
        data.forEach((s: Setting) => { map[s.key] = s.value; });
        setEditing(map);
      })
      .catch(() => {});
  }, []);

  async function handleSave(key: string) {
    await fetch("/api/admin/settings", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify([{ key, value: editing[key] }]),
    });
  }

  return (
    <div>
      <h2 className="mb-4 text-xl font-bold text-[var(--foreground)]">全局设置</h2>
      <div className="space-y-3">
        {settings.map((s) => (
          <div key={s.id} className="card p-4">
            <label className="mb-1 block text-sm font-medium text-[var(--foreground)]">{s.key}</label>
            {s.description && <p className="mb-2 text-xs text-[var(--muted)]">{s.description}</p>}
            <div className="flex gap-2">
              <input
                value={editing[s.key] || ""}
                onChange={(e) => setEditing({ ...editing, [s.key]: e.target.value })}
                className="input flex-1"
              />
              <button onClick={() => handleSave(s.key)} className="btn-primary text-sm">保存</button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
