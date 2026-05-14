"use client";

import { useTaskStream } from "@/hooks/useTaskStream";

interface TaskProgressProps {
  taskId: string | null;
  onComplete?: (result: unknown) => void;
  onError?: (error: string) => void;
}

export default function TaskProgress({ taskId, onComplete, onError }: TaskProgressProps) {
  const { progress, isComplete, error, result } = useTaskStream(taskId);

  if (!taskId) return null;

  return (
    <div className="card p-4 w-full">
      <div className="flex items-center gap-3">
        <div className="h-5 w-5 animate-spin rounded-full border-2 border-[var(--accent-primary)] border-t-transparent" />
        <span className="text-sm font-medium text-[var(--foreground)]">
          {progress?.message || "正在准备..."}
        </span>
      </div>
      {progress && (
        <div className="mt-3">
          <div className="h-2 w-full rounded-full bg-[var(--border-color)]">
            <div
              className="h-2 rounded-full bg-[var(--accent-primary)] transition-all duration-500"
              style={{ width: `${Math.min(progress.percent, 100)}%` }}
            />
          </div>
          <div className="mt-1 flex justify-between text-xs text-[var(--muted)]">
            <span>{progress.phase}</span>
            <span>{progress.percent}%</span>
          </div>
        </div>
      )}
      {isComplete && !error && (
        <div className="mt-3 text-sm text-[var(--success)]">任务完成</div>
      )}
      {error && (
        <div className="mt-3 text-sm text-[var(--highlight)]">{error}</div>
      )}
    </div>
  );
}
