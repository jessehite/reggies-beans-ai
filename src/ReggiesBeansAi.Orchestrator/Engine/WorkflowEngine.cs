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
    private readonly IWorkflowObserver? _observer;

    /// <summary>
    /// When true, the engine pauses after every stage completes (not just human-gate stages).
    /// The next stage's input is stored in its InputJson; call ResumeAsync with that value to continue.
    /// </summary>
    public bool PauseAfterEveryStage { get; set; } = false;

    public WorkflowEngine(
        IRunStore runStore,
        IReadOnlyDictionary<string, IStageHandler> handlers,
        ILogger<WorkflowEngine> logger,
        IWorkflowObserver? observer = null)
    {
        _runStore = runStore;
        _handlers = handlers;
        _logger = logger;
        _observer = observer;
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

    public async Task<WorkflowRun> RetryAsync(
        WorkflowDefinition workflow,
        string runId,
        CancellationToken cancellationToken)
    {
        var run = await _runStore.LoadAsync(runId, cancellationToken);

        if (run is null)
            throw new InvalidOperationException($"Workflow run '{runId}' not found.");

        if (run.Status != WorkflowStatus.Failed)
            throw new InvalidOperationException(
                $"Cannot retry workflow run '{runId}' with status '{run.Status}'. Expected '{WorkflowStatus.Failed}'.");

        if (run.WorkflowId != workflow.Id)
            throw new InvalidOperationException(
                $"Workflow run '{runId}' belongs to workflow '{run.WorkflowId}', not '{workflow.Id}'.");

        // Reset the failed stage and all subsequent stages back to Pending
        for (int i = run.CurrentStageIndex; i < run.Stages.Count; i++)
        {
            var stage = run.Stages[i];
            stage.Status = StageStatus.Pending;
            stage.Error = null;
            stage.OutputJson = null;
            stage.StartedAt = null;
            stage.CompletedAt = null;
            stage.AttemptCount = 0;
        }

        var inputJson = run.Stages[run.CurrentStageIndex].InputJson!;
        run.Stages[run.CurrentStageIndex].InputJson = null;

        run.Status = WorkflowStatus.Running;
        run.CompletedAt = null;

        _logger.LogInformation("Workflow run {RunId} retrying from stage {StageId}",
            run.RunId, workflow.Stages[run.CurrentStageIndex].Id);

        await _runStore.SaveAsync(run, cancellationToken);

        return await ExecuteFromCurrentStage(workflow, run, inputJson, cancellationToken);
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

                if (_observer is not null)
                    await _observer.OnRunPaused(run, stageExec, cancellationToken);

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

            if (_observer is not null)
                await _observer.OnStageStarting(run, stageDef, cancellationToken);

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

                if (_observer is not null)
                    await _observer.OnStageCompleted(run, stageExec, cancellationToken);

                // Pause after every stage for review (if enabled), except after the last stage
                if (PauseAfterEveryStage && i < workflow.Stages.Count - 1)
                {
                    run.CurrentStageIndex = i + 1;
                    run.Stages[i + 1].InputJson = currentInputJson;
                    run.Status = WorkflowStatus.WaitingForInput;

                    _logger.LogInformation(
                        "Workflow run {RunId} paused after stage {StageId} for review",
                        run.RunId, stageDef.Id);

                    await _runStore.SaveAsync(run, cancellationToken);

                    if (_observer is not null)
                        await _observer.OnRunPaused(run, stageExec, cancellationToken);

                    return run;
                }
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

                if (_observer is not null)
                {
                    await _observer.OnStageFailed(run, stageExec, cancellationToken);
                    await _observer.OnRunFailed(run, stageExec, cancellationToken);
                }

                return run;
            }

            // After first resume iteration, subsequent stages are not "resuming"
            isResuming = false;
        }

        run.Status = WorkflowStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Workflow run {RunId} completed", run.RunId);

        await _runStore.SaveAsync(run, cancellationToken);

        if (_observer is not null)
            await _observer.OnRunCompleted(run, cancellationToken);

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
