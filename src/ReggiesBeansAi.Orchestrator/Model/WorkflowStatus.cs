namespace ReggiesBeansAi.Orchestrator.Model;

public enum WorkflowStatus
{
    Created,
    Running,
    WaitingForInput,
    Completed,
    Failed,
    Cancelled
}
