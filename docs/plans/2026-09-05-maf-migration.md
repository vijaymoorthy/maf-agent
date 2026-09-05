# Migration Plan: Adopt MAF Agent and Tool Invocation

The current Core implementation hand-rolls both model orchestration and tool registration. Replace those responsibilities with Microsoft Agent Framework while retaining the repository's application-specific `AgentUsage` accounting and token/cost policy.

## Target architecture

```text
provider-specific IChatClient
  -> chat-client middleware
  -> FunctionInvokingChatClient
  -> ChatClientAgent
  -> agent and function invocation middleware
```

MAF owns model interaction, tool schemas, argument serialization, tool dispatch, tool-result messages, and the repeated model/tool-call loop. `MafAgent.Core` owns typed domain tools, usage accounting, pricing, budget policy, timeout policy, and host-agnostic composition. `MafAgent.Console` owns provider configuration and console I/O.

## Delete the hand-rolled orchestration

Delete these types from `src/MafAgent.Core/AgentLoop.cs`:

- `AgentLoop`
- `AgentLoopOptions`
- `IAgentModelClient`
- `ModelRequest`
- `ModelResponse`
- `AgentLoopResult`
- `AgentLoopStopReason`

`ChatClientAgent` becomes the agent entry point, `FunctionInvokingChatClient` owns the model/tool-call loop, and `AgentResponse` replaces the custom loop result. Do not retain a compatibility loop around the MAF agent.

Move the application accounting types into a focused file such as `src/MafAgent.Core/AgentUsage.cs` and retain:

- `ModelUsage`
- `ToolUsage`
- `AgentUsage`
- `AddModel`
- `AddTool`
- combined token and cost properties

These types remain because MAF does not know this application's cost model or reporting requirements.

## Delete the custom registry and tool contracts

Delete `src/MafAgent.Core/ToolRegistry.cs`, including:

- `ToolRegistration`
- `ToolRegistry`
- custom tool-definition projection
- name lookup and duplicate-registration handling

Delete the custom call/result/schema abstractions from `src/MafAgent.Core/ToolContracts.cs`:

- `ToolDefinition`
- `ToolCall`
- `ToolResult`
- `IAgentTool`
- `TypedAgentTool<TInput, TOutput>`

Replace them with ordinary typed Core methods. Domain tools should expose explicit input and output types and accept a `CancellationToken`. `AIFunctionFactory` generates the model-facing function metadata and JSON schema from those methods.

If per-tool timeouts and failure conversion are still required, implement them in a small function wrapper or function middleware. Do not reintroduce a registry solely to hold timeout metadata.

## Package changes

Add consistent, pinned versions of:

- `Microsoft.Agents.AI` for `ChatClientAgent`, `AIAgent`, sessions, and agent middleware.
- `Microsoft.Extensions.AI` for `IChatClient`, `AIFunctionFactory`, `FunctionInvokingChatClient`, and function/chat middleware.

The provider-specific package belongs in `MafAgent.Console` and should be selected explicitly when the model provider is chosen. Do not add an Azure or OpenAI provider implicitly to Core. Tests should use a fake `IChatClient` and must not call an external service.

## Compose the MAF agent

Build the Core composition around this sequence:

1. Receive an `IChatClient` from the host.
2. Add chat-client middleware around it.
3. Wrap it with `FunctionInvokingChatClient`.
4. Convert typed domain methods to functions with `AIFunctionFactory.Create`.
5. Construct `ChatClientAgent` with those functions.
6. Attach budget and function middleware through the agent builder.
7. Invoke `RunAsync` with the caller's `CancellationToken`.

MAF then handles the full interaction: initial model request, tool calls, typed function invocation, tool-result messages, follow-up model requests, and final response generation.

## Preserve budget enforcement as invocation middleware

Create a Core policy component such as `AgentBudgetMiddleware` with invocation-scoped state containing:

- current model-call/iteration count
- `AgentUsage`
- maximum iterations
- maximum total tokens
- maximum total cost
- a linked cancellation source
- the termination reason

### Agent run middleware

Use agent run middleware to create the invocation state and linked cancellation token, invoke the inner agent, and map policy termination to the application's result/reporting model.

Caller cancellation must remain distinguishable from budget termination. Normal caller cancellation should propagate as cancellation rather than being reported as a budget failure.

### Chat-client middleware

Use chat-client middleware to observe every model request, including requests made after tool results:

- increment the iteration/model-call count
- read token usage from `ChatResponse.Usage`
- add model usage to `AgentUsage.Model`
- calculate estimated cost through an injected pricing calculator
- prevent another model call once the aggregate budget is exhausted

This layer is necessary because agent run middleware sees the overall invocation, not each inference request.

### Function invocation middleware

Use function middleware to:

- check the aggregate budget before invoking a tool
- apply a per-tool timeout with a linked token
- record duration and tool usage
- convert tool validation failures, exceptions, and timeouts into model-visible failures
- set `FunctionInvocationContext.Terminate = true` when the iteration or budget policy requires stopping
- cancel the linked invocation token when remaining work must stop

`Terminate` is the MAF mechanism that prevents the function-call loop from issuing the follow-up model request after tool invocation. The hard cap must therefore be enforced at this boundary rather than by restoring a manual outer loop.

Tool token usage remains zero unless a tool has a separately billable token source. Tool cost and latency should be reported independently from model usage.

## Console host wiring

Replace the placeholder in `src/MafAgent.Console/Program.cs` with composition only:

1. Construct the provider-specific `IChatClient` from configuration.
2. Apply chat middleware.
3. Create the `FunctionInvokingChatClient`.
4. Create typed functions with `AIFunctionFactory`.
5. Construct and decorate `ChatClientAgent`.
6. Invoke it with a `CancellationToken`.

Keep endpoints, model names, credentials, and pricing configuration out of source. Core must remain free of `Console.*` calls.

## Test migration

Delete or rewrite:

- `tests/MafAgent.Tests/AgentLoopTests.cs`
- `tests/MafAgent.Tests/ToolRegistryTests.cs`

Keep and extend `tests/MafAgent.Tests/AgentUsageTests.cs`.

Add focused tests using a fake `IChatClient` for:

- generated function name, description, and typed schema
- valid and invalid typed arguments
- controlled tool failures rather than raw exceptions
- timeout and cancellation propagation
- multiple tool calls and tool-result follow-up messages
- model usage accumulation per model request
- independent tool usage accumulation
- combined token and cost budget termination
- hard iteration termination
- `FunctionInvocationContext.Terminate` preventing another inference request
- caller cancellation remaining distinct from budget termination
- no external service calls

## Documentation and validation

Update `README.md` to describe `ChatClientAgent`, `AIFunctionFactory`, and the budget middleware. Mark the earlier hand-rolled plan as superseded, retaining it only as historical context.

After implementation, run the focused tests while iterating, then run:

```text
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Also verify that Core contains no console concerns, no unbounded model-call loop remains, aggregate model/tool usage controls termination, and tests use only fakes.