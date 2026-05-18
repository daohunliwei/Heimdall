'use client';

import React from 'react';
import { FaExclamationTriangle } from 'react-icons/fa';

interface ErrorStateProps {
  message?: string;
  details?: string;
  title?: string;
  onRetry?: () => void;
  retryLabel?: string;
  showIcon?: boolean;
  className?: string;
  inline?: boolean;
}

export default function ErrorState({
  message = '发生错误',
  details,
  title = '出错了',
  onRetry,
  retryLabel = '重试',
  showIcon = true,
  className = '',
  inline = false,
}: ErrorStateProps) {
  if (inline) {
    return (
      <div className={`p-4 rounded-lg bg-[var(--highlight-light)] border border-[var(--highlight)]/20 ${className}`}>
        <div className="flex items-center gap-2 text-[var(--highlight)] mb-2">
          {showIcon && <FaExclamationTriangle className="flex-shrink-0" />}
          <span className="font-semibold text-sm">{title}</span>
        </div>
        <p className="text-sm text-[var(--foreground)]">{message}</p>
        {details && (
          <pre className="text-xs whitespace-pre-wrap break-words bg-[var(--background)]/70 border border-[var(--border-color)] rounded-md p-3 mt-3 overflow-x-auto">
            {details}
          </pre>
        )}
        {onRetry && (
          <button onClick={onRetry} className="btn-secondary text-sm mt-3">
            重试
          </button>
        )}
      </div>
    );
  }

  return (
    <div className={`flex-1 flex items-center justify-center ${className}`}>
      <div className="max-w-lg w-full p-6 rounded-lg bg-[var(--highlight-light)] border border-[var(--highlight)]/20">
        <div className="flex items-center gap-2 text-[var(--highlight)] mb-3">
          {showIcon && <FaExclamationTriangle />}
          <span className="font-semibold">{title}</span>
        </div>
        <p className="text-sm text-[var(--foreground)] mb-4">{message}</p>
        {details && (
          <pre className="text-xs whitespace-pre-wrap break-words bg-[var(--background)]/70 border border-[var(--border-color)] rounded-md p-3 mb-4 overflow-x-auto">
            {details}
          </pre>
        )}
        {onRetry && (
          <button onClick={onRetry} className="btn-secondary text-sm">
            {retryLabel}
          </button>
        )}
      </div>
    </div>
  );
}
