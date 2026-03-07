**AI Product Development**

**Workflow Architecture**

A 13-Step AI-Orchestrated Pipeline

From Idea Generation to Human-Approved Deployment

Built for .NET • Multi-LLM Strategy • Cost-Optimized

March 2026

**Executive Summary**

This document defines a complete AI-driven product development workflow
implemented in .NET. The pipeline takes a product concept from initial
AI-generated ideation through market analysis, technical planning,
architecture design, code generation, testing, and deployment ---
culminating in a human review gate before anything goes live.

The workflow is designed around three core principles. First, use the
right LLM for the right job: creative tasks use a frontier model (Claude
Opus 4.6), structured analytical tasks use a mid-tier model (Claude
Sonnet 4.6), market research leverages grounded search (Gemini 3.1 Pro),
and high-volume log analysis uses the cheapest capable model (GPT-5
nano). Second, every step has well-defined inputs and outputs that
serialize to C# record types, making the pipeline fully type-safe and
testable. Third, the human is always the final authority --- the AI
accelerates and augments, but a human reviewer approves every decision.

The following sections detail each of the 13 workflow steps, including
purpose, inputs/outputs, recommended LLM with cost rationale, and .NET
implementation guidance.

**Workflow Overview**

The pipeline consists of 13 sequential steps organized into four phases:

  ---------------- ------------------------------------- ----------------
  **Phase**        **Steps**                             **Primary LLMs**

  **Discovery**    01 Ideation → 02 Evaluation → 03      Opus, Sonnet,
                   Market Analysis → 04 Tech Feasibility Gemini
                   → 05 Go/No-Go                         

  **Planning**     06 Product Planning → 07 Architecture Opus, Sonnet
                   Design → 08 Backlog Generation        

  **Build**        09 Code Generation → 10 Testing → 11  Sonnet, Opus,
                   Code Review                           nano

  **Ship**         12 Deployment Prep → 13 Human Review  Sonnet, Human
                   & Approval                            
  ---------------- ------------------------------------- ----------------

Each step is designed to be idempotent and re-runnable. If the human
reviewer at Step 13 rejects with feedback, the pipeline can re-enter at
any step with the feedback injected as additional context. The
orchestrator should maintain full pipeline state in a durable store
(e.g., Azure Table Storage or SQL Server) so that progress is never
lost.

**Detailed Workflow Steps**

**Step 01: Idea Generation**

**Purpose**

Generate a batch of novel product ideas based on domain constraints,
market trends, and technology capabilities. This is the creative spark
of the pipeline --- the AI should produce diverse, non-obvious concepts
rather than incremental improvements.

**Input**

Domain/industry context, technology constraints (.NET ecosystem), target
audience profile, optional seed themes or problem statements, and any
prior rejected ideas to avoid repetition.

**Output**

A structured list of 5--15 product ideas, each containing: a concise
title, one-paragraph description, target user persona, core value
proposition, and initial category tags (e.g., SaaS, developer tool,
consumer app).

**Recommended LLM**

**Claude Opus 4.6**

Opus excels at creative, divergent reasoning and produces
well-structured outputs. Its strong instruction-following ensures ideas
come back in the exact schema your .NET orchestrator expects. At
\$5/\$25 per million tokens, the cost is justified since this step runs
infrequently (once per batch) and quality here determines the value of
everything downstream.

**.NET Implementation Notes**

Use Semantic Kernel or a direct HttpClient call to the Anthropic API.
Define a C# record type (ProductIdea) and have the LLM return JSON that
deserializes cleanly. Consider using a system prompt with few-shot
examples of good vs. bad ideas to steer quality.

**Step 02: Idea Evaluation & Scoring**

**Purpose**

Apply structured evaluation criteria to each generated idea. Score ideas
on novelty, feasibility, market potential, differentiation, and
alignment with your capabilities. This acts as the first filter,
reducing the batch to the top 3--5 candidates.

**Input**

The full list of product ideas from Step 01, plus evaluation rubric
(weighted criteria), your team's skill profile, existing product
portfolio (to avoid cannibalization), and budget constraints.

**Output**

Each idea scored 1--10 on each criterion, with a weighted composite
score, a short justification per score, and a ranked list with a clear
recommendation of which ideas to advance.

**Recommended LLM**

**Claude Sonnet 4.6**

Sonnet 4.6 at \$3/\$15 per million tokens offers strong analytical
reasoning at a much lower cost than Opus. Evaluation is more structured
and convergent than ideation, so you don't need the creative ceiling of
Opus. Sonnet handles rubric-based scoring and comparative analysis
extremely well.

