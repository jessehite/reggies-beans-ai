using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Engine;
using ReggiesBeansAi.Orchestrator.Handlers;
using ReggiesBeansAi.Orchestrator.Model;
using ReggiesBeansAi.Orchestrator.Persistence;
using ReggiesBeansAi.Web;

LoadDotEnv();

var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
var googleKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
var geminiModel = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-2.5-flash";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddSingleton<SseWorkflowObserver>();
builder.Services.AddSingleton<IRunStore>(_ => new JsonFileRunStore("runs"));

// Build the handler dictionary once (LLM clients are long-lived)
builder.Services.AddSingleton<IReadOnlyDictionary<string, IStageHandler>>(_ =>
{
    var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    var claudeClient = new ClaudeLlmClient(httpClient, anthropicKey);
    var geminiClient = new GeminiLlmClient(new HttpClient(), googleKey, geminiModel);

    return new Dictionary<string, IStageHandler>
    {
        ["trend-discovery"]       = new TrendDiscoveryHandler(geminiClient),
        ["opportunity-selection"] = new PassThroughHandler(),   // browser sends IdeationInput JSON
        ["idea-generation"]       = new IdeaGenerationHandler(claudeClient),
        ["idea-evaluation"]       = new IdeaEvaluationHandler(claudeClient),
        ["market-analysis"]       = new MarketAnalysisHandler(geminiClient),
        ["tech-feasibility"]      = new TechFeasibilityHandler(claudeClient),
        ["go-no-go"]              = new GoNoGoHandler(claudeClient),
        ["product-planning"]      = new ProductPlanningHandler(claudeClient),
        ["architecture-design"]   = new ArchitectureDesignHandler(claudeClient),
        ["backlog-generation"]    = new BacklogGenerationHandler(claudeClient),
        ["code-generation"]       = new CodeGenerationHandler(claudeClient),
        ["frontend-generation"]       = new FrontendGenerationHandler(claudeClient),
        ["infrastructure-generation"] = new InfrastructureGenerationHandler(claudeClient),
        ["full-stack-review"]         = new PassThroughHandler(),   // browser just clicks Continue
        ["local-environment-startup"] = new PassThroughHandler(),   // web: user runs docker compose manually
        ["local-testing-gate"]        = new PassThroughHandler(),   // web: user clicks Continue when done testing
        ["automated-testing"]         = new AutomatedTestingHandler(claudeClient),
        ["code-review"]           = new CodeReviewHandler(claudeClient),
        ["deployment-prep"]       = new DeploymentPrepHandler(claudeClient),
        ["human-review"]          = new PassThroughHandler(),   // browser sends HumanApprovalDecision JSON
    };
});

builder.Services.AddSingleton(sp => new WorkflowEngine(
    sp.GetRequiredService<IRunStore>(),
    sp.GetRequiredService<IReadOnlyDictionary<string, IStageHandler>>(),
    sp.GetRequiredService<ILogger<WorkflowEngine>>(),
    sp.GetRequiredService<SseWorkflowObserver>())
{
    PauseAfterEveryStage = true
});

var app = builder.Build();
app.UseStaticFiles();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var workflow = WebProductDevelopmentWorkflow.Create();

// ── GET / ─────────────────────────────────────────────────────────────────
app.MapGet("/", () => Results.Redirect("/index.html"));

// ── GET /api/workflow ──────────────────────────────────────────────────────
app.MapGet("/api/workflow", () =>
{
    var stages = workflow.Stages.Select((s, i) => new
    {
        index = i,
        id = s.Id,
        name = s.Name,
        maxAttempts = s.MaxAttempts
    });
    return Results.Ok(new { id = workflow.Id, name = workflow.Name, stages });
});

// ── GET /api/runs ──────────────────────────────────────────────────────────
app.MapGet("/api/runs", async (IRunStore store) =>
    Results.Ok(await store.ListAsync(CancellationToken.None)));

// ── GET /api/runs/{id} ─────────────────────────────────────────────────────
app.MapGet("/api/runs/{id}", async (string id, IRunStore store) =>
{
    var run = await store.LoadAsync(id, CancellationToken.None);
    return run is null ? Results.NotFound() : Results.Ok(run);
});

