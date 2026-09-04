using MafAgent.Core;

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
}