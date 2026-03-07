using ReggiesBeansAi.Orchestrator.Model;
using ReggiesBeansAi.Orchestrator.Persistence;

namespace ReggiesBeansAi.Orchestrator.Tests.Engine;

/// <summary>
/// A run store that delegates to InMemoryRunStore but records every save call
/// so tests can verify persistence frequency.
/// </summary>
public sealed class RecordingRunStore : IRunStore
{
    private readonly InMemoryRunStore _inner = new();
    private readonly List<WorkflowStatus> _savedStatuses = new();

    public IReadOnlyList<WorkflowStatus> SavedStatuses => _savedStatuses;
    public int SaveCount => _savedStatuses.Count;

    public Task SaveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        _savedStatuses.Add(run.Status);
        return _inner.SaveAsync(run, cancellationToken);
    }

    public Task<WorkflowRun?> LoadAsync(string runId, CancellationToken cancellationToken)
        => _inner.LoadAsync(runId, cancellationToken);

    public Task<IReadOnlyList<WorkflowRun>> ListAsync(CancellationToken cancellationToken)
        => _inner.ListAsync(cancellationToken);
}
