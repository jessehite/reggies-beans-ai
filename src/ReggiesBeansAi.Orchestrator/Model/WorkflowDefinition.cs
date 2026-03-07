namespace ReggiesBeansAi.Orchestrator.Model;

public sealed class WorkflowDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<StageDefinition> Stages { get; init; }

    internal WorkflowDefinition() { }
}
