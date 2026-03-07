# Gap Analysis: Current 3-Step vs Target 13-Step Pipeline

## Step Mapping

| Target Step | Current Coverage | Status |
|---|---|---|
| 01 Idea Generation | Not implemented | **NEW** |
| 02 Idea Evaluation & Scoring | Not implemented | **NEW** |
| 03 Market Viability Analysis | Not implemented | **NEW** |
| 04 Technical Feasibility | Not implemented | **NEW** |
| 05 Go/No-Go Decision Gate | Not implemented | **NEW** |
| 06 Product Planning & Requirements | Partially covered by `analyze-requirements` | **MODIFY** |
| 07 Architecture Design | Not implemented | **NEW** |
| 08 Backlog Generation | Partially covered by `generate-plan` | **MODIFY** |
| 09 Code Generation | Not implemented | **NEW** |
| 10 Automated Testing | Not implemented | **NEW** |
| 11 Code Review & Quality Gate | Not implemented | **NEW** |
| 12 Deployment Preparation | Not implemented | **NEW** |
| 13 Human Review & Approval | Covered by `human-review` / `ConsoleApprovalHandler` | **REUSE** |

## What's Reusable As-Is

### Orchestrator (entire project)
- `WorkflowEngine` — sequential stage execution, retry, human gates, persistence. No changes needed.
- `WorkflowDefinitionBuilder` — type-safe stage chaining. Works for 13 stages as-is.
- `IStageHandler` / `StageHandler<TInput, TOutput>` — handler contract and typed base class. All new steps implement this.
- `IRunStore` / `JsonFileRunStore` / `InMemoryRunStore` — persistence layer. No changes needed.
- All model types (`WorkflowRun`, `StageExecution`, `WorkflowDefinition`, `StageDefinition`, enums) — no changes needed.
- All existing tests — remain valid.

### Agents project
- `ILlmClient` / `ClaudeLlmClient` — reusable for all Claude-based steps (Opus, Sonnet, Haiku).
- `LlmRequest` / `LlmResponse` — reusable. `LlmRequest` already accepts a `Model` parameter.
- `StripMarkdownFences` utility pattern — duplicated in both handlers; could be extracted but not blocking.

### CLI project
- `ConsoleApprovalHandler` — maps directly to Step 13 (Human Review). Needs new input/output types but the pattern is reusable.
- `JsonFileRunStore` — no changes needed.
- `Program.cs` wiring pattern — same composition approach, just more handlers.

## What Needs Modification

### Existing Handlers → New Roles
1. **`AnalyzeRequirementsHandler`** — Currently takes `FeatureRequest` → `RequirementsDocument`. In the new pipeline, Step 06 (Product Planning) takes a richer input (approved idea + prior analysis) and produces a PRD. The existing handler's prompt and contract types are too narrow. **Decision: keep the existing code as-is for reference, build a new Step 06 handler with new contracts.**

2. **`GeneratePlanHandler`** — Currently takes `RequirementsDocument` → `ImplementationPlan`. Step 08 (Backlog Generation) takes PRD + architecture and produces epics/stories/tasks. Same situation: too narrow. **Decision: build new Step 08 handler with new contracts.**

3. **`ConsoleApprovalHandler`** — Currently takes `ImplementationPlan` → `ApprovalDecision`. Step 13 takes the full pipeline output and returns a richer decision (approve/reject/modify + feedback targeting specific steps). **Decision: modify input/output types or create a new handler.**

### ILlmClient — Multi-Model Support
- `ClaudeLlmClient` already accepts a model name per request — works for Opus/Sonnet/Haiku routing.
- Step 03 (Market Analysis) calls for **Gemini 3.1 Pro** with grounding. This requires a **new `ILlmClient` implementation** for the Google Gemini API.
- Step 10 (Testing log analysis) calls for **GPT-5 nano**. This requires a **new `ILlmClient` implementation** for the OpenAI API.
- **No changes to the `ILlmClient` interface itself** — the abstraction is sufficient. Just new implementations.

