-- Heimdall 种子数据初始化
-- 列名使用 PascalCase（与现有 EF Core 数据库一致）

-- ===== 默认系统设置 =====
INSERT INTO system_settings ("Id", "Key", "Value", "Description", "UpdatedAt") VALUES
    (gen_random_uuid(), 'system.version', '9.0', '系统版本号', NOW()),
    (gen_random_uuid(), 'wiki.default_language', 'zh', 'Wiki 默认生成语言', NOW()),
    (gen_random_uuid(), 'wiki.default_profile', 'comprehensive', 'Wiki 默认生成档位', NOW()),
    (gen_random_uuid(), 'task.max_retry', '3', '任务最大重试次数', NOW()),
    (gen_random_uuid(), 'task.auto_resume_window_hours', '24', '任务自动恢复窗口（小时）', NOW())
ON CONFLICT DO NOTHING;

-- ===== 默认提示词模板 =====
INSERT INTO prompt_templates ("Id", "Slug", "Name", "Layer", "ScopeType", "Category", "TemplateContent", "IsSystem", "IsActive", "Priority", "Version", "CreatedAt", "UpdatedAt")
VALUES
    (gen_random_uuid(), 'system-default', '系统默认提示词', 'system', 'global', 'general',
     '你是 Heimdall，一个专业的代码仓库分析助手。你的任务是将代码仓库转换为高质量的中文技术文档。请保持专业、准确、简洁。',
     TRUE, TRUE, 0, 1, NOW(), NOW())
ON CONFLICT DO NOTHING;
