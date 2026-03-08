using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Web;

/// <summary>
/// Echoes its input JSON as output. Used for human-gate stages in the web UI —
/// the browser constructs the correct output JSON and POSTs it; this handler passes it straight through.
/// </summary>
public sealed class PassThroughHandler : IStageHandler
{
    public Task<StageHandlerResult> ExecuteAsync(
        string inputJson,
        StageContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StageHandlerResult.Succeeded(inputJson));
}