**.NET Implementation Notes**

Pass the rubric as a structured system prompt. Use JSON mode to get
scores back as a typed object. Implement a simple scoring aggregator in
C# that computes weighted averages and applies any hard filters (e.g.,
reject anything scoring below 4 on feasibility).

**Step 03: Market Viability Analysis**

**Purpose**

For each surviving idea, determine whether a real market exists. Analyze
competitors, estimate total addressable market (TAM), identify gaps in
existing solutions, and flag any regulatory or timing concerns.

**Input**

Top-ranked ideas from Step 02, industry reports or web search results
(if integrated), competitor product data, and target
geography/demographic.

**Output**

A market viability report per idea containing: TAM/SAM/SOM estimates,
competitive landscape summary (top 3--5 competitors with
strengths/weaknesses), identified market gaps, risk factors, and a
go/no-go recommendation with confidence level.

**Recommended LLM**

**Gemini 3.1 Pro (with Grounding/Search)**

Gemini 3.1 Pro at \$2/\$12 per million tokens has built-in grounding
with Google Search, which is critical for market analysis. It can pull
real-time competitor data, pricing info, and market trends directly. Its
1M token context window handles large research inputs. For market
research specifically, access to current data trumps raw reasoning
quality.

**.NET Implementation Notes**

Call the Gemini API via the Google Cloud .NET SDK or REST. Enable the
grounding feature so the model can search for real competitor data.
Parse the structured response and store market reports in your pipeline
state. Consider caching search results to avoid redundant API calls if
re-running.

**Step 04: Technical Feasibility & Build Complexity**

**Purpose**

Assess how difficult each product would be to build in .NET. Estimate
effort in developer-weeks, identify key technical risks, determine which
existing libraries/frameworks apply, and flag any components that
require specialized expertise.

**Input**

Product ideas with market reports, your tech stack details (.NET
version, cloud provider, existing services), team size and skill matrix,
and any architectural constraints.

**Output**

A technical feasibility report per idea containing: estimated build
effort (broken into phases), complexity rating (1--10), technology stack
recommendation, key technical risks with mitigations, identification of
build-vs-buy decisions, and a list of required NuGet packages or
third-party services.

**Recommended LLM**

**Claude Opus 4.6**

Technical architecture assessment in .NET requires deep understanding of
the ecosystem --- NuGet packages, ASP.NET Core patterns, Entity
Framework, Azure services, etc. Opus 4.6 has the strongest architectural
reasoning capabilities and produces the most reliable effort estimates.
The cost is justified because bad estimates here cascade into every
downstream step.

**.NET Implementation Notes**

Provide your actual .csproj dependencies and solution structure as
context. Use a detailed system prompt that includes your team's velocity
data if available. The output should map directly to a C#
TechnicalAssessment model that feeds into subsequent planning steps.

**Step 05: Go/No-Go Decision Gate**

**Purpose**

Synthesize all analysis from Steps 02--04 into a single, defensible
recommendation. This is the critical decision point where the pipeline
either advances an idea to planning or kills it. The AI provides the
recommendation, but this step should also surface the decision to a
human dashboard.

**Input**

Evaluation scores (Step 02), market viability reports (Step 03),
technical feasibility reports (Step 04), and any business rules or
strategic priorities you've defined.

**Output**

A decision matrix showing each idea's composite score across all
dimensions, a clear ranked recommendation (build / defer / kill), key
assumptions that could invalidate the decision, and a one-page executive
summary suitable for stakeholder review.

**Recommended LLM**

**Claude Sonnet 4.6**

This is a synthesis and summarization task that doesn't require the
creative depth of Opus or the search capabilities of Gemini. Sonnet 4.6
excels at taking multiple structured inputs and producing clean,
well-reasoned summaries. Cost-effective at \$3/\$15 for what is
essentially a consolidation step.

**.NET Implementation Notes**

Aggregate all prior step outputs into a single context object. Use a
decision framework prompt that forces the model to weigh trade-offs
explicitly. Store the decision output and surface it via a SignalR
notification or email to the human reviewer for confirmation before
proceeding.

**Step 06: Product Planning & Requirements**

**Purpose**

For the approved idea(s), generate a comprehensive product requirements
document (PRD). Define user stories, acceptance criteria, MVP scope vs.
future phases, success metrics, and key milestones.

**Input**

