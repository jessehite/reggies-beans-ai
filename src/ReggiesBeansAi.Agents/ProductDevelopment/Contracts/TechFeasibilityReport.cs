namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record TechFeasibilityReport(IdeaTechAssessment[] Assessments);

public sealed record IdeaTechAssessment(
    string IdeaTitle,
    string EstimatedEffort,
    int ComplexityRating,
    string[] TechStackRecommendations,
    TechRisk[] KeyRisks,
    string[] BuildVsBuyDecisions,
    string[] RequiredPackages);

public sealed record TechRisk(
    string Risk,
    string Mitigation);
