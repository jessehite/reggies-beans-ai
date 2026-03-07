namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record HumanApprovalDecision(
    string Decision,
    string? Feedback,
    int? RevisitStep);