The approved idea with all prior analysis, target launch timeline,
resource constraints, user personas from Step 01, and any stakeholder
feedback from the decision gate.

**Output**

A structured PRD containing: product vision statement, user stories with
acceptance criteria (in Given/When/Then format), MVP feature list vs.
Phase 2+ backlog, success metrics with targets, key milestones and
dependencies, and risk register.

**Recommended LLM**

**Claude Opus 4.6**

Product planning requires both creative thinking (defining the right
features) and rigorous structure (proper user stories, measurable
criteria). Opus 4.6 produces the highest-quality PRDs with well-formed
acceptance criteria. This document drives all downstream development, so
quality matters more than cost here.

**.NET Implementation Notes**

Output the PRD both as a structured JSON object (for programmatic
consumption by downstream steps) and as a Markdown document (for human
review). Use C# record types to model UserStory, AcceptanceCriteria, and
Milestone objects.

**Step 07: Architecture Design**

**Purpose**

Design the technical architecture for the approved product. Define the
system components, data models, API contracts, cloud infrastructure,
security approach, and integration points.

**Input**

PRD from Step 06, technical feasibility report from Step 04, target
cloud platform (e.g., Azure), performance requirements, compliance
requirements, and existing system landscape.

**Output**

An architecture document containing: high-level system diagram
description (components and their relationships), data model with entity
relationships, API endpoint specifications (OpenAPI format),
infrastructure-as-code recommendations, security architecture (auth,
encryption, data handling), and CI/CD pipeline design.

**Recommended LLM**

**Claude Opus 4.6**

Architecture is the most consequential technical step. Opus 4.6's strong
understanding of .NET architecture patterns (Clean Architecture, CQRS,
MediatR, etc.) and its ability to reason about distributed systems makes
it the right choice. It also produces coherent, internally consistent
designs across multiple components. Do not economize on this step.

**.NET Implementation Notes**

Prompt the model with your preferred .NET architectural patterns (e.g.,
Clean Architecture with MediatR). Have it generate actual C# interface
definitions, Entity Framework DbContext schemas, and appsettings.json
configuration templates. Output should include a solution structure
(.sln with project references).

**Step 08: Backlog Generation & Sprint Planning**

**Purpose**

Break the architecture and PRD down into discrete, estimable work items.
Generate a prioritized product backlog with epics, stories, and tasks,
then organize them into suggested sprints.

**Input**

PRD from Step 06, architecture document from Step 07, team capacity
(number of developers, sprint length), and any hard deadlines.

**Output**

A structured backlog containing: epics with descriptions, user stories
broken into tasks (each with estimated story points), sprint plan with
stories allocated to sprints based on dependencies and capacity,
definition of done for each story, and any blocked items with identified
dependencies.

**Recommended LLM**

**Claude Sonnet 4.6**

Backlog generation is a high-volume, structured decomposition task.
Sonnet 4.6 handles this extremely well at \$3/\$15 --- significantly
cheaper than Opus for what is largely a breakdown and organization
exercise. The key quality requirement is consistency and completeness,
which Sonnet delivers reliably.

**.NET Implementation Notes**

Output in a format that can be directly imported into Azure DevOps or
Jira via their REST APIs. Model the backlog as a list of WorkItem
objects with fields matching your project management tool's schema.
Consider generating ADO work item JSON that can be batch-created via the
Azure DevOps .NET client library.

**Step 09: Code Generation (Builder)**

**Purpose**

Generate the actual implementation code based on the architecture and
backlog. This step produces working .NET code: projects, classes,
interfaces, database migrations, API controllers, services, and
configuration files.

**Input**

Architecture document from Step 07, current backlog item (story + tasks)
being implemented, any existing codebase context, coding standards
document, and relevant NuGet package documentation.

**Output**

Working C# source files organized by project, including: solution and
project files, domain models, service interfaces and implementations,
API controllers with request/response DTOs, Entity Framework migrations,
dependency injection configuration, and appsettings files.

**Recommended LLM**

**Claude Sonnet 4.6 (primary) + Claude Opus 4.6 (for complex
components)**

Sonnet 4.6 is the workhorse for code generation --- it produces clean,
idiomatic .NET code at \$3/\$15 per million tokens, and the volume of
code generated in this step makes cost a real factor. Route complex
algorithmic work, security-critical code, or performance-sensitive
components to Opus 4.6 for higher accuracy. This two-tier approach
optimizes cost without sacrificing quality where it matters.

