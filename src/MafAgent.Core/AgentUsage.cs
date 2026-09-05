namespace MafAgent.Core;

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