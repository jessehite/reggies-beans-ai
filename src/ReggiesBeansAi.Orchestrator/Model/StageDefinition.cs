namespace ReggiesBeansAi.Orchestrator.Model;

public sealed class StageDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Type InputType { get; init; }
    public required Type OutputType { get; init; }
    public int MaxAttempts { get; init; } = 1;
    public int RetryDelaySeconds { get; init; } = 0;
    public bool RequiresHumanInput { get; init; }
}