**.NET Implementation Notes**

Use a code generation orchestrator in C# that feeds one story at a time,
provides the relevant architecture context, and writes output files to
disk. Validate generated code compiles using the Roslyn compiler APIs
(Microsoft.CodeAnalysis) before proceeding. Use prompt chaining:
generate interface first, then implementation, then tests.

**Step 10: Automated Testing**

**Purpose**

Generate and execute comprehensive tests for the generated code. This
includes unit tests, integration tests, and validation of API contracts.
The AI both writes the tests and analyzes test results to identify
issues.

**Input**

Generated source code from Step 09, the user story's acceptance
criteria, architecture document for integration test context, and any
test infrastructure configuration.

**Output**

Test files (xUnit or NUnit) for each component, test execution results
with pass/fail status, code coverage report, identified bugs or issues
with suggested fixes, and a test quality assessment (are edge cases
covered, are tests meaningful vs. trivial).

**Recommended LLM**

**Claude Sonnet 4.6 (test generation) + GPT-5 nano (test result
analysis)**

Sonnet 4.6 writes excellent, comprehensive test code that covers edge
cases well. For analyzing test output logs (pass/fail parsing, error
categorization), GPT-5 nano at \$0.05/\$0.40 per million tokens is more
than sufficient and dramatically cheaper. This step generates high token
volume, so the cost savings from using a smaller model for analysis are
significant.

**.NET Implementation Notes**

Generate xUnit tests with FluentAssertions. Execute tests via dotnet
test \--logger trx and parse the TRX XML results. Feed failures back to
the LLM for fix suggestions. Implement a retry loop: generate test → run
→ if fail, send error back to LLM → regenerate → run again (max 3
retries).

**Step 11: Code Review & Quality Gate**

**Purpose**

Perform an automated code review against your coding standards, security
best practices, and architectural compliance. This step catches issues
that tests alone won't find: poor naming, architectural violations,
security vulnerabilities, performance anti-patterns, and maintainability
concerns.

**Input**

All generated source code, coding standards document, architecture
document (to verify compliance), security checklist, and any static
analysis tool output (e.g., from dotnet analyzers or SonarQube).

**Output**

A code review report containing: issues categorized by severity
(critical, major, minor, suggestion), specific file and line references,
suggested fixes for each issue, an overall quality score, and a
pass/fail gate decision.

**Recommended LLM**

**Claude Opus 4.6**

Code review requires the deepest understanding of .NET patterns,
security implications, and architectural intent. Opus 4.6's superior
reasoning catches subtle issues that smaller models miss --- things like
improper async/await usage, missing cancellation token propagation, EF
Core N+1 query patterns, and OWASP-relevant vulnerabilities. This is a
quality gate; don't cut costs here.

**.NET Implementation Notes**

Run dotnet format and Roslyn analyzers first, then feed the AI the code
plus analyzer output. The AI adds value by catching semantic issues that
static analysis misses. Model the review output as a list of
CodeReviewFinding objects that can be pushed to your PR system.

**Step 12: Deployment Preparation**

**Purpose**

Generate all deployment artifacts: Dockerfiles, Kubernetes manifests or
Azure deployment templates, CI/CD pipeline definitions, environment
configuration, database migration scripts, and rollback procedures.

**Input**

Finalized source code, architecture document (infrastructure section),
target environment details (Azure subscription, Kubernetes cluster,
etc.), existing CI/CD templates, and environment variables/secrets list.

**Output**

Deployment package containing: Dockerfile(s), docker-compose.yml for
local dev, Azure Bicep or ARM templates (or Kubernetes YAML), GitHub
Actions or Azure DevOps pipeline YAML, environment-specific
configuration files, database migration execution script, rollback
procedure document, and health check endpoint configuration.

**Recommended LLM**

**Claude Sonnet 4.6**

Deployment artifact generation is a structured, template-driven task
that Sonnet 4.6 handles well at \$3/\$15. The patterns for Dockerfiles,
Bicep templates, and CI/CD pipelines are well-established, and Sonnet
produces correct, production-ready configurations. Opus would be
overkill for this step unless your infrastructure is unusually complex.

**.NET Implementation Notes**

Use templated prompts that include your organization's standard
Dockerfile base image, Azure resource naming conventions, and pipeline
stages. Validate Bicep templates with az bicep build before proceeding.
Generate ARM/Bicep that includes Application Insights, Key Vault
references, and managed identity configuration.

**Step 13: Human Review & Approval**

