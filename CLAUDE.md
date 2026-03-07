# Project: AI Product Development Pipeline

## Vision
A 13-step AI-orchestrated product development workflow that takes ideas from generation through deployment, with human review as the final gate.

## Current State
The codebase implements a 3-step "Feature Analysis" workflow:
1. **analyze-requirements** — LLM extracts structured requirements from a feature description
2. **generate-plan** — LLM produces an implementation plan from requirements
3. **human-review** — Console-based human approval gate

Architecture is split across three projects:
- **Orchestrator** — Domain model, workflow engine, handler contracts, persistence. Zero external deps.
- **Agents** — `ILlmClient` abstraction, `ClaudeLlmClient` (raw HttpClient to Anthropic API), LLM-backed stage handlers, contract record types.
- **Cli** — Composition root (`Program.cs`), `JsonFileRunStore`, `ConsoleApprovalHandler`, workflow definition assembly.

The engine supports sequential stage execution, configurable retry with delay, human gates (pause/resume), and durable state persistence via `IRunStore` (saved after every transition).

## Target State
13-step pipeline: Ideation → Evaluation → Market Analysis → Tech Feasibility → Go/No-Go → Product Planning → Architecture → Backlog → Code Generation → Testing → Code Review → Deployment → Human Review

See `docs/workflow-architecture.md` for full specification.
See `docs/gap-analysis.md` for current-vs-target comparison.

## Architecture Decisions
- Preserve the existing Orchestrator project unchanged — it's ready for 13 stages
- Preserve `IStageHandler` / `StageHandler<TInput, TOutput>` pattern for all new handlers
- Preserve `WorkflowEngine` sequential execution with retry and human gates
- Preserve `IRunStore` / `JsonFileRunStore` persistence approach
- New steps follow the same patterns: handler class + contract records + LLM prompt
- LLM abstraction: `ILlmClient` interface exists; `ClaudeLlmClient` handles all Claude models (Opus/Sonnet/Haiku via model param). Gemini and OpenAI clients deferred until pipeline works end-to-end with Claude only.
- State management: mutable `WorkflowRun` with JSON-serialized stage I/O, persisted after every transition
- Each step implements `StageHandler<TInput, TOutput>` (or `IStageHandler` for non-LLM steps)

## Implementation Order
1. Define all 13 contract record types (input/output for each step)
2. Build Discovery phase handlers (Steps 01-05)
3. Build Planning phase handlers (Steps 06-08)
4. Build Build phase handlers (Steps 09-11)
5. Build Ship phase handlers (Steps 12-13)
6. Assemble new workflow definition (13 stages)
7. Update Program.cs wiring
8. Add Gemini/OpenAI LLM clients (post-MVP)

## LLM Model Mapping

| Step | Model | Why |
|------|-------|-----|
| 01 Idea Generation | Claude Opus 4.6 | Creative divergent reasoning |
| 02 Idea Evaluation | Claude Sonnet 4.6 | Structured analytical scoring |
| 03 Market Analysis | Claude Sonnet 4.6 (Gemini deferred) | Gemini grounding ideal but Claude works initially |
| 04 Tech Feasibility | Claude Opus 4.6 | Deep .NET ecosystem knowledge |
| 05 Go/No-Go | Claude Sonnet 4.6 | Synthesis/summarization |
| 06 Product Planning | Claude Opus 4.6 | Creative + rigorous PRD |
| 07 Architecture Design | Claude Opus 4.6 | Strongest architectural reasoning |
| 08 Backlog Generation | Claude Sonnet 4.6 | High-volume structured decomposition |
| 09 Code Generation | Claude Sonnet 4.6 | Cost-effective code gen |
| 10 Testing | Claude Sonnet 4.6 | Test generation |
| 11 Code Review | Claude Opus 4.6 | Deepest pattern understanding |
| 12 Deployment Prep | Claude Sonnet 4.6 | Template-driven structured output |
| 13 Human Review | No LLM (console I/O) | Human-in-the-loop |

## Conventions
- **Projects**: Orchestrator (zero deps), Agents (LLM + handlers), Cli (composition root)
- **Naming**: Records for contracts/DTOs, classes for stateful objects and handlers
- **Handlers**: `StageHandler<TInput, TOutput>` base class for typed handlers; `IStageHandler` for engine contract
- **Contracts**: Immutable C# records in `Agents/[WorkflowName]/Contracts/` folder
- **Handlers**: One handler class per stage in `Agents/[WorkflowName]/` folder
- **Workflow definitions**: Static factory in `Cli/Workflows/` folder
- **JSON serialization**: `System.Text.Json` with `JsonNamingPolicy.CamelCase` everywhere
- **LLM prompts**: Inline `const string SystemPrompt` in handler class; instruct model to return raw JSON
- **Logging**: `ILogger` with message templates (not interpolation)
- **Retry**: Configured per-stage via `StageDefinition.MaxAttempts` and `RetryDelaySeconds`
- **Human gates**: `StageDefinition.RequiresHumanInput = true`
- **State**: Mutable `WorkflowRun`/`StageExecution`, immutable `WorkflowDefinition`/`StageDefinition`
- **Tests**: xUnit, test project per source project, stubs over mocks
- **Target framework**: .NET 8
- **Default model**: `claude-sonnet-4-6` (configurable via `ANTHROPIC_MODEL` env var)
