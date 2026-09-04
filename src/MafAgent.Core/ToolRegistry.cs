using System.Diagnostics.CodeAnalysis;

namespace MafAgent.Core;

public sealed class ToolRegistration
{
    public ToolRegistration(IAgentTool tool, TimeSpan timeout)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(tool.Name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        Timeout = timeout;
    }

    public IAgentTool Tool { get; }

    public TimeSpan Timeout { get; }

    public ToolDefinition Definition => new(Tool.Name, Tool.Description, Tool.InputSchema);
}

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ToolRegistration> _registrations = new(StringComparer.Ordinal);

    public ToolRegistry(IEnumerable<ToolRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (var registration in registrations)
        {
            Register(registration);
        }
    }

    public IReadOnlyList<ToolDefinition> Definitions =>
        _registrations.Values.Select(registration => registration.Definition).ToArray();

    public void Register(ToolRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!_registrations.TryAdd(registration.Tool.Name, registration))
        {
            throw new ArgumentException($"A tool named '{registration.Tool.Name}' is already registered.", nameof(registration));
        }
    }

    public bool TryGetRegistration(string name, [NotNullWhen(true)] out ToolRegistration? registration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _registrations.TryGetValue(name, out registration);
    }
}