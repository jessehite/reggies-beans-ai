namespace ReggiesBeansAi.Orchestrator.Model;

public class WorkflowRun
{
    public required string RunId { get; init; }
    public required string WorkflowId { get; init; }
    public WorkflowStatus Status { get; set; }
    public int CurrentStageIndex { get; set; }
    public required List<StageExecution> Stages { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }

    public static WorkflowRun Create(string workflowId, IEnumerable<string> stageIds)
    {
        return new WorkflowRun
        {
            RunId = Guid.NewGuid().ToString(),
            WorkflowId = workflowId,
            Status = WorkflowStatus.Created,
            CurrentStageIndex = 0,
            Stages = stageIds.Select(StageExecution.CreatePending).ToList(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
