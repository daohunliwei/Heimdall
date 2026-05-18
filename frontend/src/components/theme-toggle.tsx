"use client";

import { useTheme } from "next-themes";

export default function ThemeToggle() {
  const { theme, setTheme } = useTheme();

  return (
    <button
      type="button"
      className="relative w-9 h-9 cursor-pointer bg-transparent border border-[var(--border-color)] text-[var(--foreground)] hover:border-[var(--accent-primary)] hover:bg-[var(--accent-secondary)] rounded-lg transition-all duration-200 flex items-center justify-center"
      title="Toggle theme"
      aria-label="Toggle theme"
      onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
    >
      {/* Japanese-inspired sun and moon icons */}
      {/* Sun icon */}
      <svg viewBox="0 0 24 24" fill="none" className={`w-4 h-4 absolute inset-0 m-auto transition-all duration-300 ${theme === 'dark' ? 'opacity-0 rotate-90 scale-0' : 'opacity-100 rotate-0 scale-100'}`} aria-label="Light Mode">
        <circle cx="12" cy="12" r="4" stroke="currentColor" strokeWidth="1.5" />
        <path d="M12 2V4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        <path d="M12 20V22" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        <path d="M4 12L2 12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        <path d="M22 12L20 12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      </svg>
      {/* Moon icon */}
      <svg viewBox="0 0 24 24" fill="none" className={`w-4 h-4 absolute inset-0 m-auto transition-all duration-300 ${theme === 'dark' ? 'opacity-100 rotate-0 scale-100' : 'opacity-0 -rotate-90 scale-0'}`} aria-label="Dark Mode">
        <path d="M20 14.12A7 7 0 0 1 9.88 4a6 6 0 1 0 8.24 8.24 3 3 0 0 1 1.88 1.88z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </button>
  );
}
