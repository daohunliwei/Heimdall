"use client";

import { useEffect, useState } from "react";

interface ModelMeta {
  modelName: string;
  billingType: string;
  maxContextTokens: number;
  maxOutputTokens: number;
  rateLimitPerMinute: number | null;
  inputTokenPrice: number | null;
  outputTokenPrice: number | null;
  callPrice: number | null;
  supportsCaching: boolean;
  contextFillRatio: number;
  contextWarningThreshold: number;
  updatedAt: string;
}

interface ProviderGroup {
  [provider: string]: ModelMeta[];
}

interface SystemInfo {
  defaultProvider: string;
  embedderType: string;
  contextFillRatio: number;
  providers: string[];
  pipeline_10_stage: boolean;
  auth_mode: string;
  registration_open: boolean;
}

const tabs = ["Provider 配置", "系统参数"] as const;
type Tab = (typeof tabs)[number];

export default function SettingsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("Provider 配置");
  const [metadata, setMetadata] = useState<ProviderGroup>({});
  const [sysInfo, setSysInfo] = useState<SystemInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<{
    provider: string; model: string; data: Partial<ModelMeta>;
  } | null>(null);

  const fetchMetadata = () => {
    setLoading(true);
    fetch("/api/admin/provider-metadata").then(r => r.json()).then(d => { setMetadata(d); setLoading(false); }).catch(() => setLoading(false));
    fetch("/api/admin/system-info").then(r => r.json()).then(d => setSysInfo(d)).catch(() => {});
  };
  useEffect(() => { fetchMetadata(); }, []);

  function openEdit(provider: string, model: ModelMeta) {
    setEditing({ provider, model: model.modelName, data: { ...model } });
  }

  async function saveEdit() {
    if (!editing) return;
    await fetch(`/api/admin/provider-metadata/${editing.provider}/${editing.model}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(editing.data),
    });
    setEditing(null);
    fetchMetadata();
  }

  async function deleteMeta(provider: string, model: string) {
    if (!confirm(`确定删除 ${provider}/${model} 的自定义元数据？将回退到默认值。`)) return;
    await fetch(`/api/admin/provider-metadata/${provider}/${model}`, { method: "DELETE" });
    fetchMetadata();
  }

  function updateEditField(field: string, value: string | number | boolean | null) {
    if (!editing) return;
    setEditing({ ...editing, data: { ...editing.data, [field]: value } });
  }

  return (
    <div>
      <h2 className="mb-4 text-xl font-bold text-[var(--foreground)]">全局设置</h2>

      {/* Tab 切换 */}
      <div className="mb-4 flex gap-1 border-b border-[var(--border-color)]">
        {tabs.map((t) => (
          <button
            key={t}
            onClick={() => setActiveTab(t)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === t
                ? "border-[var(--accent-primary)] text-[var(--accent-primary)]"
                : "border-transparent text-[var(--muted)] hover:text-[var(--foreground)]"
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      {/* Provider 配置 Tab */}
      {activeTab === "Provider 配置" && (
        <div>
          {loading ? (
            <p className="text-[var(--muted)]">加载中...</p>
          ) : Object.keys(metadata).length === 0 ? (
            <p className="text-[var(--muted)]">暂无 Provider 配置。数据将从 generator.json 默认值自动加载。</p>
          ) : (
            <div className="space-y-6">
              {Object.entries(metadata).map(([provider, models]) => (
                <div key={provider} className="card p-4">
                  <h3 className="mb-3 text-lg font-semibold text-[var(--foreground)] capitalize">{provider}</h3>
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead className="bg-[var(--background)]">
                        <tr>
                          <th className="px-3 py-2 text-left">模型</th>
                          <th className="px-3 py-2 text-left">计费</th>
                          <th className="px-3 py-2 text-right">上下文窗口</th>
                          <th className="px-3 py-2 text-right">最大输出</th>
                          <th className="px-3 py-2 text-right">填充比例</th>
                          <th className="px-3 py-2 text-right">警戒阈值</th>
                          <th className="px-3 py-2 text-center">缓存</th>
                          <th className="px-3 py-2 text-right">操作</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[var(--border-color)]">
                        {(models as ModelMeta[]).map((m) => (
                          <tr key={`${provider}-${m.modelName}`} className="hover:bg-[var(--background)]">
                            <td className="px-3 py-2 font-medium">{m.modelName}</td>
                            <td className="px-3 py-2">
                              <span className={`tag text-xs ${m.billingType === "CodingPlan" ? "tag-primary" : "tag-default"}`}>
                                {m.billingType === "CodingPlan" ? "按次" : "按Token"}
                              </span>
                            </td>
                            <td className="px-3 py-2 text-right font-mono">{(m.maxContextTokens / 1000).toFixed(0)}K</td>
                            <td className="px-3 py-2 text-right font-mono">{(m.maxOutputTokens / 1000).toFixed(0)}K</td>
                            <td className="px-3 py-2 text-right font-mono">{(m.contextFillRatio * 100).toFixed(0)}%</td>
                            <td className="px-3 py-2 text-right font-mono">{(m.contextWarningThreshold * 100).toFixed(0)}%</td>
                            <td className="px-3 py-2 text-center">{m.supportsCaching ? "✓" : "—"}</td>
                            <td className="px-3 py-2 text-right space-x-1">
                              <button onClick={() => openEdit(provider, m)} className="btn-primary text-xs px-2 py-0.5">编辑</button>
                              <button onClick={() => deleteMeta(provider, m.modelName)} className="text-xs text-[var(--warning)] hover:underline">删除</button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 系统参数 Tab */}
      {activeTab === "系统参数" && (
        <div className="space-y-4">
          <div className="card p-4">
            <h3 className="mb-2 text-sm font-semibold text-[var(--foreground)]">运行时配置</h3>
            {sysInfo ? (
              <table className="w-full text-sm">
                <thead>
                  <tr>
                    <th className="px-3 py-2 text-left w-1/3">配置项</th>
                    <th className="px-3 py-2 text-left">当前值</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-color)]">
                  {[
                    ["默认 Provider", sysInfo.defaultProvider],
                    ["嵌入器类型", sysInfo.embedderType],
                    ["上下文填充比例", `${(sysInfo.contextFillRatio * 100).toFixed(0)}%`],
                    ["管线版本", sysInfo.pipeline_10_stage ? "10 阶段（最新）" : "未知"],
                    ["认证模式", sysInfo.auth_mode],
                    ["开放注册", sysInfo.registration_open ? "是" : "否"],
                    ["已注册 Provider", sysInfo.providers.join(", ")],
                  ].map(([label, value]) => (
                    <tr key={label}>
                      <td className="px-3 py-1.5 text-[var(--muted)]">{label}</td>
                      <td className="px-3 py-1.5 font-mono text-xs">{value}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="text-sm text-[var(--muted)]">加载中...</p>
            )}
          </div>
          <div className="card p-4">
            <h3 className="mb-2 text-sm font-semibold text-[var(--foreground)]">模型元数据来源</h3>
            <p className="text-sm text-[var(--muted)]">
              默认值从 <code className="font-mono">config/generator.json</code> 加载。
              在「Provider 配置」Tab 中编辑并保存后，自定义值将覆盖 JSON 默认值并持久化到数据库。
              删除自定义值后回退到 JSON 默认值。
            </p>
          </div>
        </div>
      )}

      {/* 编辑弹窗 */}
      {editing && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={() => setEditing(null)}>
          <div className="card p-6 w-full max-w-lg max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <h3 className="mb-4 text-lg font-bold text-[var(--foreground)]">
              编辑 {editing.provider} / {editing.model}
            </h3>
            <div className="space-y-3">
              <label className="block text-sm">
                计费类型
                <select
                  value={editing.data.billingType || "TokenPlan"}
                  onChange={(e) => updateEditField("billingType", e.target.value)}
                  className="input mt-1 w-full"
                >
                  <option value="TokenPlan">TokenPlan（按 Token 量收费）</option>
                  <option value="CodingPlan">CodingPlan（按调用次数收费）</option>
                </select>
              </label>
              <label className="block text-sm">
                上下文窗口 (Tokens)
                <input type="number" value={editing.data.maxContextTokens || 128000}
                  onChange={(e) => updateEditField("maxContextTokens", parseInt(e.target.value) || 0)}
                  className="input mt-1 w-full" />
              </label>
              <label className="block text-sm">
                最大输出 (Tokens)
                <input type="number" value={editing.data.maxOutputTokens || 8192}
                  onChange={(e) => updateEditField("maxOutputTokens", parseInt(e.target.value) || 0)}
                  className="input mt-1 w-full" />
              </label>
              <label className="block text-sm">
                上下文填充比例 (0-1)
                <input type="number" step="0.05" min="0.1" max="1" value={editing.data.contextFillRatio || 0.65}
                  onChange={(e) => updateEditField("contextFillRatio", parseFloat(e.target.value) || 0.65)}
                  className="input mt-1 w-full" />
              </label>
              <label className="block text-sm">
                警戒阈值 (0-1)
                <input type="number" step="0.05" min="0.5" max="1" value={editing.data.contextWarningThreshold || 0.90}
                  onChange={(e) => updateEditField("contextWarningThreshold", parseFloat(e.target.value) || 0.90)}
                  className="input mt-1 w-full" />
              </label>
              {editing.data.billingType === "TokenPlan" ? (
                <>
                  <label className="block text-sm">输入价格 ($/百万Token)
                    <input type="number" step="0.01" value={editing.data.inputTokenPrice ?? ""}
                      onChange={(e) => updateEditField("inputTokenPrice", parseFloat(e.target.value) || null)}
                      className="input mt-1 w-full" />
                  </label>
                  <label className="block text-sm">输出价格 ($/百万Token)
                    <input type="number" step="0.01" value={editing.data.outputTokenPrice ?? ""}
                      onChange={(e) => updateEditField("outputTokenPrice", parseFloat(e.target.value) || null)}
                      className="input mt-1 w-full" />
                  </label>
                </>
              ) : (
                <label className="block text-sm">单次调用价格 ($)
                  <input type="number" step="0.001" value={editing.data.callPrice ?? ""}
                    onChange={(e) => updateEditField("callPrice", parseFloat(e.target.value) || null)}
                    className="input mt-1 w-full" />
                </label>
              )}
              <label className="block text-sm">速率限制 (次/分钟)
                <input type="number" value={editing.data.rateLimitPerMinute ?? ""}
                  onChange={(e) => updateEditField("rateLimitPerMinute", parseInt(e.target.value) || null)}
                  className="input mt-1 w-full" />
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={editing.data.supportsCaching || false}
                  onChange={(e) => updateEditField("supportsCaching", e.target.checked)} />
                支持 Prompt 缓存
              </label>
            </div>
            <div className="mt-4 flex justify-end gap-2">
              <button onClick={() => setEditing(null)} className="btn-secondary text-sm">取消</button>
              <button onClick={saveEdit} className="btn-primary text-sm">保存</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
