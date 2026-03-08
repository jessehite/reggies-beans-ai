using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Orchestrator.Engine;

public interface IWorkflowObserver
{
    Task OnStageStarting(WorkflowRun run, StageDefinition stage, CancellationToken cancellationToken);
    Task OnStageCompleted(WorkflowRun run, StageExecution stage, CancellationToken cancellationToken);
    Task OnStageFailed(WorkflowRun run, StageExecution stage, CancellationToken cancellationToken);
    Task OnRunPaused(WorkflowRun run, StageExecution pausedStage, CancellationToken cancellationToken);
    Task OnRunCompleted(WorkflowRun run, CancellationToken cancellationToken);
    Task OnRunFailed(WorkflowRun run, StageExecution failedStage, CancellationToken cancellationToken);
}
