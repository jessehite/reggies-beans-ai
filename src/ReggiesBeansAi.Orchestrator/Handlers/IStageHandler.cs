namespace ReggiesBeansAi.Orchestrator.Handlers;

public interface IStageHandler
{
    Task<StageHandlerResult> ExecuteAsync(
        string inputJson,
        StageContext context,
        CancellationToken cancellationToken);
}
