-- V2 数据回填脚本
-- 作用：为已有仓库创建默认 wiki_space、repository_version、wiki_version，并关联 wiki_pages
-- 执行方式：psql -h 10.189.10.252 -p 5432 -U beisen_admin -d ai_heimdall_base -f backfill_v2.sql

BEGIN;

-- 1. 为每个已有仓库创建默认 wiki_space (language=zh, view_type=default)
INSERT INTO wiki_spaces (id, repository_id, language, view_type, title, description, created_at, updated_at)
SELECT
    gen_random_uuid(),
    r."Id",
    'zh',
    'default',
    r.display_name || ' Wiki',
    '为 ' || r.display_name || ' 自动生成的 Wiki 空间',
    NOW(),
    NOW()
FROM repositories r
WHERE NOT EXISTS (
    SELECT 1 FROM wiki_spaces ws
    WHERE ws.repository_id = r."Id" AND ws.language = 'zh' AND ws.view_type = 'default'
)
AND r.display_name IS NOT NULL AND r.display_name != '';

-- 2. 为每个仓库创建初始 repository_version (commit_sha=unknown)
INSERT INTO repository_versions (id, repository_id, branch_name, commit_sha, tree_fingerprint,
    commit_time, commit_author, commit_message, source_status, is_latest_on_branch,
    version_source_confidence, created_at)
SELECT
    gen_random_uuid(),
    r."Id",
    COALESCE(r.default_branch, 'main'),
    'unknown',
    NULL,
    r.created_at,
    'system',
    '初始版本（数据回填）',
    'active',
    TRUE,
    'unknown',
    NOW()
FROM repositories r
WHERE NOT EXISTS (
    SELECT 1 FROM repository_versions rv
    WHERE rv.repository_id = r."Id" AND rv.branch_name = COALESCE(r.default_branch, 'main')
);

-- 3. 将现有 Wiki 映射为 wiki_version
INSERT INTO wiki_versions (id, wiki_space_id, repository_version_id, version_no,
    generation_mode, generation_profile, prompt_profile_hash, model_profile_hash,
    status, is_force_refresh, page_count, toc_depth, summary_markdown,
    created_by_task_id, created_at, completed_at)
SELECT
    gen_random_uuid(),
    ws.id,
    rv.id,
    1,
    'rebuild',
    'comprehensive',
    NULL,
    NULL,
    'ready',
    FALSE,
    (SELECT COUNT(*) FROM wiki_pages wp WHERE wp."WikiId" = w."Id"),
    1,
    w."Description",
    NULL,
    w."CreatedAt",
    w."UpdatedAt"
FROM wikis w
JOIN wiki_spaces ws ON ws.repository_id = w."SourceRepositoryId"
    AND ws.language = 'zh' AND ws.view_type = 'default'
JOIN repository_versions rv ON rv.repository_id = w."SourceRepositoryId"
    AND rv.branch_name = w."SourceBranch"
WHERE NOT EXISTS (
    SELECT 1 FROM wiki_versions wv
    WHERE wv.wiki_space_id = ws.id AND wv.repository_version_id = rv.id
);

-- 4. 更新 wiki_pages 关联到 wiki_version
UPDATE wiki_pages wp
SET "WikiVersionId" = wv.id,
    page_type = CASE WHEN wp."PageType" IS NULL OR wp."PageType" = '' THEN 'article' ELSE wp."PageType" END,
    status = CASE WHEN wp."Status" IS NULL OR wp."Status" = '' THEN 'ready' ELSE wp."Status" END
FROM wikis w
JOIN wiki_spaces ws ON ws.repository_id = w."SourceRepositoryId"
    AND ws.language = 'zh' AND ws.view_type = 'default'
JOIN repository_versions rv ON rv.repository_id = w."SourceRepositoryId"
    AND rv.branch_name = w."SourceBranch"
JOIN wiki_versions wv ON wv.wiki_space_id = ws.id AND wv.repository_version_id = rv.id
WHERE wp."WikiId" = w."Id"
AND wp."WikiVersionId" IS NULL;

-- 5. 设置发布态
UPDATE wiki_spaces ws
SET published_wiki_version_id = wv.id
FROM wiki_versions wv
WHERE ws.published_wiki_version_id IS NULL
AND wv.wiki_space_id = ws.id
AND wv.status = 'ready';

-- 6. 更新发布版本状态
UPDATE wiki_versions wv
SET status = 'published'
FROM wiki_spaces ws
WHERE ws.published_wiki_version_id = wv.id
AND wv.status = 'ready';

-- 验证结果
SELECT 'WikiSpaces' as entity, COUNT(*) as count FROM wiki_spaces
UNION ALL
SELECT 'RepositoryVersions', COUNT(*) FROM repository_versions
UNION ALL
SELECT 'WikiVersions', COUNT(*) FROM wiki_versions
UNION ALL
SELECT 'WikiPages (total)', COUNT(*) FROM wiki_pages
UNION ALL
SELECT 'WikiPages (with version)', COUNT(*) FROM wiki_pages WHERE "WikiVersionId" IS NOT NULL
UNION ALL
SELECT 'Published Spaces', COUNT(*) FROM wiki_spaces WHERE published_wiki_version_id IS NOT NULL;

COMMIT;
