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

        var totalUsage = AgentUsage.None;
        string? previousResponse = null;

        for (var iteration = 1; iteration <= _options.MaxIterations; iteration++)
        {
            var request = new ModelRequest(
                task,
                previousResponse,
                iteration,
                _options.MaxTotalTokens - totalUsage.CombinedTotalTokens,
                _options.MaxTotalCostUsd - totalUsage.CombinedEstimatedCostUsd);

            var response = await _modelClient.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            totalUsage = totalUsage.AddModel(response.Usage);
            previousResponse = response.Content;

            if (response.IsTaskComplete)
            {
                return new AgentLoopResult(response.Content, iteration, totalUsage, AgentLoopStopReason.TaskComplete);
            }

            if (totalUsage.CombinedTotalTokens >= _options.MaxTotalTokens ||
                totalUsage.CombinedEstimatedCostUsd >= _options.MaxTotalCostUsd)
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
    decimal RemainingCostBudgetUsd,
    IReadOnlyList<ToolDefinition>? AvailableTools = null,
    IReadOnlyList<ToolResult>? ToolResults = null);

public sealed record ModelResponse(
    string Content,
    bool IsTaskComplete,
    ModelUsage Usage,
    IReadOnlyList<ToolCall>? ToolCalls = null);

public sealed record ModelUsage(long InputTokens, long OutputTokens, decimal EstimatedCostUsd)
{
    public static ModelUsage None { get; } = new(0, 0, 0m);

    public long TotalTokens => checked(InputTokens + OutputTokens);

    public ModelUsage Add(ModelUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new ModelUsage(
            checked(InputTokens + other.InputTokens),
            checked(OutputTokens + other.OutputTokens),
            EstimatedCostUsd + other.EstimatedCostUsd);
    }
}

public sealed record ToolUsage(long InputTokens, long OutputTokens, decimal EstimatedCostUsd)
{
    public static ToolUsage None { get; } = new(0, 0, 0m);

    public long TotalTokens => checked(InputTokens + OutputTokens);
}

public sealed record AgentUsage(ModelUsage Model, ToolUsage Tools)
{
    public static AgentUsage None { get; } = new(ModelUsage.None, ToolUsage.None);

    public long CombinedTotalTokens => checked(Model.TotalTokens + Tools.TotalTokens);

    public decimal CombinedEstimatedCostUsd => Model.EstimatedCostUsd + Tools.EstimatedCostUsd;

    public AgentUsage AddModel(ModelUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        return this with { Model = Model.Add(usage) };
    }

    public AgentUsage AddTool(ToolUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        return this with
        {
            Tools = new ToolUsage(
                checked(Tools.InputTokens + usage.InputTokens),
                checked(Tools.OutputTokens + usage.OutputTokens),
                Tools.EstimatedCostUsd + usage.EstimatedCostUsd)
        };
    }
}

public sealed record AgentLoopResult(
    string? LastResponse,
    int Iterations,
    AgentUsage Usage,
    AgentLoopStopReason StopReason);

public enum AgentLoopStopReason
{
    TaskComplete,
    IterationLimitReached,
    BudgetExceeded
}