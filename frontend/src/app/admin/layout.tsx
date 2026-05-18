"use client";

import { useAuth } from "@/contexts/AuthContext";
import { useRouter, usePathname } from "next/navigation";
import { useEffect } from "react";
import Link from "next/link";

const navItems = [
  { href: "/admin/dashboard", label: "仪表盘" },
  { href: "/admin/users", label: "用户管理" },
  { href: "/admin/settings", label: "全局设置" },
  { href: "/admin/tasks", label: "任务监控" },
  { href: "/admin/repositories", label: "仓库管理" },
  { href: "/admin/prompts", label: "提示词管理" },
];

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, loading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!loading && !isAuthenticated) {
      router.push("/login");
    }
  }, [loading, isAuthenticated, router]);

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[var(--background)]">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-[var(--accent-primary)] border-t-transparent" />
      </div>
    );
  }

  if (!isAuthenticated) return null;

  return (
    <div className="flex min-h-screen bg-[var(--background)]">
      <aside className="w-56 bg-[var(--card-bg)] shadow-sm border-r border-[var(--border-color)]">
        <div className="border-b border-[var(--border-color)] px-4 py-4">
          <h1 className="text-lg font-bold text-[var(--foreground)]">Heimdall 管理</h1>
        </div>
        <nav className="mt-2">
          {navItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={`block px-4 py-2 text-sm transition-colors ${
                pathname === item.href
                  ? "bg-[var(--accent-secondary)] text-[var(--accent-primary)] border-r-2 border-[var(--accent-primary)]"
                  : "text-[var(--foreground)] hover:bg-[var(--background)]"
              }`}
            >
              {item.label}
            </Link>
          ))}
        </nav>
      </aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
