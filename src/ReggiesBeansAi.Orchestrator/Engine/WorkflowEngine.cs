using Microsoft.Extensions.Logging;
using ReggiesBeansAi.Orchestrator.Handlers;
using ReggiesBeansAi.Orchestrator.Model;
using ReggiesBeansAi.Orchestrator.Persistence;

namespace ReggiesBeansAi.Orchestrator.Engine;

public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IRunStore _runStore;
    private readonly IReadOnlyDictionary<string, IStageHandler> _handlers;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IRunStore runStore,
        IReadOnlyDictionary<string, IStageHandler> handlers,
        ILogger<WorkflowEngine> logger)
    {
        _runStore = runStore;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<WorkflowRun> StartAsync(
        WorkflowDefinition workflow,
        string initialInputJson,
        CancellationToken cancellationToken)
    {
        var run = WorkflowRun.Create(workflow.Id, workflow.Stages.Select(s => s.Id));

        _logger.LogInformation("Workflow run {RunId} created for workflow {WorkflowId}", run.RunId, run.WorkflowId);

        await _runStore.SaveAsync(run, cancellationToken);

        run.Status = WorkflowStatus.Running;

        _logger.LogInformation("Workflow run {RunId} started", run.RunId);

        await _runStore.SaveAsync(run, cancellationToken);

        return await ExecuteFromCurrentStage(workflow, run, initialInputJson, cancellationToken);
    }

    public async Task<WorkflowRun> ResumeAsync(
        WorkflowDefinition workflow,
        string runId,
        string humanInputJson,
        CancellationToken cancellationToken)
    {
        var run = await _runStore.LoadAsync(runId, cancellationToken);

        if (run is null)
            throw new InvalidOperationException($"Workflow run '{runId}' not found.");

        if (run.Status != WorkflowStatus.WaitingForInput)
            throw new InvalidOperationException(
                $"Cannot resume workflow run '{runId}' with status '{run.Status}'. Expected '{WorkflowStatus.WaitingForInput}'.");

        if (run.WorkflowId != workflow.Id)
            throw new InvalidOperationException(
                $"Workflow run '{runId}' belongs to workflow '{run.WorkflowId}', not '{workflow.Id}'.");

        run.Status = WorkflowStatus.Running;

        _logger.LogInformation("Workflow run {RunId} resumed at stage {StageId}",
            run.RunId, workflow.Stages[run.CurrentStageIndex].Id);

        await _runStore.SaveAsync(run, cancellationToken);

        return await ExecuteFromCurrentStage(workflow, run, humanInputJson, cancellationToken, isResuming: true);
    }

    private async Task<WorkflowRun> ExecuteFromCurrentStage(
        WorkflowDefinition workflow,
        WorkflowRun run,
        string currentInputJson,
        CancellationToken cancellationToken,
        bool isResuming = false)
    {
        var resumeStageIndex = run.CurrentStageIndex;

        for (int i = run.CurrentStageIndex; i < workflow.Stages.Count; i++)
        {
            run.CurrentStageIndex = i;
            var stageDef = workflow.Stages[i];
            var stageExec = run.Stages[i];

            // Check for human gate before executing.
            // Skip the gate when resuming at the exact stage we paused on — human input is already provided.
            bool shouldPauseForHuman = stageDef.RequiresHumanInput
                && stageExec.Status == StageStatus.Pending
                && !(isResuming && i == resumeStageIndex);

            if (shouldPauseForHuman)
            {
                stageExec.InputJson = currentInputJson;
                run.Status = WorkflowStatus.WaitingForInput;

                _logger.LogInformation(
                    "Workflow run {RunId} paused at stage {StageId}, waiting for human input",
                    run.RunId, stageDef.Id);

                await _runStore.SaveAsync(run, cancellationToken);
                return run;
            }

            if (!_handlers.TryGetValue(stageDef.Id, out var handler))
            {
                stageExec.Status = StageStatus.Failed;
                stageExec.Error = $"No handler registered for stage '{stageDef.Id}'.";
                FailRun(run, i);

                _logger.LogError("Workflow run {RunId} failed at stage {StageId}: {Error}",
                    run.RunId, stageDef.Id, stageExec.Error);

                await _runStore.SaveAsync(run, cancellationToken);
                return run;
            }

            stageExec.InputJson = currentInputJson;
            stageExec.Status = StageStatus.Running;
            stageExec.StartedAt = DateTimeOffset.UtcNow;
            await _runStore.SaveAsync(run, cancellationToken);

            StageHandlerResult? result = null;

            for (int attempt = 1; attempt <= stageDef.MaxAttempts; attempt++)
            {
                stageExec.AttemptCount = attempt;
                var context = new StageContext(run.RunId, stageDef.Id, attempt);

                _logger.LogInformation("Stage {StageId} started (attempt {Attempt}/{MaxAttempts})",
                    stageDef.Id, attempt, stageDef.MaxAttempts);

                result = await handler.ExecuteAsync(currentInputJson, context, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("Stage {StageId} completed", stageDef.Id);
                    break;
                }

                if (attempt < stageDef.MaxAttempts)
                {
                    _logger.LogWarning(
                        "Stage {StageId} failed (attempt {Attempt}/{MaxAttempts}), retrying in {DelaySeconds}s: {Error}",
                        stageDef.Id, attempt, stageDef.MaxAttempts, stageDef.RetryDelaySeconds, result.Error);

                    await _runStore.SaveAsync(run, cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(stageDef.RetryDelaySeconds), cancellationToken);
                }
            }

            if (result!.Success)
            {
                stageExec.Status = StageStatus.Completed;
                stageExec.OutputJson = result.OutputJson;
                stageExec.CompletedAt = DateTimeOffset.UtcNow;
                currentInputJson = result.OutputJson!;
                await _runStore.SaveAsync(run, cancellationToken);
            }
            else
            {
                stageExec.Status = StageStatus.Failed;
                stageExec.Error = result.Error;
                stageExec.CompletedAt = DateTimeOffset.UtcNow;
                FailRun(run, i);

                _logger.LogError(
                    "Stage {StageId} failed after {MaxAttempts} attempts: {Error}",
                    stageDef.Id, stageDef.MaxAttempts, result.Error);

                _logger.LogError("Workflow run {RunId} failed at stage {StageId}",
                    run.RunId, stageDef.Id);

                await _runStore.SaveAsync(run, cancellationToken);
                return run;
            }
        }

        run.Status = WorkflowStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Workflow run {RunId} completed", run.RunId);

        await _runStore.SaveAsync(run, cancellationToken);
        return run;
    }

    private static void FailRun(WorkflowRun run, int failedStageIndex)
    {
        run.Status = WorkflowStatus.Failed;
        run.CompletedAt = DateTimeOffset.UtcNow;

        for (int j = failedStageIndex + 1; j < run.Stages.Count; j++)
        {
            run.Stages[j].Status = StageStatus.Skipped;
        }
    }
}
