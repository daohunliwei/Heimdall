using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Heimdall.Core.Services;

public class CodeFirstSyncService
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<CodeFirstSyncService> _logger;

    public CodeFirstSyncService(ISqlSugarClient db, ILogger<CodeFirstSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SyncAsync()
    {
        var sw = Stopwatch.StartNew();
        int successCount = 0;
        int failedCount = 0;
        var failedTables = new List<string>();

        try
        {
            // 扫描 Core.Entities 命名空间的所有实体类型
            var entityTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                    && t.Namespace == "Heimdall.Core.Entities"
                    && t.GetCustomAttribute<SugarTable>() != null)
                .ToArray();

            _logger.LogInformation("扫描到 {Count} 个实体类型，开始 CodeFirst 同步", entityTypes.Length);

            // 执行列命名统一迁移（PascalCase → snake_case）
            await RunColumnNamingMigrationAsync();

            foreach (var type in entityTypes)
            {
                try
                {
                    _db.CodeFirst.SetStringDefaultLength(200).InitTables(type);
                    successCount++;
                    _logger.LogDebug("实体 {Entity} 同步成功", type.Name);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    failedTables.Add($"{type.Name}: {ex.Message}");
                    _logger.LogError(ex, "实体 {Entity} 同步失败: {Message}", type.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "实体扫描失败，CodeFirst 同步中断");
        }

        sw.Stop();
        _logger.LogInformation("CodeFirst 同步完成: 成功 {SuccessCount} 张表, 失败 {FailedCount} 张表, 耗时 {ElapsedMs}ms",
            successCount, failedCount, sw.ElapsedMilliseconds);

        if (failedTables.Count > 0)
        {
            _logger.LogWarning("失败的表: {FailedTables}", string.Join("; ", failedTables));
        }
    }

    /// <summary>
    /// 执行列命名统一迁移：将 144 个 PascalCase 列重命名为 snake_case。
    /// 迁移是幂等的——已重命名的列会跳过（RENAME COLUMN 失败时不报错）。
    /// </summary>
    private async Task RunColumnNamingMigrationAsync()
    {
        var migrations = new (string Table, string Old, string New)[]
        {
            ("code_index_entries","FilePath","file_path"), ("code_index_entries","ModuleName","module_name"), ("code_index_entries","FileType","file_type"), ("code_index_entries","Language","language"), ("code_index_entries","CallGraphJson","call_graph_json"), ("code_index_entries","DependencyEdgesJson","dependency_edges_json"), ("code_index_entries","DesignPatternHints","design_pattern_hints"), ("code_index_entries","SizeBytes","size_bytes"), ("code_index_entries","ImportanceScore","importance_score"), ("code_index_entries","RepositoryVersionId","repository_version_id"),
            ("code_index_chunks","Content","content"), ("code_index_chunks","Language","language"), ("code_index_chunks","StartLine","start_line"), ("code_index_chunks","EndLine","end_line"), ("code_index_chunks","CodeIndexEntryId","code_index_entry_id"),
            ("llm_call_metrics","TaskId","task_id"), ("llm_call_metrics","Stage","stage"), ("llm_call_metrics","Provider","provider"), ("llm_call_metrics","Model","model"), ("llm_call_metrics","InputTokens","input_tokens"), ("llm_call_metrics","OutputTokens","output_tokens"), ("llm_call_metrics","CacheHitTokens","cache_hit_tokens"), ("llm_call_metrics","LatencyMs","latency_ms"), ("llm_call_metrics","Success","success"), ("llm_call_metrics","ErrorType","error_type"), ("llm_call_metrics","IsEstimated","is_estimated"), ("llm_call_metrics","IsStreaming","is_streaming"), ("llm_call_metrics","FirstTokenLatencyMs","first_token_latency_ms"), ("llm_call_metrics","CreatedAt","created_at"),
            ("prompt_template_history","PromptTemplateId","prompt_template_id"), ("prompt_template_history","Version","version"), ("prompt_template_history","TemplateContent","template_content"), ("prompt_template_history","ChangedBy","changed_by"), ("prompt_template_history","ChangedAt","changed_at"),
            ("prompt_templates","Slug","slug"), ("prompt_templates","Name","name"), ("prompt_templates","Layer","layer"), ("prompt_templates","ScopeType","scope_type"), ("prompt_templates","ScopeValue","scope_value"), ("prompt_templates","TemplateContent","template_content"), ("prompt_templates","Category","category"), ("prompt_templates","SubCategory","sub_category"), ("prompt_templates","Priority","priority"), ("prompt_templates","ApplicableProviders","applicable_providers"), ("prompt_templates","Variables","variables"), ("prompt_templates","IsSystem","is_system"), ("prompt_templates","IsActive","is_active"), ("prompt_templates","Version","version"), ("prompt_templates","CreatedAt","created_at"), ("prompt_templates","UpdatedAt","updated_at"),
            ("provider_model_metadata","ProviderKey","provider_key"), ("provider_model_metadata","ModelName","model_name"), ("provider_model_metadata","BillingType","billing_type"), ("provider_model_metadata","MaxContextTokens","max_context_tokens"), ("provider_model_metadata","MaxOutputTokens","max_output_tokens"), ("provider_model_metadata","RateLimitPerMinute","rate_limit_per_minute"), ("provider_model_metadata","InputTokenPrice","input_token_price"), ("provider_model_metadata","OutputTokenPrice","output_token_price"), ("provider_model_metadata","CallPrice","call_price"), ("provider_model_metadata","SupportsCaching","supports_caching"), ("provider_model_metadata","ContextFillRatio","context_fill_ratio"), ("provider_model_metadata","ContextWarningThreshold","context_warning_threshold"), ("provider_model_metadata","SupportsStreaming","supports_streaming"), ("provider_model_metadata","RawEndpoint","raw_endpoint"), ("provider_model_metadata","UpdatedAt","updated_at"),
            ("repositories","Owner","owner"), ("repositories","RepoName","repo_name"), ("repositories","RepoType","repo_type"), ("repositories","RepoUrl","repo_url"), ("repositories","CloneUrl","clone_url"), ("repositories","DefaultBranch","default_branch"), ("repositories","DefaultLanguage","default_language"), ("repositories","Description","description"), ("repositories","CreatedAt","created_at"), ("repositories","UpdatedAt","updated_at"),
            ("repository_prompt_overrides","RepositoryId","repository_id"), ("repository_prompt_overrides","PromptTemplateId","prompt_template_id"), ("repository_prompt_overrides","OverrideContent","override_content"), ("repository_prompt_overrides","Strategy","strategy"), ("repository_prompt_overrides","Priority","priority"), ("repository_prompt_overrides","IsEnabled","is_enabled"), ("repository_prompt_overrides","CreatedAt","created_at"),
            ("repository_versions","RepositoryId","repository_id"),
            ("system_settings","Key","key"), ("system_settings","Value","value"), ("system_settings","Description","description"), ("system_settings","UpdatedAt","updated_at"),
            ("task_artifacts","TaskId","task_id"),
            ("task_llm_call_logs","TaskId","task_id"), ("task_llm_call_logs","StepOrder","step_order"), ("task_llm_call_logs","CallType","call_type"), ("task_llm_call_logs","Provider","provider"), ("task_llm_call_logs","Model","model"), ("task_llm_call_logs","PromptTokens","prompt_tokens"), ("task_llm_call_logs","CompletionTokens","completion_tokens"), ("task_llm_call_logs","TotalTokens","total_tokens"), ("task_llm_call_logs","RequestPreview","request_preview"), ("task_llm_call_logs","ResponsePreview","response_preview"), ("task_llm_call_logs","LatencyMs","latency_ms"), ("task_llm_call_logs","IsError","is_error"), ("task_llm_call_logs","ErrorMessage","error_message"), ("task_llm_call_logs","ToolCallLogsJson","tool_call_logs_json"), ("task_llm_call_logs","CreatedAt","created_at"),
            ("tasks","TaskType","task_type"), ("tasks","RepositoryId","repository_id"), ("tasks","UserId","user_id"), ("tasks","RequestHash","request_hash"), ("tasks","Provider","provider"), ("tasks","Model","model"), ("tasks","Language","language"), ("tasks","ProgressPercent","progress_percent"), ("tasks","ProgressMessage","progress_message"), ("tasks","TotalPromptTokens","total_prompt_tokens"), ("tasks","TotalCompletionTokens","total_completion_tokens"), ("tasks","ResultJson","result_json"), ("tasks","ErrorMessage","error_message"), ("tasks","CreatedAt","created_at"), ("tasks","UpdatedAt","updated_at"), ("tasks","StartedAt","started_at"), ("tasks","CompletedAt","completed_at"),
            ("users","Username","username"), ("users","Email","email"), ("users","PasswordHash","password_hash"), ("users","Source","source"), ("users","Role","role"), ("users","IsActive","is_active"), ("users","CreatedAt","created_at"), ("users","UpdatedAt","updated_at"),
            ("wiki_page_relations","WikiVersionId","wiki_version_id"), ("wiki_page_relations","SourcePageId","source_page_id"), ("wiki_page_relations","TargetPageId","target_page_id"),
            ("wiki_pages","WikiVersionId","wiki_version_id"), ("wiki_pages","TaskId","task_id"), ("wiki_pages","PageOrder","page_order"), ("wiki_pages","Title","title"), ("wiki_pages","ContentMarkdown","content_markdown"), ("wiki_pages","ParentPageId","parent_page_id"), ("wiki_pages","Importance","importance"), ("wiki_pages","FilePaths","file_paths"), ("wiki_pages","CreatedAt","created_at"), ("wiki_pages","UpdatedAt","updated_at"),
            ("wiki_spaces","RepositoryId","repository_id"),
            ("wiki_versions","WikiSpaceId","wiki_space_id"), ("wiki_versions","RepositoryVersionId","repository_version_id"),
        };

        int done = 0;
        foreach (var (table, oldName, newName) in migrations)
        {
            try
            {
                await _db.Ado.ExecuteCommandAsync(
                    $"ALTER TABLE {table} RENAME COLUMN \"{oldName}\" TO \"{newName}\"");
                done++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "列重命名跳过（可能已完成）: {Table}.{Old} -> {New}", table, oldName, newName);
            }
        }

        _logger.LogInformation("列命名迁移完成: {Done} 列已重命名", done);
    }
}
