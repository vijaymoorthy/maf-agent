using System.ComponentModel;

namespace MafAgent.Core;

public sealed record OrderStatusRequest
{
    [Description("The customer order identifier to look up.")]
    public string OrderId { get; init; } = string.Empty;
}

public sealed record OrderStatusResult(string OrderId, string Status);

public sealed class OrderStatusTool
{
    [Description("Retrieves the current fulfillment status for a customer order.")]
    public Task<OrderStatusResult> ExecuteAsync(
        OrderStatusRequest input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OrderId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new OrderStatusResult(input.OrderId, "Processing"));
    }
}