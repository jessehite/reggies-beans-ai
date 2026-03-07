using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReggiesBeansAi.Agents.FeatureAnalysis;
using ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Cli.Handlers;
using ReggiesBeansAi.Cli.Persistence;
using ReggiesBeansAi.Cli.Workflows;
using ReggiesBeansAi.Orchestrator.Engine;
using ReggiesBeansAi.Orchestrator.Handlers;
using ReggiesBeansAi.Orchestrator.Model;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Error: ANTHROPIC_API_KEY environment variable is not set.");
    return 1;
}

var description = args.Length > 0
    ? string.Join(" ", args)
    : PromptForDescription();

if (string.IsNullOrWhiteSpace(description))
{
    Console.Error.WriteLine("Error: feature description is required.");
    return 1;
}

// Wire up dependencies
using var httpClient = new HttpClient();
var llmClient = new ClaudeLlmClient(httpClient, apiKey);

var handlers = new Dictionary<string, IStageHandler>
{
    ["analyze-requirements"] = new AnalyzeRequirementsHandler(llmClient),
    ["generate-plan"]        = new GeneratePlanHandler(llmClient),
    ["human-review"]         = new ConsoleApprovalHandler()
};

var runStore = new JsonFileRunStore("runs");

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<WorkflowEngine>();

var engine = new WorkflowEngine(runStore, handlers, logger);
var workflow = FeatureAnalysisWorkflow.Create();

var initialInput = JsonSerializer.Serialize(new FeatureRequest(description));

Console.WriteLine($"Starting Feature Analysis for: {description}");
Console.WriteLine();

var run = await engine.StartAsync(workflow, initialInput, CancellationToken.None);

// Human review gate: resume immediately — the ConsoleApprovalHandler does its own I/O
if (run.Status == WorkflowStatus.WaitingForInput)
{
    var stagedInput = run.Stages[run.CurrentStageIndex].InputJson!;
    run = await engine.ResumeAsync(workflow, run.RunId, stagedInput, CancellationToken.None);
}

PrintSummary(run);

return run.Status == WorkflowStatus.Completed ? 0 : 1;

static string PromptForDescription()
{
    Console.Write("Feature description: ");
    return Console.ReadLine() ?? string.Empty;
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
