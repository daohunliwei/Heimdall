'use client';

import React from 'react';

interface LoadingStateProps {
  message?: string;
  progressPercent?: number;
  stageMessage?: string;
  taskId?: string;
  className?: string;
}

export default function LoadingState({
  message = '加载中...',
  progressPercent,
  stageMessage,
  taskId,
  className = '',
}: LoadingStateProps) {
  return (
    <div className={`flex-1 flex items-center justify-center ${className}`}>
      <div className="text-center">
        <div className="flex items-center justify-center gap-1.5 mb-4">
          <div className="w-2.5 h-2.5 rounded-full bg-[var(--accent-primary)] animate-bounce" />
          <div className="w-2.5 h-2.5 rounded-full bg-[var(--accent-primary)] animate-bounce"
            style={{ animationDelay: '0.1s' }} />
          <div className="w-2.5 h-2.5 rounded-full bg-[var(--accent-primary)] animate-bounce"
            style={{ animationDelay: '0.2s' }} />
        </div>
        <p className="text-sm text-[var(--foreground)] font-medium">{message}</p>
        {progressPercent !== undefined && (
          <div className="mt-3 w-48 mx-auto">
            <div className="h-1.5 rounded-full bg-[var(--border-color)] overflow-hidden">
              <div
                className="h-full rounded-full bg-[var(--accent-primary)] transition-all duration-500"
                style={{ width: `${Math.min(100, Math.max(0, progressPercent))}%` }}
              />
            </div>
            <p className="text-xs text-[var(--muted)] mt-1">{progressPercent}%</p>
          </div>
        )}
        {stageMessage && (
          <p className="text-xs text-[var(--muted)] mt-2">{stageMessage}</p>
        )}
        {taskId && (
          <p className="text-xs text-[var(--muted)] mt-2">任务 ID：{taskId}</p>
        )}
      </div>
    </div>
  );
}
