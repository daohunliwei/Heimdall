"use client";

import { useState } from "react";

interface ConfigItem {
  value: string;
  source: string;
}

interface ConfigSection {
  [key: string]: ConfigItem;
}

interface SystemConfig {
  serviceConfig: ConfigSection;
  resourceConfig: ConfigSection;
  providerKeyStatus: {
    provider: string;
    envVar: string;
    isSet: boolean;
    maskedValue: string;
  }[];
}

const sourceLabels: Record<string, string> = {
  env: "ENV",
  default: "DEFAULT",
  file: "FILE",
};

function AccordionPanel({
  title,
  children,
  defaultOpen = false,
}: {
  title: string;
  children: React.ReactNode;
  defaultOpen?: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <div className="card overflow-hidden">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-4 py-2.5 hover:bg-[var(--background)] transition-colors text-left"
      >
        <span className="text-sm font-semibold text-[var(--foreground)]">
          {title}
        </span>
        <span
          className={`text-[var(--muted)] transition-transform ${
            open ? "rotate-180" : ""
          }`}
        >
          ▾
        </span>
      </button>
      {open && (
        <div className="border-t border-[var(--border-color)] p-4">
          {children}
        </div>
      )}
    </div>
  );
}

export default function ConfigStatusPanel({ config }: { config: SystemConfig | null }) {
  if (!config)
    return <p className="text-sm text-[var(--muted)]">加载中...</p>;

  return (
    <div className="space-y-3">
      {/* 服务配置 */}
      <AccordionPanel title="服务配置" defaultOpen>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs text-[var(--muted)]">
              <th className="px-2 py-1 text-left w-1/3">配置项</th>
              <th className="px-2 py-1 text-left">当前值</th>
              <th className="px-2 py-1 text-right">来源</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border-color)]">
            {Object.entries(config.serviceConfig).map(([label, item]) => (
              <tr key={label}>
                <td className="px-2 py-1.5 text-[var(--muted)]">{label}</td>
                <td className="px-2 py-1.5 font-mono text-xs">{item.value}</td>
                <td className="px-2 py-1.5 text-right">
                  <span className="tag tag-default text-xs">
                    {sourceLabels[item.source] || item.source}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </AccordionPanel>

      {/* 资源配置 */}
      <AccordionPanel title="资源配置">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs text-[var(--muted)]">
              <th className="px-2 py-1 text-left w-1/3">配置项</th>
              <th className="px-2 py-1 text-left">当前值</th>
              <th className="px-2 py-1 text-right">来源</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border-color)]">
            {Object.entries(config.resourceConfig).map(([label, item]) => (
              <tr key={label}>
                <td className="px-2 py-1.5 text-[var(--muted)]">{label}</td>
                <td className="px-2 py-1.5 font-mono text-xs">{item.value}</td>
                <td className="px-2 py-1.5 text-right">
                  <span className="tag tag-default text-xs">
                    {sourceLabels[item.source] || item.source}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </AccordionPanel>

      {/* Provider 密钥状态 */}
      <AccordionPanel title="Provider 密钥状态">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs text-[var(--muted)]">
              <th className="px-2 py-1 text-left">Provider</th>
              <th className="px-2 py-1 text-left">环境变量</th>
              <th className="px-2 py-1 text-center">状态</th>
              <th className="px-2 py-1 text-right">当前值</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border-color)]">
            {config.providerKeyStatus.map((item) => (
              <tr key={item.provider}>
                <td className="px-2 py-1.5 font-medium text-[var(--foreground)]">
                  {item.provider}
                </td>
                <td className="px-2 py-1.5 font-mono text-xs text-[var(--muted)]">
                  {item.envVar}
                </td>
                <td className="px-2 py-1.5 text-center">
                  {item.isSet ? (
                    <span className="text-[var(--success)] text-sm">✓</span>
                  ) : (
                    <span className="text-[var(--muted)] text-sm">—</span>
                  )}
                </td>
                <td className="px-2 py-1.5 text-right font-mono text-xs text-[var(--muted)]">
                  {item.maskedValue}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className="mt-2 text-xs text-[var(--muted)]">
          敏感值仅显示首尾字符，完整密钥不会通过 API 返回
        </p>
      </AccordionPanel>
    </div>
  );
}
