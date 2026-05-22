-- Heimdall 种子数据初始化
-- 用途：插入系统运行必需的基础数据

-- ===== 默认系统设置 =====
INSERT INTO system_settings (id, key, value, description) VALUES
    (gen_random_uuid(), 'system.version', '9.0', '系统版本号'),
    (gen_random_uuid(), 'wiki.default_language', 'zh', 'Wiki 默认生成语言'),
    (gen_random_uuid(), 'wiki.default_profile', 'comprehensive', 'Wiki 默认生成档位'),
    (gen_random_uuid(), 'task.max_retry', '3', '任务最大重试次数'),
    (gen_random_uuid(), 'task.auto_resume_window_hours', '24', '任务自动恢复窗口（小时）')
ON CONFLICT DO NOTHING;

-- ===== 默认提示词模板 =====
INSERT INTO prompt_templates (id, slug, name, layer, scope_type, category, template_content, is_system, is_active, priority, version) VALUES
    (gen_random_uuid(), 'system-default', '系统默认提示词', 'system', 'global', 'general',
     '你是 Heimdall，一个专业的代码仓库分析助手。你的任务是将代码仓库转换为高质量的中文技术文档。请保持专业、准确、简洁。',
     TRUE, TRUE, 0, 1),
    (gen_random_uuid(), 'wiki-structure-planning', 'Wiki 结构规划', 'system', 'global', 'wiki',
     '你需要分析代码仓库结构，规划 Wiki 文档的目录结构。请根据代码的模块划分、依赖关系和重要程度，设计合理的信息架构。',
     TRUE, TRUE, 10, 1),
    (gen_random_uuid(), 'wiki-page-generation', 'Wiki 页面生成', 'system', 'global', 'wiki',
     '你需要根据提供的代码文件和上下文信息，生成高质量的中文技术文档页面。请确保内容准确、结构清晰、易于理解。',
     TRUE, TRUE, 10, 1),
    (gen_random_uuid(), 'wiki-quality-review', 'Wiki 质量审查', 'system', 'global', 'wiki',
     '你需要审查已生成的 Wiki 页面，检查内容准确性、完整性和一致性。如发现问题请指出并给出修改建议。',
     TRUE, TRUE, 10, 1)
ON CONFLICT DO NOTHING;
