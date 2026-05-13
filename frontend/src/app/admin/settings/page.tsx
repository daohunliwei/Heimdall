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

  const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
  const authHeader = { Authorization: `Bearer ${localStorage.getItem("heimdall_token")}` };

  useEffect(() => {
    fetch(`${baseUrl}/admin/settings`, { headers: authHeader })
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
    await fetch(`${baseUrl}/admin/settings`, {
      method: "PUT",
      headers: { ...authHeader, "Content-Type": "application/json" },
      body: JSON.stringify([{ key, value: editing[key] }]),
    });
  }

  return (
    <div>
      <h2 className="mb-4 text-xl font-bold text-gray-900 dark:text-white">全局设置</h2>
      <div className="space-y-3">
        {settings.map((s) => (
          <div key={s.id} className="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">{s.key}</label>
            {s.description && <p className="mb-2 text-xs text-gray-500">{s.description}</p>}
            <div className="flex gap-2">
              <input
                value={editing[s.key] || ""}
                onChange={(e) => setEditing({ ...editing, [s.key]: e.target.value })}
                className="flex-1 rounded border px-2 py-1 text-sm dark:bg-gray-700 dark:text-white"
              />
              <button onClick={() => handleSave(s.key)} className="rounded bg-blue-600 px-3 py-1 text-sm text-white hover:bg-blue-700">保存</button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