**Purpose**

Present the complete pipeline output to a human reviewer for final
approval. This is not just a rubber stamp --- the reviewer should have
access to every artifact, every decision rationale, and every test
result from all prior steps. The human can approve, reject with feedback
(sending items back to specific steps), or approve with modifications.

**Input**

The complete pipeline output: idea description, market analysis,
technical assessment, decision rationale, PRD, architecture, backlog,
generated code, test results, code review report, and deployment
artifacts. Also include a pipeline execution summary showing which LLMs
were used at each step, token costs, and elapsed time.

**Output**

Human decision: approved (proceed to deploy), rejected with feedback
(specifying which step to revisit and what to change), or approved with
modifications (human edits specific artifacts before deployment). The
feedback should be captured in a structured format that the pipeline can
act on.

**Recommended LLM**

**No LLM (human-in-the-loop)**

This step is intentionally human-only. The entire pipeline exists to
augment human judgment, not replace it. A human reviewer catches
business context that no LLM has, validates that the product actually
makes sense for your organization, and provides the accountability that
AI cannot. Consider using a lightweight LLM (Haiku at \$1/\$5) only to
generate a summary dashboard of all artifacts for the reviewer.

**.NET Implementation Notes**

Build a Blazor or ASP.NET Core MVC review dashboard that presents all
artifacts in an organized, navigable UI. Include diff views for code,
rendered architecture diagrams, and a simple approve/reject/feedback
workflow. Store the review decision and any feedback in your pipeline
state store. Use SignalR for real-time status updates.

**LLM Pricing Reference**

The following table summarizes current API pricing (as of March 2026)
for all models recommended in this workflow. Prices are per million
tokens. Use this to estimate your per-run pipeline costs based on
expected token volumes.

  ------------- -------------- ----------- ------------ ------------- ---------------------------
  **Model**     **Provider**   **Input**   **Output**   **Context**   **Best For**

  **Claude Opus Anthropic      \$5.00      \$25.00      200K          Complex reasoning,
  4.6**                                                               architecture, code review

  **Claude      Anthropic      \$3.00      \$15.00      200K          Code generation, structured
  Sonnet 4.6**                                                        analysis, planning

  **Claude      Anthropic      \$1.00      \$5.00       200K          Summaries, simple
  Haiku 4.5**                                                         classification, dashboards

  **GPT-5.2**   OpenAI         \$1.75      \$14.00      256K          General reasoning, broad
                                                                      knowledge tasks

  **GPT-5       OpenAI         \$0.05      \$0.40       128K          Log parsing, simple
  nano**                                                              classification, high-volume

  **Gemini 3.1  Google         \$2.00      \$12.00      1M            Market research with
  Pro**                                                               grounding, large-context
                                                                      analysis

  **Gemini 3    Google         \$0.50      \$3.00       1M            Budget alternative for
  Flash**                                                             structured tasks

  **DeepSeek    DeepSeek       \$0.14      \$0.28       128K          Ultra-budget bulk
  V3**                                                                processing, fallback model
  ------------- -------------- ----------- ------------ ------------- ---------------------------

Cost optimization tips: Use prompt caching aggressively --- system
prompts for the code generation and testing steps remain constant across
stories. Anthropic and OpenAI both offer cached token discounts of up to
90%. Use batch APIs (50% discount) for non-time-sensitive steps like
backlog generation. Monitor token usage per step and adjust model
selection if a cheaper model performs adequately.

**Estimated Per-Run Pipeline Cost**

The following estimates assume a single product idea flowing through the
complete pipeline, generating a small-to-medium .NET application
(roughly 15--20 source files with tests and deployment artifacts).
Actual costs will vary based on idea complexity and code volume.

  ---------------------- ---------------- ---------- ----------- --------------
  **Step**               **Model**        **Est.     **Est.      **Est. Cost**
                                          Input**    Output**    

  01 Idea Generation     Opus 4.6         \~5K       \~10K       \$0.30

  02 Evaluation          Sonnet 4.6       \~15K      \~8K        \$0.17

  03 Market Analysis     Gemini 3.1 Pro   \~20K      \~15K       \$0.22

  04 Tech Feasibility    Opus 4.6         \~20K      \~12K       \$0.40

  05 Go/No-Go            Sonnet 4.6       \~25K      \~5K        \$0.15

  06 Product Planning    Opus 4.6         \~15K      \~20K       \$0.58

  07 Architecture        Opus 4.6         \~20K      \~30K       \$0.85

  08 Backlog             Sonnet 4.6       \~30K      \~25K       \$0.47

  09 Code Generation     Sonnet 4.6       \~80K      \~120K      \$2.04

  10 Testing             Sonnet + nano    \~60K      \~80K       \$1.40

  11 Code Review         Opus 4.6         \~100K     \~15K       \$0.88

  12 Deployment          Sonnet 4.6       \~30K      \~40K       \$0.69

  13 Human Review        Haiku (summary)  \~50K      \~5K        \$0.08

                                                     **TOTAL**   **\~\$8.23**
  ---------------------- ---------------- ---------- ----------- --------------

