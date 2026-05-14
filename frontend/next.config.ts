import type { NextConfig } from "next";

const TARGET_SERVER_BASE_URL = process.env.SERVER_BASE_URL || 'http://localhost:8001';

const nextConfig: NextConfig = {
  output: 'standalone',
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
