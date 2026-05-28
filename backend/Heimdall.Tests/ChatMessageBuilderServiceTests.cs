using Heimdall.Core.Services.Tasks;
using Microsoft.Extensions.AI;

namespace Heimdall.Tests;

/// <summary>
/// ChatMessageBuilderService 测试 — 验证结构化消息构建
/// </summary>
public class ChatMessageBuilderServiceTests
{
    private readonly ChatMessageBuilderService _service = new();

    [Fact]
    public void BuildWikiMessages_AllParams_ShouldReturnThreeMessages()
    {
        var messages = _service.BuildWikiMessages(
            "你是一位技术文档专家",
            "## 代码上下文\n```csharp\nclass UserService {}\n```",
            "## 页面主题\n生成 UserService 的 Wiki 页面");

        Assert.Equal(3, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains("技术文档专家", messages[0].Text);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Contains("代码上下文", messages[1].Text);
        Assert.Equal(ChatRole.User, messages[2].Role);
        Assert.Contains("页面主题", messages[2].Text);
    }

    [Fact]
    public void BuildWikiMessages_SystemOnly_ShouldReturnSystemMessage()
    {
        var messages = _service.BuildWikiMessages("system prompt", "", "");

        Assert.Equal(1, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains("system prompt", messages[0].Text);
    }

    [Fact]
    public void BuildWikiMessages_EmptyAll_ShouldReturnFallback()
    {
        var messages = _service.BuildWikiMessages("", "", "");

        Assert.Single(messages);
        Assert.Equal(ChatRole.User, messages[0].Role);
        Assert.Contains("Wiki", messages[0].Text);
    }

    [Fact]
    public void BuildSlidesMessages_ShouldReturnStructuredMessages()
    {
        var messages = _service.BuildSlidesMessages(
            "Slide system prompt",
            "代码上下文",
            "幻灯片主题");

        Assert.Equal(3, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Equal(ChatRole.User, messages[2].Role);
    }

    [Fact]
    public void BuildWorkshopMessages_ShouldReturnStructuredMessages()
    {
        var messages = _service.BuildWorkshopMessages(
            "Workshop system prompt",
            "代码上下文",
            "训练营主题");

        Assert.Equal(3, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Equal(ChatRole.User, messages[2].Role);
    }

    [Fact]
    public void ParseRole_SystemAssistantTool_ShouldMapCorrectly()
    {
        Assert.Equal(ChatRole.System, ChatMessageBuilderService.ParseRole("system"));
        Assert.Equal(ChatRole.Assistant, ChatMessageBuilderService.ParseRole("assistant"));
        Assert.Equal(ChatRole.Tool, ChatMessageBuilderService.ParseRole("tool"));
        Assert.Equal(ChatRole.User, ChatMessageBuilderService.ParseRole("user"));
        Assert.Equal(ChatRole.User, ChatMessageBuilderService.ParseRole("unknown"));
        Assert.Equal(ChatRole.User, ChatMessageBuilderService.ParseRole(null));
    }

    [Fact]
    public void BuildChatMessages_WithHistoryAndSystemPrompt_ShouldPreserveOrder()
    {
        var history = new List<Heimdall.Infrastructure.Models.ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        var messages = _service.BuildChatMessages(history, "You are helpful", null);

        Assert.True(messages.Count >= 2);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains("helpful", messages[0].Text);
        Assert.Equal(ChatRole.User, messages[^1].Role);
    }

    [Fact]
    public void BuildAskMessages_ShouldIncludeSystemAndUser()
    {
        var knowledgeContext = new Heimdall.Core.Models.VersionedKnowledgeContext
        {
            Repository = new() { DisplayName = "TestRepo", RepoUrl = "https://github.com/test/repo" },
            EffectiveBranch = "main",
            EffectiveLanguage = "zh",
            RepositoryVersion = new() { Id = Guid.NewGuid(), CommitSha = "abc123" },
            WikiVersion = new() { Id = Guid.NewGuid(), VersionNo = 1 }
        };

        var messages = _service.BuildAskMessages(
            knowledgeContext,
            "这个项目怎么用？",
            null,
            false,
            "",
            "",
            []);

        Assert.True(messages.Count >= 2);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains("TestRepo", messages[0].Text);
        Assert.Contains("这个项目怎么用", messages[^1].Text);
    }
}