// ── POST /api/runs/start ───────────────────────────────────────────────────
// Body: { "areaOfInterest": "...", "signalSources": ["..."] }
// Returns 202 immediately. Client polls GET /api/runs (newest run appears within ~1s).
app.MapPost("/api/runs/start", (StartRunRequest req, WorkflowEngine engine) =>
{
    var prompt = new DiscoveryPrompt(req.AreaOfInterest, req.SignalSources ?? []);
    var initialJson = JsonSerializer.Serialize(prompt, jsonOpts);

    // Fire-and-forget: engine saves the run record before executing any stage,
    // so GET /api/runs will surface the new run ID almost immediately.
    _ = Task.Run(async () =>
    {
        try { await engine.StartAsync(workflow, initialJson, CancellationToken.None); }
        catch (Exception ex) { app.Logger.LogError(ex, "StartAsync failed"); }
    });

    return Results.Accepted();
});

// ── POST /api/runs/{id}/continue ───────────────────────────────────────────
// Body: { "inputJson": "..." } — JSON to pass to the current stage.
// If inputJson is omitted, the engine reuses the staged InputJson (simple "Continue" click).
app.MapPost("/api/runs/{id}/continue", async (string id, ContinueRequest? req, WorkflowEngine engine, IRunStore store) =>
{
    var run = await store.LoadAsync(id, CancellationToken.None);
    if (run is null) return Results.NotFound();
    if (run.Status != WorkflowStatus.WaitingForInput)
        return Results.BadRequest(new { error = $"Run is {run.Status}, not WaitingForInput" });

    // Use caller-supplied JSON, or fall back to what's already staged
    var inputJson = req?.InputJson ?? run.Stages[run.CurrentStageIndex].InputJson ?? "{}";

    _ = Task.Run(async () =>
    {
        try { await engine.ResumeAsync(workflow, id, inputJson, CancellationToken.None); }
        catch (Exception ex) { app.Logger.LogError(ex, "ResumeAsync failed for run {RunId}", id); }
    });

    return Results.Accepted();
});

// ── POST /api/runs/{id}/retry ──────────────────────────────────────────────
app.MapPost("/api/runs/{id}/retry", async (string id, WorkflowEngine engine, IRunStore store) =>
{
    var run = await store.LoadAsync(id, CancellationToken.None);
    if (run is null) return Results.NotFound();
    if (run.Status != WorkflowStatus.Failed)
        return Results.BadRequest(new { error = $"Run is {run.Status}, not Failed" });

    _ = Task.Run(async () =>
    {
        try { await engine.RetryAsync(workflow, id, CancellationToken.None); }
        catch (Exception ex) { app.Logger.LogError(ex, "RetryAsync failed for run {RunId}", id); }
    });

    return Results.Accepted();
});

// ── POST /api/runs/{id}/retry/{stageIndex} ─────────────────────────────────
app.MapPost("/api/runs/{id}/retry/{stageIndex:int}", async (string id, int stageIndex, WorkflowEngine engine, IRunStore store) =>
{
    var run = await store.LoadAsync(id, CancellationToken.None);
    if (run is null) return Results.NotFound();

    _ = Task.Run(async () =>
    {
        try { await engine.RetryFromStageAsync(workflow, id, stageIndex, CancellationToken.None); }
        catch (Exception ex) { app.Logger.LogError(ex, "RetryFromStageAsync failed for run {RunId} stage {StageIndex}", id, stageIndex); }
    });

    return Results.Accepted();
});

// ── GET /api/runs/{id}/events ──────────────────────────────────────────────
// Server-Sent Events: streams JSON events as the engine progresses.
// Event types: stage-starting, stage-completed, stage-failed, run-paused, run-completed, run-failed
app.MapGet("/api/runs/{id}/events", async (string id, SseWorkflowObserver observer, HttpContext ctx) =>
{
    ctx.Response.Headers["Content-Type"] = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    var channel = observer.GetOrCreateChannel(id);
    var ct = ctx.RequestAborted;

    try
    {
        await foreach (var message in channel.Reader.ReadAllAsync(ct))
        {
            await ctx.Response.WriteAsync($"data: {message}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) { /* client disconnected — normal */ }
});

Console.WriteLine("==============================================");
Console.WriteLine("  Reggie's Beans AI — Web Dashboard");
Console.WriteLine("  http://localhost:5000");
Console.WriteLine("==============================================");

app.Run();

// ── Helpers ────────────────────────────────────────────────────────────────
static void LoadDotEnv()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir is not null)
    {
        var path = Path.Combine(dir, ".env");
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                var eq = trimmed.IndexOf('=');
                if (eq < 1) continue;
                var key = trimmed[..eq].Trim();
                var value = trimmed[(eq + 1)..].Trim();
                if (!string.IsNullOrEmpty(key) && Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
            break;
        }
        dir = Directory.GetParent(dir)?.FullName;
    }
}

record StartRunRequest(string AreaOfInterest, string[]? SignalSources);
record ContinueRequest(string? InputJson);
