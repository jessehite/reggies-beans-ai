using System.Collections.Concurrent;
using System.Text.Json;
using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Orchestrator.Persistence;

public sealed class InMemoryRunStore : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task SaveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(run, JsonOptions);
        _store[run.RunId] = json;
        return Task.CompletedTask;
    }

    public Task<WorkflowRun?> LoadAsync(string runId, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(runId, out var json))
        {
            var run = JsonSerializer.Deserialize<WorkflowRun>(json, JsonOptions);
            return Task.FromResult(run);
        }

        return Task.FromResult<WorkflowRun?>(null);
    }

    public Task<IReadOnlyList<WorkflowRun>> ListAsync(CancellationToken cancellationToken)
    {
        var runs = _store.Values
            .Select(json => JsonSerializer.Deserialize<WorkflowRun>(json, JsonOptions)!)
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowRun>>(runs);
    }
}
