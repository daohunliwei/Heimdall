import type { NextConfig } from "next";

const TARGET_SERVER_BASE_URL = process.env.SERVER_BASE_URL || 'http://localhost:8001';

const nextConfig: NextConfig = {
  output: 'standalone',

  // V4：Windows 下文件监听排除大目录，避免内存泄露
  // Next.js 默认 watchOptions 不可在此扩展，改用环境变量 CHOKIDAR_USEPOLLING=1 或 .next 缓存控制
  // 实际内存优化在前端根目录 .gitignore 中排除 backend/ .trae/ doc/ .playwright-mcp/
  async rewrites() {
    return [
      {
        source: '/api/wiki_cache/:path*',
        destination: `${TARGET_SERVER_BASE_URL}/api/wiki_cache/:path*`,
      },
      {
        source: '/api/tasks/:path*',
        destination: `${TARGET_SERVER_BASE_URL}/tasks/:path*`,
      },
      {
        source: '/export/wiki/:path*',
        destination: `${TARGET_SERVER_BASE_URL}/export/wiki/:path*`,
      },
      {
        source: '/api/wiki_cache',
        destination: `${TARGET_SERVER_BASE_URL}/api/wiki_cache`,
      },
      {
        source: '/local_repo/structure',
        destination: `${TARGET_SERVER_BASE_URL}/local_repo/structure`,
      },
      {
        source: '/api/auth/status',
        destination: `${TARGET_SERVER_BASE_URL}/auth/status`,
      },
      {
        source: '/api/auth/validate',
        destination: `${TARGET_SERVER_BASE_URL}/auth/validate`,
      },
      {
        source: '/api/lang/config',
        destination: `${TARGET_SERVER_BASE_URL}/lang/config`,
      },
      // V2 新增：仓库与项目 API
      {
        source: '/api/repositories/:path*',
        destination: `${TARGET_SERVER_BASE_URL}/api/repositories/:path*`,
      },
      {
        source: '/api/processed_projects/:path*',
        destination: `${TARGET_SERVER_BASE_URL}/api/processed_projects/:path*`,
      },
      {
        source: '/api/processed_projects',
        destination: `${TARGET_SERVER_BASE_URL}/api/processed_projects`,
      },
      // 模型配置与 Chat API
      {
        source: '/api/models/config',
        destination: `${TARGET_SERVER_BASE_URL}/models/config`,
      },
      {
        source: '/api/chat/:path*',
        destination: `${TARGET_SERVER_BASE_URL}/chat/:path*`,
      },
      // 管理后台 API（后端路由为 /admin/* 不带 /api 前缀）
      {
        source: '/api/admin/:path*',
        destination: `${TARGET_SERVER_BASE_URL}/admin/:path*`,
      },
    ];
  },
};

export default nextConfig;
