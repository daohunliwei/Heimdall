'use client';

import React from 'react';
import { FaBookOpen } from 'react-icons/fa';

interface EmptyStateProps {
  message?: string;
  description?: string;
  icon?: React.ReactNode;
  action?: {
    label: string;
    onClick: () => void;
  };
  className?: string;
}

export default function EmptyState({
  message = '暂无数据',
  description,
  icon,
  action,
  className = '',
}: EmptyStateProps) {
  return (
    <div className={`flex-1 flex items-center justify-center min-h-[300px] ${className}`}>
      <div className="text-center">
        <div className="text-3xl mx-auto mb-3 opacity-30 text-[var(--muted)]">
          {icon || <FaBookOpen />}
        </div>
        <p className="text-sm text-[var(--foreground)] font-medium">{message}</p>
        {description && (
          <p className="text-xs text-[var(--muted)] mt-2 max-w-md">{description}</p>
        )}
        {action && (
          <button onClick={action.onClick} className="btn-primary text-sm mt-4 inline-flex">
            {action.label}
          </button>
        )}
      </div>
    </div>
  );
}
