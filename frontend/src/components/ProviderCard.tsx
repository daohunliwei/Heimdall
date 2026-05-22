"use client";

import { useState } from "react";

interface ModelInfo {
  modelName: string;
  billingType: string;
  maxContextTokens: number;
  maxOutputTokens: number;
  contextFillRatio: number;
  contextWarningThreshold: number;
  supportsCaching: boolean;
}

interface ProviderStatus {
  provider: string;
  displayName: string;
  hasApiKey: boolean;
  status: "configured" | "no_key" | "unconfigured";
  modelCount: number;
  models: ModelInfo[];
}

interface MetadataModel {
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

function StatusDot({ status }: { status: string }) {
  const colors: Record<string, string> = {
    configured: "#22c55e",
    no_key: "#f59e0b",
    unconfigured: "#9ca3af",
  };
  const labels: Record<string, string> = {
    configured: "已配置 · 密钥可用",
    no_key: "默认配置 · 缺少密钥",
    unconfigured: "未配置",
  };
  return (
    <span
      className="inline-block h-3 w-3 rounded-full"
      style={{ backgroundColor: colors[status] || colors.unconfigured }}
      title={labels[status] || "未知"}
    />
  );
}

function ModelRow({ model, provider }: { model: ModelInfo; provider: string }) {
  const fillPct = Math.round(model.contextFillRatio * 100);
  const ctxK = model.maxContextTokens >= 1000
    ? (model.maxContextTokens / 1000).toFixed(0) + "K"
    : model.maxContextTokens.toString();
  const outK = model.maxOutputTokens >= 1000
    ? (model.maxOutputTokens / 1000).toFixed(0) + "K"
    : model.maxOutputTokens.toString();

  return (
    <div className="flex items-center gap-3 py-2 px-3 rounded hover:bg-[var(--background)] text-sm border-b border-[var(--border-color)] last:border-b-0">
      <span className="font-medium text-[var(--foreground)] min-w-[140px]">
        {model.modelName}
      </span>
      <span className={`tag text-xs ${model.billingType === "CodingPlan" ? "tag-primary" : "tag-default"}`}>
        {model.billingType === "CodingPlan" ? "按次" : "按Token"}
      </span>
      <span className="text-[var(--muted)] text-xs min-w-[60px] text-right font-mono">
        上下文 {ctxK}
      </span>
      <span className="text-[var(--muted)] text-xs min-w-[60px] text-right font-mono">
        输出 {outK}
      </span>
      <div className="flex items-center gap-2 min-w-[80px]">
        <div className="h-1.5 w-12 rounded-full bg-[var(--border-color)]">
          <div
            className="h-1.5 rounded-full bg-[var(--accent-primary)]"
            style={{ width: `${fillPct}%` }}
          />
        </div>
        <span className="text-xs text-[var(--muted)] font-mono">{fillPct}%</span>
      </div>
      <span className="text-xs text-[var(--muted)] min-w-[32px] text-center">
        {model.supportsCaching ? "缓存" : "—"}
      </span>
    </div>
  );
}

const providerIcons: Record<string, string> = {
  openai: "◈",
  google: "◆",
  ollama: "◆",
  minimax: "◇",
  dashscope: "◇",
  deepseek: "◇",
  openrouter: "◎",
  azure: "▣",
  bedrock: "▣",
};

export default function ProviderCard({
  providerData,
  metadata,
  onEdit,
}: {
  providerData: ProviderStatus;
  metadata: MetadataModel[];
  onEdit: (provider: string, model: MetadataModel) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const icon = providerIcons[providerData.provider.toLowerCase()] || "○";

  return (
    <div className="card overflow-hidden">
      {/* 卡片头部 */}
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center gap-3 px-4 py-3 hover:bg-[var(--background)] transition-colors text-left"
      >
        <span className="text-xl">{icon}</span>
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-[var(--foreground)] capitalize">
              {providerData.displayName}
            </h3>
            <StatusDot status={providerData.status} />
          </div>
          <p className="text-xs text-[var(--muted)]">
            {providerData.modelCount} 个模型
          </p>
        </div>
        <span
          className={`text-[var(--muted)] transition-transform text-lg ${
            expanded ? "rotate-180" : ""
          }`}
        >
          ▾
        </span>
      </button>

      {/* 展开的模型列表 */}
      {expanded && (
        <div className="border-t border-[var(--border-color)]">
          {providerData.models.length === 0 ? (
            <p className="px-4 py-3 text-sm text-[var(--muted)]">
              暂无模型配置
            </p>
          ) : (
            <div>
              {providerData.models.map((m) => (
                <button
                  key={`${providerData.provider}-${m.modelName}`}
                  className="w-full text-left"
                  onClick={() => {
                    const meta = metadata.find(
                      (md) =>
                        md.modelName === m.modelName
                    );
                    if (meta) onEdit(providerData.provider, meta);
                  }}
                >
                  <ModelRow model={m} provider={providerData.provider} />
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
