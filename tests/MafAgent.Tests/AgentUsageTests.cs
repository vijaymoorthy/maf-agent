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
}