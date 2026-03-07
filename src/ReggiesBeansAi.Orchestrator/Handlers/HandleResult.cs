namespace ReggiesBeansAi.Orchestrator.Handlers;

public sealed class HandleResult<T>
{
    public bool Success { get; }
    public T? Output { get; }
    public string? Error { get; }

    private HandleResult(bool success, T? output, string? error)
    {
        Success = success;
        Output = output;
        Error = error;
    }

    public static HandleResult<T> Succeeded(T output) =>
        new(true, output, null);

    public static HandleResult<T> Failed(string error) =>
        new(false, default, error);
}
