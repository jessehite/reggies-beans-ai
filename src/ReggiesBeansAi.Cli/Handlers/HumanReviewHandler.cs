using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Cli.Handlers;

public sealed class HumanReviewHandler : StageHandler<DeploymentPackage, HumanApprovalDecision>
{
    protected override Task<HandleResult<HumanApprovalDecision>> HandleAsync(
        DeploymentPackage input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  PIPELINE COMPLETE — HUMAN REVIEW REQUIRED");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Deployment package contains {input.DeploymentFiles.Length} file(s):");

        foreach (var file in input.DeploymentFiles)
            Console.WriteLine($"  • {file.Path} ({file.FileType})");

        Console.WriteLine();
        Console.WriteLine($"Health check config: {input.HealthCheckConfig}");
        Console.WriteLine();
        Console.WriteLine("Rollback procedure:");
        Console.WriteLine($"  {input.RollbackProcedure}");
        Console.WriteLine();
        Console.WriteLine("Decision options:");
        Console.WriteLine("  [A] Approve — proceed to deploy");
        Console.WriteLine("  [R] Reject  — provide feedback and step to revisit");
        Console.WriteLine();
        Console.Write("Your decision (A/R): ");

        var answer = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (answer == "A")
        {
            Console.WriteLine("Approved. Pipeline complete.");
            Console.WriteLine();
            return Task.FromResult(HandleResult<HumanApprovalDecision>.Succeeded(
                new HumanApprovalDecision("approved", null, null)));
        }

        Console.Write("Feedback (what to change): ");
        var feedback = Console.ReadLine()?.Trim();

        Console.Write("Which step to revisit? (1-12, or leave blank for full re-run): ");
        var stepInput = Console.ReadLine()?.Trim();
        int? revisitStep = int.TryParse(stepInput, out var s) ? s : null;

        Console.WriteLine("Rejected. Pipeline will revisit from the specified step.");
        Console.WriteLine();

        return Task.FromResult(HandleResult<HumanApprovalDecision>.Succeeded(
            new HumanApprovalDecision("rejected", feedback, revisitStep)));
    }
}
