using System.Text.RegularExpressions;
using Heimdall.Api.Models;
using Heimdall.Api.Services.Chat;

namespace Heimdall.Api.Services.Tasks;

/// <summary>
/// Ask 任务服务，负责普通问答与深度研究编排。
/// </summary>
public sealed class AskTaskService
{
    private const int MaxResearchIterations = 5;
    private readonly ChatOrchestratorService _chatOrchestratorService;
    private readonly TaskRequestUtilityService _taskRequestUtilityService;

    /// <summary>
    /// 初始化 Ask 任务服务。
    /// </summary>
    public AskTaskService(ChatOrchestratorService chatOrchestratorService, TaskRequestUtilityService taskRequestUtilityService)
    {
        _chatOrchestratorService = chatOrchestratorService;
        _taskRequestUtilityService = taskRequestUtilityService;
    }

    /// <summary>
    /// 生成问答结果。
    /// </summary>
    public async Task<AskTaskResponse> GenerateAsync(AskTaskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new InvalidOperationException("问题不能为空。");
        }

        return request.DeepResearch
            ? await GenerateDeepResearchAsync(request, cancellationToken)
            : await GenerateSimpleAskAsync(request, cancellationToken);
    }

    private async Task<AskTaskResponse> GenerateSimpleAskAsync(AskTaskRequest request, CancellationToken cancellationToken)
    {
        var history = request.History.ToList();
        history.Add(new ChatMessage
        {
            Role = "user",
            Content = request.Question.Trim()
        });

        var chatRequest = _taskRequestUtilityService.BuildChatRequest(request, history, request.FilePath);
        var content = await _chatOrchestratorService.GenerateAsync(chatRequest, cancellationToken);
        return new AskTaskResponse
        {
            Content = content,
            Complete = true,
            Iterations = 1
        };
    }

    private async Task<AskTaskResponse> GenerateDeepResearchAsync(AskTaskRequest request, CancellationToken cancellationToken)
    {
        var messages = request.History.ToList();
        var stages = new List<AskResearchStage>();
        var finalContent = string.Empty;
        var iterations = 0;
        var complete = false;

        for (var iteration = 1; iteration <= MaxResearchIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations = iteration;

            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = iteration == 1
                    ? $"[DEEP RESEARCH] {request.Question.Trim()}"
                    : "[DEEP RESEARCH] Continue the research"
            });

            var chatRequest = _taskRequestUtilityService.BuildChatRequest(request, messages, request.FilePath);
            var content = await _chatOrchestratorService.GenerateAsync(chatRequest, cancellationToken);
            finalContent = content;

            var stage = ExtractResearchStage(content, iteration);
            if (stage is not null)
            {
                stages.Add(stage);
            }

            complete = CheckIfResearchComplete(content);
            messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = content
            });

            if (complete)
            {
                break;
            }
        }

        if (!complete)
        {
            finalContent += "\n\n## Final Conclusion\nAfter multiple iterations of deep research, we've gathered significant insights about this topic. This concludes our investigation process, having reached the maximum number of research iterations.";
            stages.Add(new AskResearchStage
            {
                Title = "Final Conclusion",
                Content = finalContent,
                Iteration = iterations,
                Type = "conclusion"
            });
            complete = true;
        }

        return new AskTaskResponse
        {
            Content = finalContent,
            Stages = stages,
            Complete = complete,
            Iterations = iterations
        };
    }

    private static bool CheckIfResearchComplete(string content)
    {
        if (content.Contains("## Final Conclusion", StringComparison.Ordinal))
        {
            return true;
        }

        if ((content.Contains("## Conclusion", StringComparison.Ordinal) || content.Contains("## Summary", StringComparison.Ordinal)) &&
            !content.Contains("I will now proceed to", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("Next Steps", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("next iteration", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return content.Contains("This concludes our research", StringComparison.OrdinalIgnoreCase)
            || content.Contains("This completes our investigation", StringComparison.OrdinalIgnoreCase)
            || content.Contains("This concludes the deep research process", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Key Findings and Implementation Details", StringComparison.OrdinalIgnoreCase)
            || content.Contains("In conclusion,", StringComparison.OrdinalIgnoreCase)
            || (content.Contains("Final", StringComparison.OrdinalIgnoreCase) &&
                content.Contains("Conclusion", StringComparison.OrdinalIgnoreCase));
    }

    private static AskResearchStage? ExtractResearchStage(string content, int iteration)
    {
        if (iteration == 1 && content.Contains("## Research Plan", StringComparison.Ordinal))
        {
            return new AskResearchStage
            {
                Title = "Research Plan",
                Content = content,
                Iteration = iteration,
                Type = "plan"
            };
        }

        var updateMatch = Regex.Match(content, $"## Research Update {iteration}([\\s\\S]*?)(?:## Next Steps|$)", RegexOptions.IgnoreCase);
        if (updateMatch.Success)
        {
            return new AskResearchStage
            {
                Title = $"Research Update {iteration}",
                Content = content,
                Iteration = iteration,
                Type = "update"
            };
        }

        if (content.Contains("## Final Conclusion", StringComparison.Ordinal))
        {
            return new AskResearchStage
            {
                Title = "Final Conclusion",
                Content = content,
                Iteration = iteration,
                Type = "conclusion"
            };
        }

        return null;
    }
}
