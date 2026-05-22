"use client";

import { useCallback, useEffect, useState } from "react";
import ProviderCard from "@/components/ProviderCard";
import ConfigStatusPanel from "@/components/ConfigStatusPanel";

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

interface ModelInfo {
  modelName: string;
  billingType: string;
  maxContextTokens: number;
  maxOutputTokens: number;
  contextFillRatio: number;
  contextWarningThreshold: number;
  supportsCaching: boolean;
}

interface ProviderStatusItem {
  provider: string;
  displayName: string;
  hasApiKey: boolean;
  status: "configured" | "no_key" | "unconfigured";
  modelCount: number;
  models: ModelInfo[];
}

interface SystemConfig {
  serviceConfig: Record<string, { value: string; source: string }>;
  resourceConfig: Record<string, { value: string; source: string }>;
  providerKeyStatus: {
    provider: string;
    envVar: string;
    isSet: boolean;
    maskedValue: string;
  }[];
}

interface DebugConfig {
  enabled: boolean;
  maxDebugPages: number;
}

const tabs = ["Provider 管理", "系统配置", "调试设置"] as const;
type Tab = (typeof tabs)[number];

export default function SettingsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("Provider 管理");
  const [metadata, setMetadata] = useState<ProviderGroup>({});
  const [providerStatus, setProviderStatus] = useState<ProviderStatusItem[]>([]);
  const [sysConfig, setSysConfig] = useState<SystemConfig | null>(null);
  const [debugConfig, setDebugConfig] = useState<DebugConfig>({ enabled: false, maxDebugPages: 5 });
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<{
    provider: string; model: string; data: Partial<ModelMeta>;
  } | null>(null);

  const fetchAll = useCallback(() => {
    setLoading(true);
    // 加载 Provider 元数据
    fetch("/api/admin/provider-metadata")
      .then((r) => r.json())
      .then((d) => { setMetadata(d); })
      .catch(() => {});
    // 加载 Provider 连接状态
    fetch("/api/admin/provider-status")
      .then((r) => r.json())
      .then((d) => { setProviderStatus(Array.isArray(d) ? d : []); })
      .catch(() => {});
    // 加载系统配置
    fetch("/api/admin/system-config")
      .then((r) => r.json())
      .then((d) => { setSysConfig(d); })
      .catch(() => {});
    // 加载调试配置
    fetch("/api/admin/debug-config")
      .then((r) => r.json())
      .then((d) => { setDebugConfig(d); })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { fetchAll(); }, [fetchAll]);

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
    fetchAll();
  }

  async function deleteMeta(provider: string, model: string) {
    if (!confirm(`确定删除 ${provider}/${model} 的自定义元数据？将回退到默认值。`)) return;
    await fetch(`/api/admin/provider-metadata/${provider}/${model}`, { method: "DELETE" });
    fetchAll();
  }

  function updateEditField(field: string, value: string | number | boolean | null) {
    if (!editing) return;
    setEditing({ ...editing, data: { ...editing.data, [field]: value } });
  }

  async function saveDebugConfig() {
    await fetch("/api/admin/debug-config", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(debugConfig),
    });
    fetchAll();
  }

  // ── Provider 编辑弹窗（复用原有逻辑） ──
  const renderEditModal = () => {
    if (!editing) return null;
    return (
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
    );
  };

  // ── 渲染 ──
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

      {/* Provider 管理 Tab */}
      {activeTab === "Provider 管理" && (
        <div>
          {loading ? (
            <p className="text-[var(--muted)]">加载中...</p>
          ) : providerStatus.length === 0 ? (
            <p className="text-[var(--muted)]">
              暂无 Provider 配置。数据将从 generator.json 默认值自动加载。
            </p>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {providerStatus.map((ps) => {
                // 合并元数据用于编辑
                const providerModels = metadata[ps.provider] || [];
                return (
                  <ProviderCard
                    key={ps.provider}
                    providerData={ps}
                    metadata={providerModels}
                    onEdit={openEdit}
                  />
                );
              })}
            </div>
          )}
          <p className="mt-4 text-xs text-[var(--muted)]">
            点击展开 Provider 卡片查看模型详情，点击模型行可编辑元数据。连接状态指示灯：绿=密钥已配置，黄=有默认配置无密钥，灰=未配置。
          </p>
        </div>
      )}

      {/* 系统配置 Tab */}
      {activeTab === "系统配置" && (
        <ConfigStatusPanel config={sysConfig} />
      )}

      {/* 调试设置 Tab */}
      {activeTab === "调试设置" && (
        <div className="space-y-4">
          <div className="card p-4">
            <h3 className="mb-3 text-sm font-semibold text-[var(--foreground)]">调试模式</h3>
            <label className="flex items-center gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={debugConfig.enabled}
                onChange={(e) =>
                  setDebugConfig({ ...debugConfig, enabled: e.target.checked })
                }
                className="h-4 w-4"
              />
              <div>
                <span className="text-sm font-medium text-[var(--foreground)]">
                  开启调试模式
                </span>
                <p className="text-xs text-[var(--muted)]">
                  开启后 Wiki 生成将限制最大页数，加快调试迭代
                </p>
              </div>
            </label>
          </div>

          <div className="card p-4">
            <h3 className="mb-3 text-sm font-semibold text-[var(--foreground)]">最大调试页数</h3>
            <label className="flex items-center gap-3">
              <input
                type="number"
                min={1}
                max={20}
                value={debugConfig.maxDebugPages}
                onChange={(e) => {
                  const v = parseInt(e.target.value) || 5;
                  setDebugConfig({
                    ...debugConfig,
                    maxDebugPages: Math.min(20, Math.max(1, v)),
                  });
                }}
                className="input w-20"
                disabled={!debugConfig.enabled}
              />
              <span className="text-sm text-[var(--muted)]">页（范围 1-20）</span>
            </label>
            <p className="mt-2 text-xs text-[var(--muted)]">
              当前状态：{debugConfig.enabled
                ? `调试模式开启 · 最多生成 ${debugConfig.maxDebugPages} 页`
                : "调试模式关闭 · 正常全量生成"}
            </p>
          </div>

          <div className="flex justify-end">
            <button onClick={saveDebugConfig} className="btn-primary text-sm">
              保存调试设置
            </button>
          </div>

          <div className="card p-4 bg-[var(--accent-secondary)]">
            <h3 className="mb-2 text-sm font-semibold text-[var(--foreground)]">说明</h3>
            <ul className="text-xs text-[var(--muted)] space-y-1">
              <li>• 调试模式配置实时生效，不影响已在执行中的任务</li>
              <li>• 截断后生成的 Wiki 版本会标记 debug_truncated 字段</li>
              <li>• 建议调试时设置为 3-5 页，验证通过后关闭以生成完整 Wiki</li>
            </ul>
          </div>
        </div>
      )}

      {renderEditModal()}
    </div>
  );
}
