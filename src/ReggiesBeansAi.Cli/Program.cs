using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Cli.Handlers;
using ReggiesBeansAi.Cli.Persistence;
using ReggiesBeansAi.Cli.Workflows;
using ReggiesBeansAi.Orchestrator.Engine;
using ReggiesBeansAi.Orchestrator.Handlers;
using ReggiesBeansAi.Orchestrator.Model;

LoadDotEnv();

var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrWhiteSpace(anthropicKey))
{
    Console.Error.WriteLine("Error: ANTHROPIC_API_KEY environment variable is not set.");
    return 1;
}

var googleKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
if (string.IsNullOrWhiteSpace(googleKey))
{
    Console.Error.WriteLine("Error: GOOGLE_API_KEY environment variable is not set. Required for market analysis (Step 03).");
    return 1;
}

using var httpClient = new HttpClient();
var claudeClient = new ClaudeLlmClient(httpClient, anthropicKey);

var geminiModel = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-3.1-pro";
var geminiClient = new GeminiLlmClient(new HttpClient(), googleKey, geminiModel);

var handlers = new Dictionary<string, IStageHandler>
{
    ["idea-generation"]     = new IdeaGenerationHandler(claudeClient),
    ["idea-evaluation"]     = new IdeaEvaluationHandler(claudeClient),
    ["market-analysis"]     = new MarketAnalysisHandler(geminiClient),
    ["tech-feasibility"]    = new TechFeasibilityHandler(claudeClient),
    ["go-no-go"]            = new GoNoGoHandler(claudeClient),
    ["product-planning"]    = new ProductPlanningHandler(claudeClient),
    ["architecture-design"] = new ArchitectureDesignHandler(claudeClient),
    ["backlog-generation"]  = new BacklogGenerationHandler(claudeClient),
    ["code-generation"]     = new CodeGenerationHandler(claudeClient),
    ["automated-testing"]   = new AutomatedTestingHandler(claudeClient),
    ["code-review"]         = new CodeReviewHandler(claudeClient),
    ["deployment-prep"]     = new DeploymentPrepHandler(claudeClient),
    ["human-review"]        = new HumanReviewHandler()
};

var runStore = new JsonFileRunStore("runs");

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
var logger = loggerFactory.CreateLogger<WorkflowEngine>();

var engine = new WorkflowEngine(runStore, handlers, logger);
var workflow = ProductDevelopmentWorkflow.Create();
var ideationInput = PromptForIdeationInput(args);

Console.WriteLine($"Starting Product Development pipeline for domain: {ideationInput.Domain}");
Console.WriteLine();

var initialInput = JsonSerializer.Serialize(ideationInput, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

var run = await engine.StartAsync(workflow, initialInput, CancellationToken.None);

// Human review gate at Step 13: HumanReviewHandler does its own console I/O
if (run.Status == WorkflowStatus.WaitingForInput)
{
    var staged = run.Stages[run.CurrentStageIndex].InputJson!;
    run = await engine.ResumeAsync(workflow, run.RunId, staged, CancellationToken.None);
}

PrintSummary(run);

return run.Status == WorkflowStatus.Completed ? 0 : 1;

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

static IdeationInput PromptForIdeationInput(string[] args)
{
    // Accept a JSON string as the first arg for scripted use
    if (args.Length > 0 && args[0].TrimStart().StartsWith('{'))
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<IdeationInput>(args[0], new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (parsed is not null) return parsed;
        }
        catch { /* fall through to interactive prompts */ }
    }

    Console.WriteLine("Product Development Pipeline — Enter ideation parameters:");
    Console.WriteLine();

    Console.Write("Domain/industry (e.g. developer tools, healthcare, fintech): ");
    var domain = Console.ReadLine() ?? string.Empty;

    Console.Write("Target audience: ");
    var audience = Console.ReadLine() ?? string.Empty;

    Console.Write("Seed themes (comma-separated, or leave blank): ");
    var themesInput = Console.ReadLine() ?? string.Empty;
    var themes = themesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.Write("Previously rejected ideas to avoid (comma-separated, or leave blank): ");
    var rejectedInput = Console.ReadLine() ?? string.Empty;
    var rejected = rejectedInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.WriteLine();

    return new IdeationInput(domain, audience, themes, rejected);
}

static void PrintSummary(WorkflowRun run)
{
    Console.WriteLine("═══════════════════════════════════════════");
    Console.WriteLine($"  Run complete — {run.Status}");
    Console.WriteLine($"  Run ID: {run.RunId}");
    Console.WriteLine("═══════════════════════════════════════════");

    foreach (var stage in run.Stages)
    {
        var attempts = stage.AttemptCount > 1 ? $" ({stage.AttemptCount} attempts)" : string.Empty;
        Console.WriteLine($"  {stage.StageId}: {stage.Status}{attempts}");
        if (stage.Error is not null)
            Console.WriteLine($"    Error: {stage.Error}");
    }

    Console.WriteLine();
    Console.WriteLine($"Run saved to: runs/{run.RunId}.json");
}
