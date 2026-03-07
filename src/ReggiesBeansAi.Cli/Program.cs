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
    Console.Error.WriteLine("Error: GOOGLE_API_KEY environment variable is not set. Required for trend discovery and market analysis.");
    return 1;
}

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
var claudeClient = new ClaudeLlmClient(httpClient, anthropicKey);

var geminiModel = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-2.5-flash";
var geminiClient = new GeminiLlmClient(new HttpClient(), googleKey, geminiModel);

var handlers = new Dictionary<string, IStageHandler>
{
    ["trend-discovery"]     = new TrendDiscoveryHandler(geminiClient),
    ["opportunity-selection"] = new OpportunitySelectionHandler(),
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
var discoveryPrompt = PromptForDiscovery(args);

Console.WriteLine($"Starting Product Development pipeline — discovering trends for: {discoveryPrompt.AreaOfInterest}");
Console.WriteLine();

var initialInput = JsonSerializer.Serialize(discoveryPrompt, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

var run = await engine.StartAsync(workflow, initialInput, CancellationToken.None);

// Resume through human gates: opportunity selection (Step 01) and final review (Step 14)
while (run.Status == WorkflowStatus.WaitingForInput)
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

static DiscoveryPrompt PromptForDiscovery(string[] args)
{
    // Accept area of interest as CLI args
    if (args.Length > 0)
    {
        return new DiscoveryPrompt(
            string.Join(" ", args),
            Array.Empty<string>());
    }

    Console.WriteLine("Product Development Pipeline — Trend Discovery");
    Console.WriteLine();

    Console.Write("Area of interest (e.g. developer tools, healthcare, fintech, .NET): ");
    var area = Console.ReadLine() ?? string.Empty;

    Console.Write("Signal sources to prioritize (comma-separated, or leave blank for defaults): ");
    var sourcesInput = Console.ReadLine() ?? string.Empty;
    var sources = sourcesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.WriteLine();

    return new DiscoveryPrompt(area, sources);
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
