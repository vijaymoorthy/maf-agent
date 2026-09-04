## Plan: Add Typed Tool Support

Extend the host-agnostic Core loop so a model response can request registered tools, the loop invokes them safely, and the next model turn receives structured tool results. Preserve the existing hard iteration cap and cumulative token/cost budget; expose model and tool usage separately while enforcing limits against their combined totals.

**Steps**
1. **Define the Core contracts and usage breakdown**
   - Extend `ModelRequest` with registered tool definitions and results from the preceding tool-execution phase.
   - Extend `ModelResponse` with zero or more typed-tool call records containing a tool name and serialized input, while retaining the existing completion/content fields.
   - Add a non-generic `IAgentTool` boundary suitable for the model-facing registry, plus typed implementation support (for example a generic/internal adapter or abstract base that validates input and produces a typed output before serializing it).
   - Add explicit tool definition, call, and result records. Results must carry success/failure, serialized output or error text, and tool usage.
   - Change the usage record to expose separate model usage and tool usage fields, with derived combined totals used for budget checks while retaining the breakdown for observability.
   - Add focused contract/accounting tests proving model and tool usage remain separately inspectable and combined totals are calculated correctly; run the focused tests and Release build before moving on.
2. **Add registration and validation**
   - Add a Core registry/registration abstraction that stores tools by unique name and associates each registration with a positive per-tool timeout.
   - Reject null tools, blank/duplicate names, invalid timeout values, and malformed registration at construction/registration time.
   - Expose only tool metadata to model requests; keep executable tool instances behind the registry.
   - Add registry tests for successful registration, metadata projection, duplicate names, and invalid inputs; run those tests and the Release build before moving on.
3. **Integrate invocation into `AgentLoop`**
   - Pass available tool definitions on every model request and pass the prior turn’s tool results on the following request.
   - When a response contains tool calls, resolve each name through the registry and invoke calls sequentially under that tool’s timeout, using linked cancellation so caller cancellation still wins.
   - Isolate failures: unknown tool names, input validation errors, thrown exceptions, and timeout cancellation become failed `ToolResult` values and do not escape the loop or prevent other requested tools from running.
   - Treat tool calls as an intermediate turn: return to the model with the collected results. Process calls before allowing completion when a response is marked complete with calls, so requested work is not silently skipped.
   - Add tool usage to the separate tool-usage field and check the combined model/tool usage against each configured budget. Do not execute further tool calls after the budget is exhausted; return the latest response/result state consistently.
   - Keep the existing `MaxIterations` `for` cap, request remaining-budget calculations, caller cancellation flow, and host-agnostic Core boundary intact.
   - Add loop tests for successful invocation, result propagation, multiple calls, exception isolation, timeout enforcement, cancellation, and combined-budget stopping; run the focused suite and Release build before moving on.
4. **Provide one worked example tool**
   - Add a small Core tool with a typed input/output implementation, such as `GetCurrentTimeTool`, with explicit validation, UTC ISO-8601 output, and no host or external-service dependency.
   - Keep the example reusable from any host and make its registration/invocation path apparent through the public Core API. Do not wire console-specific behavior into Core; only update the console host if needed to demonstrate construction without introducing service calls or secrets.
   - Add example-tool tests for valid and invalid typed input plus output shape; run the focused tests and Release build so this step is independently committable.

**Relevant files**
- `c:\AllVijayData\Repos\maf-agent\src\MafAgent.Core\AgentLoop.cs` — existing loop, request/response contracts, options, usage accounting, and stop reasons; primary integration surface.
- `c:\AllVijayData\Repos\maf-agent\src\MafAgent.Core\MafAgent.Core.csproj` — only if new Core source organization or project settings require it; retain .NET 9, nullable, and implicit usings.
- `c:\AllVijayData\Repos\maf-agent\src\MafAgent.Core\Tools\...` — recommended location for the tool contract, registry, execution records, and worked example, keeping the loop file focused.
- `c:\AllVijayData\Repos\maf-agent\tests\MafAgent.Tests\AgentLoopTests.cs` — existing sequence model fake and loop behavior tests; extend with focused tool tests or add a nearby `ToolTests.cs`.
- `c:\AllVijayData\Repos\maf-agent\src\MafAgent.Console\Program.cs` — optional minimal host wiring only if the worked example needs an end-to-end construction sample; do not add console concerns to Core.
- `c:\AllVijayData\Repos\maf-agent\.github\copilot-instructions.md` and `c:\AllVijayData\Repos\maf-agent\.github\instructions\maf-agent.instructions.md` — governing constraints: explicit contracts, no raw tool exceptions, no external service calls in tests, warnings as errors, and Release validation.

**Verification**
1. Run the focused tool/loop xUnit tests while iterating, including a timeout test using a short registration timeout and a cancellation-aware delayed fake.
2. Run `dotnet build --configuration Release` from `c:\AllVijayData\Repos\maf-agent`.
3. Run `dotnet test --configuration Release --no-build` from the repository root.
4. Manually inspect that Core contains no `Console.*`, tool calls cannot bypass the iteration cap, tool usage affects remaining budgets, and no package references were added.

**Decisions**
- Model-facing tool API: non-generic base contract with typed concrete implementations/adapters, as selected by the user.
- Model usage and tool usage are stored as separate fields on the usage record, while budget checks use derived combined token and cost totals, as selected by the user.
- Registration owns per-tool timeout metadata; one global timeout is insufficient for the requested per-tool behavior.
- Tool failures are data returned to the model, not loop-level exceptions; failed results plus another bounded model turn preserve the current public result contract.
- No external service calls or new NuGet packages; the worked example is deterministic/local and tests use fakes.

**Further Considerations**
1. Confirm whether the model client should receive inputs/outputs as `JsonElement`/JSON strings or a repository-specific serializer. Recommendation: use `JsonElement` at the non-generic boundary to avoid adding a package and keep typed conversion inside concrete tools.
2. Keep `AgentLoopStopReason` unchanged unless product requirements demand a distinct terminal tool-failure reason; failed results plus another bounded model turn preserve the current public result contract.
