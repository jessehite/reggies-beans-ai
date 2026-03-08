using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using ReggiesBeansAi.Orchestrator.Engine;
using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Web;

/// <summary>
/// Bridges WorkflowEngine lifecycle events to per-run SSE channels.
/// Registered as a singleton; the SSE endpoint reads from the channel for its run.
/// </summary>
public sealed class SseWorkflowObserver : IWorkflowObserver
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<string, Channel<string>> _channels = new();

    public Channel<string> GetOrCreateChannel(string runId)
        => _channels.GetOrAdd(runId, _ => Channel.CreateUnbounded<string>());

    private Task Push(string runId, string eventType, object payload)
    {
        if (_channels.TryGetValue(runId, out var channel))
        {
            var json = JsonSerializer.Serialize(new { type = eventType, payload }, JsonOpts);
            channel.Writer.TryWrite(json);
        }
        return Task.CompletedTask;
    }

    private void Complete(string runId)
    {
        if (_channels.TryRemove(runId, out var channel))
            channel.Writer.Complete();
    }

    public Task OnStageStarting(WorkflowRun run, StageDefinition stage, CancellationToken ct)
        => Push(run.RunId, "stage-starting", new
        {
            stageId = stage.Id,
            stageName = stage.Name,
            stageIndex = run.CurrentStageIndex
        });

    public Task OnStageCompleted(WorkflowRun run, StageExecution stage, CancellationToken ct)
        => Push(run.RunId, "stage-completed", new
        {
            stageId = stage.StageId,
            outputJson = stage.OutputJson,
            completedAt = stage.CompletedAt,
            attemptCount = stage.AttemptCount
        });

    public Task OnStageFailed(WorkflowRun run, StageExecution stage, CancellationToken ct)
        => Push(run.RunId, "stage-failed", new
        {
            stageId = stage.StageId,
            error = stage.Error
        });

    public Task OnRunPaused(WorkflowRun run, StageExecution pausedStage, CancellationToken ct)
        => Push(run.RunId, "run-paused", new
        {
            currentStageIndex = run.CurrentStageIndex,
            pausedAfterStageId = pausedStage.StageId,
            stagedInputJson = run.Stages[run.CurrentStageIndex].InputJson
        });

    public Task OnRunCompleted(WorkflowRun run, CancellationToken ct)
    {
        Push(run.RunId, "run-completed", new { runId = run.RunId });
        Complete(run.RunId);
        return Task.CompletedTask;
    }

    public Task OnRunFailed(WorkflowRun run, StageExecution failedStage, CancellationToken ct)
    {
        Push(run.RunId, "run-failed", new
        {
            runId = run.RunId,
            stageId = failedStage.StageId,
            error = failedStage.Error
        });
        Complete(run.RunId);
        return Task.CompletedTask;
    }
}
