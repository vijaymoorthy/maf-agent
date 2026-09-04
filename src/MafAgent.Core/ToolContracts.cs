using System.Text.Json;

namespace MafAgent.Core;

public sealed record ToolDefinition(string Name, string Description, JsonElement InputSchema);

public sealed record ToolCall(string Id, string Name, JsonElement Input);

public sealed record ToolResult(
    string CallId,
    string Name,
    bool Succeeded,
    JsonElement? Output,
    string? Error,
    ToolUsage Usage);

public interface IAgentTool
{
    string Name { get; }

    string Description { get; }

    JsonElement InputSchema { get; }

    Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken);
}

public abstract class TypedAgentTool<TInput, TOutput> : IAgentTool
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public abstract string Name { get; }

    public abstract string Description { get; }

    public abstract JsonElement InputSchema { get; }

    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);

        var input = call.Input.Deserialize<TInput>(SerializerOptions)
            ?? throw new JsonException($"Tool '{Name}' input must not be null.");
        ValidateInput(input);
        var output = await ExecuteTypedAsync(input, cancellationToken).ConfigureAwait(false);

        return new ToolResult(
            call.Id,
            Name,
            true,
            JsonSerializer.SerializeToElement(output, SerializerOptions),
            null,
            ToolUsage.None);
    }

    protected virtual void ValidateInput(TInput input)
    {
    }

    protected abstract Task<TOutput> ExecuteTypedAsync(TInput input, CancellationToken cancellationToken);
}