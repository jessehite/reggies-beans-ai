namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record CodeReviewReport(
    CodeReviewFinding[] Findings,
    int QualityScore,
    bool Passed);

public sealed record CodeReviewFinding(
    string Severity,
    string File,
    string Description,
    string SuggestedFix);
