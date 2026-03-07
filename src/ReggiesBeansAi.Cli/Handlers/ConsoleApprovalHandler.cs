using ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Cli.Handlers;

public sealed class ConsoleApprovalHandler : StageHandler<ImplementationPlan, ApprovalDecision>
{
    protected override Task<HandleResult<ApprovalDecision>> HandleAsync(
        ImplementationPlan input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  IMPLEMENTATION PLAN — REVIEW REQUIRED");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Approach: {input.Approach}");
        Console.WriteLine();
        Console.WriteLine("Tasks:");

        for (int i = 0; i < input.Tasks.Length; i++)
        {
            var task = input.Tasks[i];
            Console.WriteLine($"  {i + 1}. {task.Name}");
            Console.WriteLine($"     {task.Description}");
        }

        Console.WriteLine();
        Console.Write("Approve this plan? (y/n): ");

        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        var approved = answer == "y";

        Console.WriteLine(approved ? "Approved." : "Rejected.");
        Console.WriteLine();

        return Task.FromResult(HandleResult<ApprovalDecision>.Succeeded(
            new ApprovalDecision(approved, null)));
    }
}
