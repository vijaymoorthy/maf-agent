using System.Text.Json;
using MafAgent.Core;

namespace MafAgent.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Constructor_RegistersToolsAndProjectsDefinitions()
    {
        var tool = new TestTool("lookup", "Looks up a value.");
        var registry = new ToolRegistry([new ToolRegistration(tool, TimeSpan.FromSeconds(5))]);

        var definition = Assert.Single(registry.Definitions);
        Assert.Equal("lookup", definition.Name);
        Assert.Equal("Looks up a value.", definition.Description);
        Assert.True(registry.TryGetRegistration("lookup", out var registration));
        Assert.Same(tool, registration.Tool);
        Assert.Equal(TimeSpan.FromSeconds(5), registration.Timeout);
    }

    [Fact]
    public void Register_RejectsDuplicateToolNames()
    {
        var registry = new ToolRegistry([]);
        registry.Register(new ToolRegistration(new TestTool("lookup", "First."), TimeSpan.FromSeconds(1)));

        Assert.Throws<ArgumentException>(() =>
            registry.Register(new ToolRegistration(new TestTool("lookup", "Second."), TimeSpan.FromSeconds(1))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registration_RejectsNonPositiveTimeout(int timeoutSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ToolRegistration(new TestTool("lookup", "Looks up a value."), TimeSpan.FromSeconds(timeoutSeconds)));
    }

    [Fact]
    public void Registration_RejectsNullTool()
    {
        Assert.Throws<ArgumentNullException>(() => new ToolRegistration(null!, TimeSpan.FromSeconds(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registration_RejectsBlankToolName(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            new ToolRegistration(new TestTool(name, "Looks up a value."), TimeSpan.FromSeconds(1)));
    }

    private sealed class TestTool(string name, string description) : IAgentTool
    {
        public string Name { get; } = name;

        public string Description { get; } = description;

        public JsonElement InputSchema { get; } = JsonSerializer.SerializeToElement(new { type = "object" });

        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken) =>
            Task.FromResult(new ToolResult(call.Id, Name, true, null, null, ToolUsage.None));
    }
    }