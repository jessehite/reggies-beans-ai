namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record GoNoGoDecision(
    IdeaDecision[] Decisions,
    string ExecutiveSummary);

public sealed record IdeaDecision(
    string IdeaTitle,
    string Recommendation,
    double CompositeScore,
    string[] KeyAssumptions,
    string Rationale);
