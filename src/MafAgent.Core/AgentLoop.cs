namespace MafAgent.Core;

public sealed class AgentLoop
{
    private readonly IAgentModelClient _modelClient;
    private readonly AgentLoopOptions _options;

    public AgentLoop(IAgentModelClient modelClient, AgentLoopOptions options)
    {
        _modelClient = modelClient ?? throw new ArgumentNullException(nameof(modelClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<AgentLoopResult> RunAsync(string task, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);

        var totalUsage = ModelUsage.None;
        string? previousResponse = null;

        for (var iteration = 1; iteration <= _options.MaxIterations; iteration++)
        {
            var request = new ModelRequest(
                task,
                previousResponse,
                iteration,
                _options.MaxTotalTokens - totalUsage.TotalTokens,
                _options.MaxTotalCostUsd - totalUsage.EstimatedCostUsd);

            var response = await _modelClient.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            totalUsage = totalUsage.Add(response.Usage);
            previousResponse = response.Content;

            if (response.IsTaskComplete)
            {
                return new AgentLoopResult(response.Content, iteration, totalUsage, AgentLoopStopReason.TaskComplete);
            }

            if (totalUsage.TotalTokens >= _options.MaxTotalTokens ||
                totalUsage.EstimatedCostUsd >= _options.MaxTotalCostUsd)
            {
                return new AgentLoopResult(response.Content, iteration, totalUsage, AgentLoopStopReason.BudgetExceeded);
            }
        }

        return new AgentLoopResult(previousResponse, _options.MaxIterations, totalUsage, AgentLoopStopReason.IterationLimitReached);
    }
}

public sealed class AgentLoopOptions
{
    public int MaxIterations { get; init; } = 10;

    public long MaxTotalTokens { get; init; } = 10_000;

    public decimal MaxTotalCostUsd { get; init; } = 1.00m;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxTotalTokens, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaxTotalCostUsd, 0m);
    }
}

public interface IAgentModelClient
{
    Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken);
}

public sealed record ModelRequest(
    string Task,
    string? PreviousResponse,
    int Iteration,
    long RemainingTokenBudget,
    decimal RemainingCostBudgetUsd);

public sealed record ModelResponse(string Content, bool IsTaskComplete, ModelUsage Usage);

public sealed record ModelUsage(long InputTokens, long OutputTokens, decimal EstimatedCostUsd)
{
    public static ModelUsage None { get; } = new(0, 0, 0m);

    public long TotalTokens => InputTokens + OutputTokens;

    public ModelUsage Add(ModelUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new ModelUsage(
            checked(InputTokens + other.InputTokens),
            checked(OutputTokens + other.OutputTokens),
            EstimatedCostUsd + other.EstimatedCostUsd);
    }
}

public sealed record AgentLoopResult(
    string? LastResponse,
    int Iterations,
    ModelUsage Usage,
    AgentLoopStopReason StopReason);

public enum AgentLoopStopReason
{
    TaskComplete,
    IterationLimitReached,
    BudgetExceeded
}