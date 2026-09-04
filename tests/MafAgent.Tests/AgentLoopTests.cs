using MafAgent.Core;

namespace MafAgent.Tests;

public class AgentLoopTests
{
    [Fact]
    public async Task RunAsync_ContinuesUntilTheModelCompletesTheTask()
    {
        var modelClient = new SequenceModelClient(
            new ModelResponse("Working on it.", false, new ModelUsage(10, 5, 0.01m)),
            new ModelResponse("Task complete.", true, new ModelUsage(8, 4, 0.01m)));
        var loop = new AgentLoop(modelClient, new AgentLoopOptions());

        var result = await loop.RunAsync("Complete this task.");

        Assert.Equal(AgentLoopStopReason.TaskComplete, result.StopReason);
        Assert.Equal(2, result.Iterations);
        Assert.Equal("Task complete.", result.LastResponse);
        Assert.Equal(27, result.Usage.CombinedTotalTokens);
        Assert.Equal(0.02m, result.Usage.CombinedEstimatedCostUsd);
        Assert.Equal("Working on it.", modelClient.Requests[1].PreviousResponse);
    }

    [Fact]
    public async Task RunAsync_StopsWhenTheIterationLimitIsReached()
    {
        var modelClient = new SequenceModelClient(
            new ModelResponse("Still working.", false, new ModelUsage(1, 1, 0.01m)),
            new ModelResponse("Still working.", false, new ModelUsage(1, 1, 0.01m)));
        var loop = new AgentLoop(modelClient, new AgentLoopOptions { MaxIterations = 2 });

        var result = await loop.RunAsync("Continue working.");

        Assert.Equal(AgentLoopStopReason.IterationLimitReached, result.StopReason);
        Assert.Equal(2, result.Iterations);
        Assert.Equal(2, modelClient.Requests.Count);
    }

    [Fact]
    public async Task RunAsync_StopsWhenTheTokenBudgetIsExhausted()
    {
        var modelClient = new SequenceModelClient(
            new ModelResponse("Still working.", false, new ModelUsage(4, 6, 0.01m)),
            new ModelResponse("Should not be called.", true, new ModelUsage(1, 1, 0.01m)));
        var loop = new AgentLoop(modelClient, new AgentLoopOptions { MaxTotalTokens = 10 });

        var result = await loop.RunAsync("Stay within budget.");

        Assert.Equal(AgentLoopStopReason.BudgetExceeded, result.StopReason);
        Assert.Equal(1, result.Iterations);
        Assert.Single(modelClient.Requests);
    }

    private sealed class SequenceModelClient(params ModelResponse[] responses) : IAgentModelClient
    {
        private readonly Queue<ModelResponse> _responses = new(responses);

        public List<ModelRequest> Requests { get; } = [];

        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}