## Architectural Conflicts

| Area | Current | Target | Conflict? |
|---|---|---|---|
| Stage pipeline | Linear, sequential | Linear, sequential | **None** |
| State persistence | JSON files, persisted every transition | Durable store (SQL/Cosmos recommended) | **No conflict** — JSON files work for V1, can swap later via `IRunStore` |
| LLM abstraction | Single `ILlmClient` with model param | Multi-provider (Anthropic, Google, OpenAI) | **Minor** — need new implementations, not interface changes |
| Stage I/O | JSON strings, type-safe builder | Same pattern | **None** |
| Retry/resilience | Built-in retry in engine | Polly recommended | **No conflict** — engine retry works, Polly can wrap `ILlmClient` later |
| Human gates | `RequiresHumanInput` flag on stage | Same concept at Step 13 | **None** |
| DI | Manual wiring in Program.cs | Same approach works | **None** |

## Net-New Steps (Dependency Order)

The steps must be built in pipeline order since each step's output type becomes the next step's input type. However, they can be implemented in phases:

### Phase 1: Discovery (Steps 01-05)
All new contracts and handlers. No dependency on existing feature analysis code.

1. **Step 01 — Idea Generation**: `IdeationInput` → `IdeaBatch` (Claude Opus)
2. **Step 02 — Idea Evaluation**: `IdeaBatch` → `EvaluatedIdeas` (Claude Sonnet)
3. **Step 03 — Market Analysis**: `EvaluatedIdeas` → `MarketAnalysisReport` (Gemini — or Claude as fallback)
4. **Step 04 — Tech Feasibility**: `MarketAnalysisReport` → `TechFeasibilityReport` (Claude Opus)
5. **Step 05 — Go/No-Go**: `TechFeasibilityReport` → `GoNoGoDecision` (Claude Sonnet)

### Phase 2: Planning (Steps 06-08)
6. **Step 06 — Product Planning**: `GoNoGoDecision` → `ProductRequirementsDocument` (Claude Opus)
7. **Step 07 — Architecture Design**: `ProductRequirementsDocument` → `ArchitectureDocument` (Claude Opus)
8. **Step 08 — Backlog Generation**: `ArchitectureDocument` → `ProductBacklog` (Claude Sonnet)

### Phase 3: Build (Steps 09-11)
9. **Step 09 — Code Generation**: `ProductBacklog` → `GeneratedCodePackage` (Claude Sonnet + Opus)
10. **Step 10 — Automated Testing**: `GeneratedCodePackage` → `TestResults` (Claude Sonnet)
11. **Step 11 — Code Review**: `TestResults` → `CodeReviewReport` (Claude Opus)

### Phase 4: Ship (Steps 12-13)
12. **Step 12 — Deployment Prep**: `CodeReviewReport` → `DeploymentPackage` (Claude Sonnet)
13. **Step 13 — Human Review**: `DeploymentPackage` → `HumanApprovalDecision` (Console/No LLM)

## New LLM Client Implementations Needed

| Provider | Steps | Priority |
|---|---|---|
| Claude (existing) | 01, 02, 04, 05, 06, 07, 08, 09, 10, 11, 12 | Already done |
| Gemini 3.1 Pro | 03 (Market Analysis) | Can defer — use Claude as fallback |
| GPT-5 nano | 10 (Test log analysis) | Can defer — use Claude as fallback |

**Recommendation**: Build all 13 steps using Claude initially (Opus/Sonnet/Haiku as appropriate). Add Gemini and OpenAI clients as a follow-up once the pipeline is functional end-to-end.

## Summary

- **Orchestrator project**: No changes needed. It's ready for 13 stages.
- **Agents project**: 12 new handlers + ~25 new contract record types. 1-2 new `ILlmClient` implementations (deferred).
- **CLI project**: New workflow definition (13 stages), updated `Program.cs` wiring, possibly updated console approval handler.
- **Tests project**: Existing tests remain valid. New handler tests recommended but not blocking.
