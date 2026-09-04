# MafAgent — purpose and conventions

## What this repo is for
A Microsoft Agent Framework (MAF) agent in C#, built as the reference implementation for my
AI engineering track. It will later be ported to Python as a learning exercise, so favour
patterns that translate: explicit contracts, no clever C#-only idioms in the agent core.

## Agent design rules
- Agent logic lives in `MafAgent.Core` and must be host-agnostic — no `Console.*` in Core.
- Every tool is a small, single-purpose class with an explicit input and output type.
  Validate inputs; never let a tool throw raw exceptions to the agent loop.
- The agent loop must have a **hard iteration cap** and a token/cost budget. Never write an
  unbounded `while` loop around a model call.
- Prefer **structured output** (typed objects) over free-text parsing.
- Model and endpoint configuration comes from `IConfiguration`/typed options — never hard-coded,
  never a secret in source. Azure AI Foundry endpoints and keys come from environment or Key Vault.
- Log token usage and latency per model call; cost observability is a first-class requirement,
  not an afterthought.

## Constraints
- Do not add NuGet packages without telling me which and why.
- No external service calls in unit tests — fake the model client.