using System.Text.Json;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Orchestrator.Tests.Engine;

/// <summary>
/// A configurable stub handler for engine tests.
/// Operates on a simple string-wrapper type to keep tests focused on engine behavior.
/// </summary>
public sealed class StubHandler : IStageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Queue<Func<StageHandlerResult>> _results = new();

    /// <summary>
    /// Configures the handler to succeed on the next call, passing through input as output.
    /// </summary>
    public StubHandler Succeeds()
    {
        _results.Enqueue(() => StageHandlerResult.Succeeded(_lastInputJson!));
        return this;
    }

    /// <summary>
    /// Configures the handler to succeed with specific output JSON.
    /// </summary>
    public StubHandler SucceedsWith(string outputJson)
    {
        _results.Enqueue(() => StageHandlerResult.Succeeded(outputJson));
        return this;
    }

    /// <summary>
    /// Configures the handler to fail on the next call.
    /// </summary>
    public StubHandler Fails(string error = "Simulated failure")
    {
        _results.Enqueue(() => StageHandlerResult.Failed(error));
        return this;
    }

    public int CallCount { get; private set; }

    private string? _lastInputJson;
    public string? LastInputJson => _lastInputJson;

    public Task<StageHandlerResult> ExecuteAsync(
        string inputJson,
        StageContext context,
        CancellationToken cancellationToken)
    {
        CallCount++;
        _lastInputJson = inputJson;

        if (_results.Count == 0)
            return Task.FromResult(StageHandlerResult.Succeeded(inputJson));

        var resultFactory = _results.Dequeue();
        return Task.FromResult(resultFactory());
    }
}
