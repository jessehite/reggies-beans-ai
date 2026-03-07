namespace ReggiesBeansAi.Orchestrator.Handlers;

public sealed class StageHandlerResult
{
    public bool Success { get; }
    public string? OutputJson { get; }
    public string? Error { get; }

    private StageHandlerResult(bool success, string? outputJson, string? error)
    {
        Success = success;
        OutputJson = outputJson;
        Error = error;
    }

    public static StageHandlerResult Succeeded(string outputJson) =>
        new(true, outputJson, null);

    public static StageHandlerResult Failed(string error) =>
        new(false, null, error);
}
