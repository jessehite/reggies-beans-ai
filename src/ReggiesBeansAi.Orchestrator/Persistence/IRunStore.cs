using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Orchestrator.Persistence;

public interface IRunStore
{
    Task SaveAsync(WorkflowRun run, CancellationToken cancellationToken);
    Task<WorkflowRun?> LoadAsync(string runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowRun>> ListAsync(CancellationToken cancellationToken);
}
