using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MafAgent.Core;

public static class AgentComposition
{
    public static ChatClientAgent Create(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        var function = AIFunctionFactory.Create(new OrderStatusTool().ExecuteAsync);
        var invokingClient = new FunctionInvokingChatClient(chatClient, null, null)
        {
            AdditionalTools = [function]
        };

        return new ChatClientAgent(
            invokingClient,
            new ChatClientAgentOptions
            {
                Name = "MafAgent",
                Description = "An order status agent.",
                UseProvidedChatClientAsIs = true
            });
    }
}