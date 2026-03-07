namespace ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;

public sealed record ImplementationPlan(
    string Approach,
    PlannedTask[] Tasks);

public sealed record PlannedTask(
    string Name,
    string Description);
