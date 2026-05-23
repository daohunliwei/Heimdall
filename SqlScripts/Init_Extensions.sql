-- Heimdall 数据库扩展初始化
-- 用途：启用 PostgreSQL 扩展

-- pgvector：向量存储与检索（如未来重新启用向量功能）
CREATE EXTENSION IF NOT EXISTS vector;

-- 常用扩展
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";      -- UUID 生成
CREATE EXTENSION IF NOT EXISTS pg_trgm;          -- 三元组模糊搜索（用于 ILIKE 优化）
