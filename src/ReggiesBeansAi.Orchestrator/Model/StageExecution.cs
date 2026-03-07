namespace ReggiesBeansAi.Orchestrator.Model;

public class StageExecution
{
    public required string StageId { get; init; }
    public StageStatus Status { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public static StageExecution CreatePending(string stageId)
    {
        return new StageExecution
        {
            StageId = stageId,
            Status = StageStatus.Pending
        };
    }
}
