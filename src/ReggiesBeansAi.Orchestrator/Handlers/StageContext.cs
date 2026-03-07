namespace ReggiesBeansAi.Orchestrator.Handlers;

public sealed record StageContext(
    string RunId,
    string StageId,
    int AttemptNumber);