At roughly \$8--15 per full pipeline run (depending on complexity), the
cost of having AI generate, evaluate, plan, build, test, and prepare a
product for deployment is remarkably low. The primary cost driver is
Step 09 (Code Generation) due to token volume. With prompt caching
enabled, expect 30--50% total savings.

**.NET Implementation Architecture**

**Orchestrator Design**

The pipeline orchestrator should be built as a .NET 9 worker service or
ASP.NET Core background service that manages step execution, state
persistence, and error handling. Key design decisions:

-   Use the Pipeline pattern (or a lightweight workflow engine like Elsa
    Workflows) to define step ordering and conditional branching.

-   Each step is a class implementing an IWorkflowStep\<TInput,
    TOutput\> interface, making steps independently testable and
    replaceable.

-   Pipeline state is persisted to a durable store (SQL Server, Azure
    Cosmos DB, or Azure Table Storage) after each step completes,
    enabling resume-from-failure.

-   LLM calls are abstracted behind an ILlmClient interface with
    implementations for Anthropic, OpenAI, and Google APIs. This makes
    model swapping a configuration change, not a code change.

-   Use Polly for retry policies on LLM API calls (exponential backoff,
    circuit breaker for rate limits).

**Key NuGet Packages**

-   Microsoft.SemanticKernel --- Unified LLM abstraction layer with
    built-in prompt templating, function calling, and multi-provider
    support.

-   Anthropic.SDK or direct HttpClient --- For Claude API calls if
    Semantic Kernel's Anthropic connector doesn't meet your needs.

-   Microsoft.CodeAnalysis (Roslyn) --- For compile-time validation of
    generated C# code before it enters the testing step.

-   Polly --- Resilience and transient-fault handling for all external
    API calls.

-   FluentAssertions + xUnit --- For test generation targets in Step 10.

-   Azure.ResourceManager (Bicep/ARM) --- For validating and deploying
    infrastructure templates in Step 12.

**Multi-LLM Routing Strategy**

Implement a model router that selects the appropriate LLM based on the
current step and optional complexity signals. A simple approach: define
a dictionary mapping WorkflowStepType to a model configuration
(provider, model name, temperature, max tokens). For steps that use two
models (like Step 09), implement a complexity classifier that examines
the story description and routes simple CRUD stories to Sonnet and
complex algorithmic stories to Opus.

**Recommendations & Next Steps**

To get this pipeline into production, consider the following phased
approach:

-   **Phase 1 --- Proof of Concept (2--3 weeks):** Implement Steps
    01--05 as a console application. Use hardcoded prompts, validate
    that the LLM outputs deserialize correctly into your C# types, and
    manually review the quality of ideation, evaluation, and market
    analysis outputs.

-   **Phase 2 --- Planning Pipeline (2--3 weeks):** Add Steps 06--08.
    This is where you'll refine your prompt engineering the most, as PRD
    and architecture quality directly determines code generation quality
    downstream.

-   **Phase 3 --- Build Pipeline (3--4 weeks):** Implement Steps 09--11
    with the compile-validate-test feedback loop. This is the most
    technically challenging phase. Start with a simple CRUD API as your
    test case before attempting complex business logic.

-   **Phase 4 --- Ship Pipeline (1--2 weeks):** Add Steps 12--13. Build
    the human review dashboard. Integrate with your CI/CD system. Run
    end-to-end tests with a real product idea.

Throughout all phases, invest heavily in prompt engineering. The quality
of your system prompts is the single biggest lever on output quality.
Version control your prompts alongside your code, and treat prompt
changes with the same rigor as code changes --- review them, test them,
and measure their impact.

Finally, build observability into every step from the start. Log token
usage, latency, and output quality scores for every LLM call. This data
will be invaluable for optimizing model selection, identifying prompt
regressions, and building the business case for the pipeline's ROI.
