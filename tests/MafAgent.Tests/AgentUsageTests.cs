using MafAgent.Core;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace MafAgent.Tests;

public class AgentUsageTests
{
    [Fact]
    public void AddModelAndTool_ExposesBreakdownAndCombinedTotals()
    {
        var usage = AgentUsage.None
            .AddModel(new ModelUsage(10, 5, 0.01m))
            .AddTool(new ToolUsage(2, 3, 0.02m));

        Assert.Equal(new ModelUsage(10, 5, 0.01m), usage.Model);
        Assert.Equal(new ToolUsage(2, 3, 0.02m), usage.Tools);
        Assert.Equal(20, usage.CombinedTotalTokens);
        Assert.Equal(0.03m, usage.CombinedEstimatedCostUsd);
    }

    [Fact]
    public async Task OrderStatusTool_ReturnsStatusForValidOrder()
    {
        var tool = new OrderStatusTool();

        var result = await tool.ExecuteAsync(new OrderStatusRequest { OrderId = "ORD-123" }, CancellationToken.None);

        Assert.Equal(new OrderStatusResult("ORD-123", "Processing"), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OrderStatusTool_RejectsBlankOrderId(string orderId)
    {
        var tool = new OrderStatusTool();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tool.ExecuteAsync(new OrderStatusRequest { OrderId = orderId }, CancellationToken.None));
    }

    [Fact]
    public void AIFunctionFactory_Create_DescribesOrderStatusTool()
    {
        var function = AIFunctionFactory.Create(new OrderStatusTool().ExecuteAsync);
        var schema = function.JsonSchema;

        Assert.Equal("Execute", function.Name);
        Assert.Equal("Retrieves the current fulfillment status for a customer order.", function.Description);
        Assert.Equal("object", schema.GetProperty("type").GetString());
        var inputSchema = schema.GetProperty("properties").GetProperty("input");
        Assert.Equal("object", inputSchema.GetProperty("type").GetString());
        Assert.Equal("string", inputSchema.GetProperty("properties").GetProperty("orderId").GetProperty("type").GetString());
    }

    [Fact]
    public async Task AgentComposition_InvokesToolAndSendsResultBackToModel()
    {
        var chatClient = new SequenceChatClient();
        var agent = AgentComposition.Create(chatClient);
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync("What is the status of ORD-123?", session, null, CancellationToken.None);

        Assert.Equal("ORD-123 is Processing.", response.Text);
        Assert.Equal(2, chatClient.Requests.Count);
        Assert.Contains(
            chatClient.Requests[1].SelectMany(message => message.Contents),
            content => content is FunctionResultContent result &&
                result.Result?.ToString()?.Contains("ORD-123", StringComparison.Ordinal) == true);
    }

    private sealed class SequenceChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public ChatClientMetadata Metadata => new("fake");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messages = chatMessages.ToArray();
            Requests.Add(messages);

            if (Requests.Count == 1)
            {
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [new FunctionCallContent(
                            "call-1",
                            "Execute",
                            new Dictionary<string, object?>
                            {
                                ["input"] = new Dictionary<string, object?>
                                {
                                    ["orderId"] = "ORD-123"
                                }
                            })])));
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ORD-123 is Processing.")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, (await GetResponseAsync(chatMessages, options, cancellationToken)).Text);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}