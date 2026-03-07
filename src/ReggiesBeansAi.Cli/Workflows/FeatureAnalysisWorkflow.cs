using ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;
using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Cli.Workflows;

public static class FeatureAnalysisWorkflow
{
    public static WorkflowDefinition Create()
    {
        return new WorkflowDefinitionBuilder("feature-analysis", "Feature Analysis")
            .AddStage<FeatureRequest, RequirementsDocument>(
                id: "analyze-requirements",
                name: "Analyze Requirements",
                maxAttempts: 3)
            .AddStage<RequirementsDocument, ImplementationPlan>(
                id: "generate-plan",
                name: "Generate Plan",
                maxAttempts: 3)
            .AddStage<ImplementationPlan, ApprovalDecision>(
                id: "human-review",
                name: "Human Review",
                requiresHumanInput: true)
            .Build();
    }
}
