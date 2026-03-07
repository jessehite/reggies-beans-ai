using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Orchestrator.Engine;

public interface IWorkflowEngine
{
    Task<WorkflowRun> StartAsync(
        WorkflowDefinition workflow,
        string initialInputJson,
        CancellationToken cancellationToken);

    Task<WorkflowRun> ResumeAsync(
        WorkflowDefinition workflow,
        string runId,
        string humanInputJson,
        CancellationToken cancellationToken);
}
