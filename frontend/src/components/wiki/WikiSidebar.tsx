'use client';

/**
 * WikiSidebar 侧边栏组件（V4 占位）。
 * 当前阶段保持与现有 page.tsx 结构兼容，后续渐进式迁移。
 */
interface WikiSidebarProps {
  repositoryId: string;
}

export default function WikiSidebar({ repositoryId: _repositoryId }: WikiSidebarProps) {
  // V4 占位组件——实际侧边栏渲染逻辑保留在 page.tsx 中以确保稳定性
  return null;
}
