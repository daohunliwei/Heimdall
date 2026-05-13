using System.Text;
using System.Text.RegularExpressions;
using Heimdall.Api.Models;

namespace Heimdall.Api.Services.Tasks;

/// <summary>
/// Workshop 任务服务，负责后端主导生成训练营内容。
/// </summary>
public sealed class WorkshopTaskService
{
    private readonly TaskLlmService _taskLlmService;
    private readonly TaskPromptService _taskPromptService;
    private readonly TaskRequestUtilityService _taskRequestUtilityService;
    private readonly WikiTaskService _wikiTaskService;

    /// <summary>
    /// 初始化 Workshop 任务服务。
    /// </summary>
    public WorkshopTaskService(
        TaskLlmService taskLlmService,
        TaskPromptService taskPromptService,
        TaskRequestUtilityService taskRequestUtilityService,
        WikiTaskService wikiTaskService)
    {
        _taskLlmService = taskLlmService;
        _taskPromptService = taskPromptService;
        _taskRequestUtilityService = taskRequestUtilityService;
        _wikiTaskService = wikiTaskService;
    }

    /// <summary>
    /// 生成 Workshop 内容。
    /// </summary>
    public async Task<WorkshopTaskResponse> GenerateAsync(WorkshopTaskRequest request, CancellationToken cancellationToken)
    {
        var languageDisplayName = _taskRequestUtilityService.ResolveLanguageDisplayName(request);
        var wikiResponse = await _wikiTaskService.GenerateAsync(new WikiTaskRequest
        {
            RepoUrl = request.RepoUrl,
            Owner = request.Owner,
            Repo = request.Repo,
            Type = request.Type,
            Token = request.Token,
            Provider = request.Provider,
            Model = request.Model,
            CustomModel = request.CustomModel,
            Language = request.Language,
            ExcludedDirs = request.ExcludedDirs,
            ExcludedFiles = request.ExcludedFiles,
            IncludedDirs = request.IncludedDirs,
            IncludedFiles = request.IncludedFiles,
            ForceRefresh = request.ForceRefresh,
            Comprehensive = request.Comprehensive
        }, cancellationToken);

        var wikiContent = _taskPromptService.BuildWikiReferenceText(wikiResponse);
        var prompt = _taskPromptService.BuildWorkshopPrompt(
            wikiResponse.Repo.Owner,
            wikiResponse.Repo.Repo,
            wikiContent,
            languageDisplayName);

        var content = await _taskLlmService.GenerateTextAsync(request, prompt, cancellationToken);
        content = NormalizeWorkshopContent(content);

        return new WorkshopTaskResponse
        {
            Content = content
        };
    }

    private static string NormalizeWorkshopContent(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("```markdown", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```md", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!normalized.Contains("## Table of Contents", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("## Contents", StringComparison.OrdinalIgnoreCase))
        {
            normalized = InjectTableOfContents(normalized);
        }

        normalized = InjectExerciseProgress(normalized);
        normalized = InjectFinalProjectNote(normalized);
        return normalized;
    }

    private static string InjectTableOfContents(string content)
    {
        var headings = Regex.Matches(content, @"^## (.+)$", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        if (headings.Count == 0)
        {
            return content;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## Table of Contents");
        builder.AppendLine();
        foreach (var heading in headings)
        {
            var anchor = Regex.Replace(heading.ToLowerInvariant(), @"[^\w\s-]", string.Empty).Replace(' ', '-');
            builder.AppendLine($"- [{heading}](#{anchor})");
        }

        builder.AppendLine();
        var firstSectionIndex = content.IndexOf("## ", StringComparison.Ordinal);
        if (firstSectionIndex <= 0)
        {
            return builder + content;
        }

        return content.Insert(firstSectionIndex, builder.ToString());
    }

    private static string InjectExerciseProgress(string content)
    {
        var matches = Regex.Matches(content, @"^## Exercise (\d+):", RegexOptions.Multiline);
        if (matches.Count == 0)
        {
            return content;
        }

        var result = content;
        for (var index = matches.Count - 1; index >= 0; index--)
        {
            var match = matches[index];
            var exerciseNumber = int.Parse(match.Groups[1].Value);
            var estimatedTime = exerciseNumber switch
            {
                1 => 5,
                _ when exerciseNumber == matches.Count => 15,
                _ => 10 + Math.Min(5, exerciseNumber)
            };

            var insertPosition = result.IndexOf('\n', match.Index);
            if (insertPosition >= 0)
            {
                var note = $"\n<div style=\"text-align: right; font-size: 0.85em; color: #666;\">\nExercise {exerciseNumber} of {matches.Count} | Estimated time: {estimatedTime} minutes\n</div>\n";
                result = result.Insert(insertPosition + 1, note);
            }
        }

        return result;
    }

    private static string InjectFinalProjectNote(string content)
    {
        var match = Regex.Match(content, @"^## Final Project.*$", RegexOptions.Multiline);
        if (!match.Success)
        {
            return content;
        }

        var insertPosition = content.IndexOf('\n', match.Index);
        if (insertPosition < 0)
        {
            return content;
        }

        const string note = "\n<div style=\"text-align: right; font-size: 0.85em; color: #666;\">\nEstimated time: 20-30 minutes | Combines concepts from all exercises\n</div>\n";
        return content.Insert(insertPosition + 1, note);
    }
}
