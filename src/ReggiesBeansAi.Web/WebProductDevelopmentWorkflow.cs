using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Web;

/// <summary>
/// Same 17-stage pipeline as the CLI workflow, but without RequiresHumanInput flags.
/// The web engine uses PauseAfterEveryStage instead, and PassThroughHandlers for the
/// three human-interaction stages (opportunity-selection, full-stack-review, human-review).
/// The browser constructs the correct JSON for those stages and POSTs it via /continue.
/// </summary>
public static class WebProductDevelopmentWorkflow
{
    public static WorkflowDefinition Create()
    {
        return new WorkflowDefinitionBuilder("product-development", "Product Development")
            .AddStage<DiscoveryPrompt, DiscoveredOpportunities>(
                id: "trend-discovery",
                name: "Trend Discovery",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<DiscoveredOpportunities, IdeationInput>(
                id: "opportunity-selection",
                name: "Opportunity Selection")
            .AddStage<IdeationInput, IdeaBatch>(
                id: "idea-generation",
                name: "Idea Generation",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<IdeaBatch, EvaluatedIdeas>(
                id: "idea-evaluation",
                name: "Idea Evaluation & Scoring",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<EvaluatedIdeas, MarketAnalysisReport>(
                id: "market-analysis",
                name: "Market Viability Analysis",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<MarketAnalysisReport, TechFeasibilityReport>(
                id: "tech-feasibility",
                name: "Technical Feasibility",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<TechFeasibilityReport, GoNoGoDecision>(
                id: "go-no-go",
                name: "Go/No-Go Decision Gate",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<GoNoGoDecision, ProductRequirementsDocument>(
                id: "product-planning",
                name: "Product Planning & Requirements",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<ProductRequirementsDocument, ArchitectureDocument>(
                id: "architecture-design",
                name: "Architecture Design",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<ArchitectureDocument, ProductBacklog>(
                id: "backlog-generation",
                name: "Backlog Generation & Sprint Planning",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<ProductBacklog, GeneratedCodePackage>(
                id: "code-generation",
                name: "Code Generation",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<GeneratedCodePackage, GeneratedFrontendPackage>(
                id: "frontend-generation",
                name: "Frontend Generation",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<GeneratedFrontendPackage, GeneratedFrontendPackage>(
                id: "full-stack-review",
                name: "Full-Stack Review")
            .AddStage<GeneratedFrontendPackage, TestResults>(
                id: "automated-testing",
                name: "Automated Testing",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<TestResults, CodeReviewReport>(
                id: "code-review",
                name: "Code Review & Quality Gate",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<CodeReviewReport, DeploymentPackage>(
                id: "deployment-prep",
                name: "Deployment Preparation",
                maxAttempts: 3,
                retryDelaySeconds: 5)
            .AddStage<DeploymentPackage, HumanApprovalDecision>(
                id: "human-review",
                name: "Human Review & Approval")
            .Build();
    }
}
